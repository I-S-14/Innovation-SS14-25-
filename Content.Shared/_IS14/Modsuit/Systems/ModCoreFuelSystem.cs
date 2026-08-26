// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modular.Systems;
using Content.Shared._IS14.Modsuit.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Network;

namespace Content.Shared._IS14.Modsuit.Systems;

/// <summary>
///     Feeding a fuel-burning core.
///
///     Works on the core in your hand and on the suit you are wearing — the second is
///     the one that matters, because a core that had to come out of the panel every time
///     it got hungry would be a worse item than a cell you swap.
/// </summary>
public sealed class ModCoreFuelSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly ModCoreSystem _core = default!;
    [Dependency] private readonly ChassisPowerSystem _power = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;

    /// <summary>Seconds between hopper draws.</summary>
    private const float BurnInterval = 1f;

    private float _accumulator;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModCoreFuelComponent, InteractUsingEvent>(OnCoreInteract);
        SubscribeLocalEvent<ModCoreSlotComponent, InteractUsingEvent>(OnChassisInteract);
        SubscribeLocalEvent<ModCoreFuelComponent, ContainerIsInsertingAttemptEvent>(OnHopperInsertAttempt);
    }

    /// <summary>
    ///     The hopper only takes what this core actually burns. Filtering on the fuel
    ///     table rather than on a tag means a core that learns a new fuel accepts it in
    ///     the same breath, with no second list to keep in step.
    /// </summary>
    private void OnHopperInsertAttempt(Entity<ModCoreFuelComponent> ent, ref ContainerIsInsertingAttemptEvent args)
    {
        if (args.Container.ID != StorageComponent.ContainerId)
            return;

        if (!TryComp<StackComponent>(args.EntityUid, out var stack)
            || !ent.Comp.Fuel.ContainsKey(stack.StackTypeId))
        {
            args.Cancel();
        }
    }

    /// <summary>
    ///     Burns down whatever is sitting in the hopper, a little at a time, whenever the
    ///     core has room for it. Server only: this deletes stack entities, and a client
    ///     predicting that would be predicting somebody else's fuel away.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        _accumulator += frameTime;

        if (_accumulator < BurnInterval)
            return;

        _accumulator = 0f;

        var query = EntityQueryEnumerator<ModCoreFuelComponent, StorageComponent, BatteryComponent>();

        while (query.MoveNext(out var uid, out var fuel, out var storage, out var battery))
        {
            if (battery.MaxCharge - _battery.GetCharge((uid, battery)) <= 0f)
                continue;

            // One item per tick, and the enumeration stops the moment it burns something:
            // a successful burn deletes the stack out from under the list.
            foreach (var item in storage.Container.ContainedEntities)
            {
                if (TryRefuel((uid, fuel), item, null))
                    break;
            }
        }
    }

    private void OnCoreInteract(Entity<ModCoreFuelComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = TryRefuel(ent, args.Used, args.User);
    }

    /// <summary>
    ///     Fuel pushed at the suit goes to whatever core is in it.
    /// </summary>
    private void OnChassisInteract(Entity<ModCoreSlotComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled
            || _core.GetCore(ent) is not { } core
            || !TryComp<ModCoreFuelComponent>(core, out var fuel))
            return;

        args.Handled = TryRefuel((core.Owner, fuel), args.Used, args.User);
    }

    /// <summary>
    ///     Burns as much of the stack as the core has room for. Returns false when the
    ///     item is not fuel at all, so the click can go on to mean something else.
    /// </summary>
    private bool TryRefuel(Entity<ModCoreFuelComponent> ent, EntityUid used, EntityUid? user)
    {
        if (!TryComp<StackComponent>(used, out var stack)
            || !ent.Comp.Fuel.TryGetValue(stack.StackTypeId, out var joules)
            || joules <= 0f)
            return false;

        if (!TryComp<BatteryComponent>(ent, out var battery))
            return false;

        var missing = battery.MaxCharge - _battery.GetCharge((ent.Owner, battery));

        if (missing <= 0f)
        {
            if (user != null)
                _popup.PopupClient(Loc.GetString("modsuit-core-full"), ent, user);

            return true;
        }

        // Round up: the last unit is allowed to overfill rather than be refused, or a
        // core would sit one sheet short of full forever.
        var wanted = (int) MathF.Ceiling(missing / joules);
        var burned = Math.Min(wanted, _stack.GetCount((used, stack)));

        if (burned <= 0 || !_stack.TryUse((used, stack), burned))
            return false;

        _battery.ChangeCharge((ent.Owner, battery), burned * joules);
        _audio.PlayPredicted(ent.Comp.RefuelSound, ent, user);

        if (user != null)
        {
            _popup.PopupClient(
                Loc.GetString("modsuit-core-refuelled", ("count", burned), ("fuel", used)),
                ent,
                user);
        }

        // The charge was written straight to the core's own battery, which the suit's
        // power system never sees. Without this the panel keeps showing the old number
        // until something else happens to push a state.
        if (_container.TryGetContainingContainer((ent.Owner, null, null), out var container))
            _power.NotifyChargeChanged(container.Owner);

        return true;
    }
}
