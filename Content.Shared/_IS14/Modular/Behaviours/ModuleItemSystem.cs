// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modular.Components;
using Content.Shared._IS14.Modular.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using System.Numerics;
using Robust.Shared.Network;
using Robust.Shared.Timing;
using System.Diagnostics.CodeAnalysis;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Extends and retracts a module's device entity.
/// </summary>
public sealed class ModuleItemSystem : ModuleBehaviourSystem<ModuleItemComponent>
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedChassisModuleSystem _modules = default!;

    /// <summary>
    ///     Devices that tried to leave the operator's hands and have to be folded away.
    ///     Doing it inline would mean tearing the device out of the very container whose
    ///     removal we just cancelled, so it waits a tick.
    /// </summary>
    private readonly List<EntityUid> _reelIn = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModuleItemComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ModuleItemComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ModuleItemComponent, EntGotRemovedFromContainerMessage>(OnDeviceRemoved);

        SubscribeLocalEvent<ChassisDeviceComponent, ContainerGettingRemovedAttemptEvent>(OnDeviceRemoveAttempt);
    }

    private void OnInit(Entity<ModuleItemComponent> ent, ref ComponentInit args)
    {
        ent.Comp.Container = _container.EnsureContainer<ContainerSlot>(ent, ent.Comp.ContainerId);
    }

    private bool TryGetDeviceContainer(Entity<ModuleItemComponent> ent, [NotNullWhen(true)] out ContainerSlot? container)
    {
        if (ent.Comp.Container != null)
        {
            container = ent.Comp.Container;
            return true;
        }

        if (_container.TryGetContainer(ent, ent.Comp.ContainerId, out var found)
            && found is ContainerSlot slot)
        {
            ent.Comp.Container = slot;
            container = slot;
            return true;
        }

        container = null;
        return false;
    }

    private void OnMapInit(Entity<ModuleItemComponent> ent, ref MapInitEvent args)
    {
        EnsureDevice(ent);
    }

    /// <summary>
    ///     Builds the device if it is missing. Normally MapInit does this, but a module
    ///     can reach a player without ever having been map-initialised — spawned straight
    ///     into a container, restored from a save — and a module with no hardware would
    ///     silently do nothing when selected.
    /// </summary>
    private bool EnsureDevice(Entity<ModuleItemComponent> ent)
    {
        if (ent.Comp.Device is { } existing && !TerminatingOrDeleted(existing))
            return true;

        if (_net.IsClient)
            return false;

        var device = Spawn(ent.Comp.DevicePrototype, new EntityCoordinates(ent, Vector2.Zero));

        if (!TryGetDeviceContainer(ent, out var container) || !_container.Insert(device, container))
        {
            Log.Error($"Failed to stow device {ent.Comp.DevicePrototype} in {ToPrettyString(ent)}.");
            Del(device);
            return false;
        }

        // Stamp it as suit property. This is what stops the player walking off with it,
        // and what the device needs to find its way home again.
        var marker = EnsureComp<ChassisDeviceComponent>(device);
        marker.Module = ent;
        Dirty(device, marker);

        ent.Comp.Device = device;
        Dirty(ent);
        return true;
    }

    /// <summary>
    ///     Active modules follow the selection switch, not mere installation.
    /// </summary>
    protected override bool RequiresActive(Entity<ModuleItemComponent> ent) => true;

    protected override void Start(Entity<ModuleItemComponent> ent, EntityUid chassis)
    {
        if (!EnsureDevice(ent) || ent.Comp.Device is not { } device)
            return;

        if (GetChassisUser(chassis) is not { } user)
            return;

        // Claim the device before it moves, so OnDeviceRemoved knows this is an
        // extension rather than the hardware being lost.
        ent.Comp.HeldBy = user;

        // Hand it straight over: inserting into a hand pulls the device out of our
        // container on its own. Removing it first would strand it — the module sits
        // inside the chassis, which sits inside the wearer, so there is no world
        // position to fall back to.
        if (!_hands.TryForcePickupAnyHand(user, device, checkActionBlocker: false))
        {
            ent.Comp.HeldBy = null;

            // Two-handed hardware needs two free hands, and silently refusing to deploy
            // reads as the module being broken.
            _popup.PopupClient(Loc.GetString("chassis-device-no-hands", ("device", device)), ent, user);

            // Put it back where it belongs and stand the module down rather than
            // leaving the suit in a half-activated state.
            if (TryGetDeviceContainer(ent, out var container))
                _container.Insert(device, container);

            if (TryComp<ChassisModuleComponent>(ent, out var module))
                _modules.Deactivate((ent.Owner, module), chassis, user, quiet: true);

            return;
        }

        Dirty(ent);
    }

    protected override void Stop(Entity<ModuleItemComponent> ent, EntityUid chassis)
    {
        if (ent.Comp.Device is not { } device || ent.Comp.HeldBy is not { } holder)
            return;

        if (TerminatingOrDeleted(device))
        {
            ent.Comp.HeldBy = null;
            return;
        }

        // Go through hands explicitly: the device is anchored to the suit, so a plain
        // container insert would be fighting the hands system rather than cooperating.
        if (!TryGetDeviceContainer(ent, out var container))
            return;

        TryComp<ChassisDeviceComponent>(device, out var marker);

        // Stand the reel-in guard down for this one removal, or it would block the very
        // retraction it exists to force.
        if (marker != null)
            marker.Stowing = true;

        if (_hands.IsHolding(holder, device))
            _hands.TryDropIntoContainer(holder, device, container, checkActionBlocker: false);
        else
            _container.Insert(device, container);

        if (marker != null)
            marker.Stowing = false;

        ent.Comp.HeldBy = null;
        Dirty(ent);
    }

    /// <summary>
    ///     If the device leaves our container by any route other than being extended,
    ///     the module has lost its hardware and should stop claiming to have it.
    /// </summary>
    private void OnDeviceRemoved(Entity<ModuleItemComponent> ent, ref EntGotRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.ContainerId || args.Entity != ent.Comp.Device)
            return;

        if (ent.Comp.HeldBy != null)
            return;

        ent.Comp.Device = null;
        Dirty(ent);
    }

    #region Reel-in

    /// <summary>
    ///     Suit hardware does not become loot. Dropping it, throwing it or stuffing it in a
    ///     bag all funnel through the same removal check, and all of them fold the device
    ///     back into the module instead of leaving it on the floor.
    /// </summary>
    private void OnDeviceRemoveAttempt(Entity<ChassisDeviceComponent> ent, ref ContainerGettingRemovedAttemptEvent args)
    {
        // Never fight an incoming server state, or the client errors out on any container
        // move the server has already decided on.
        if (_timing.ApplyingState || ent.Comp.Stowing)
            return;

        if (ent.Comp.Module is not { } module || !TryComp<ModuleItemComponent>(module, out var item))
            return;

        // Leaving its own slot is how the device gets deployed in the first place.
        if (args.Container.ID == item.ContainerId)
            return;

        args.Cancel();

        if (_net.IsServer && !_reelIn.Contains(ent.Owner))
            _reelIn.Add(ent.Owner);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_reelIn.Count == 0)
            return;

        foreach (var device in _reelIn)
        {
            if (!TryComp<ChassisDeviceComponent>(device, out var marker)
                || marker.Module is not { } module
                || !TryComp<ChassisModuleComponent>(module, out var moduleComp)
                || moduleComp.Chassis is not { } chassis)
                continue;

            var holder = CompOrNull<ModuleItemComponent>(module)?.HeldBy;

            // Switching the module off is what stows the device, and it also clears the
            // suit's selection so the UI stops claiming the tool is out.
            if (!_modules.Deactivate((module, moduleComp), chassis, holder, quiet: true))
                continue;

            if (holder != null)
            {
                _popup.PopupEntity(
                    Loc.GetString("chassis-device-reeled-in", ("device", device)),
                    holder.Value,
                    holder.Value);
            }
        }

        _reelIn.Clear();
    }

    #endregion
}
