// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Diagnostics.CodeAnalysis;
using Content.Goobstation.Common.Footprints;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Fluids;
using Content.Shared.Actions;
using Content.Shared.Fluids.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Robust.Shared.Map.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._IS14.Cyborgs.Systems;

public sealed class CyborgAutoWashSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedActionsSystem _actions = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedCyborgSystem _cyborg = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPuddleSystem _puddle = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    private readonly HashSet<Entity<PuddleComponent>> _puddles = new();
    private readonly HashSet<EntityUid> _onTile = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgAutoWashComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CyborgAutoWashComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CyborgAutoWashComponent, CyborgToggleAutoWashEvent>(OnToggle);
        SubscribeLocalEvent<CyborgAutoWashComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<CyborgAutoWashComponent, MoveEvent>(OnMove);
    }

    // The component comes and goes with the janitor model, long after map init, so the action
    // is granted on startup. The server owns it, the client gets it networked.
    private void OnStartup(Entity<CyborgAutoWashComponent> ent, ref ComponentStartup args)
    {
        if (_net.IsClient)
            return;

        _actions.AddAction(ent.Owner, ref ent.Comp.ActionEntity, ent.Comp.Action);
        Dirty(ent);
    }

    private void OnShutdown(Entity<CyborgAutoWashComponent> ent, ref ComponentShutdown args)
    {
        if (_net.IsServer)
            _actions.RemoveAction(ent.Owner, ent.Comp.ActionEntity);

        ent.Comp.Enabled = false;
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
        ent.Comp.LoopStream = _audio.Stop(ent.Comp.LoopStream);
    }

    private void OnToggle(Entity<CyborgAutoWashComponent> ent, ref CyborgToggleAutoWashEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        // tg refuses to start with an empty bucket.
        if (!ent.Comp.Enabled && !HasWater(ent))
        {
            _popup.PopupClient(Loc.GetString("cyborg-autowash-no-water"), ent, ent);
            return;
        }

        ent.Comp.Enabled = !ent.Comp.Enabled;
        ent.Comp.LastTile = null;
        Dirty(ent);

        _actions.SetToggled(ent.Comp.ActionEntity, ent.Comp.Enabled);
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
        UpdateSound(ent);
        _popup.PopupClient(Loc.GetString(ent.Comp.Enabled ? "cyborg-autowash-on" : "cyborg-autowash-off"), ent, ent);
    }

    private void OnRefreshSpeed(Entity<CyborgAutoWashComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Enabled)
            args.ModifySpeed(ent.Comp.SpeedModifier, ent.Comp.SpeedModifier);
    }

    private void OnMove(Entity<CyborgAutoWashComponent> ent, ref MoveEvent args)
    {
        // Solutions of puddles are server authoritative.
        if (_net.IsClient || !ent.Comp.Enabled)
            return;

        // Deleting the cyborg moves it to nullspace, there is nothing to wash there.
        if (!args.NewPosition.EntityId.Valid || TerminatingOrDeleted(ent))
            return;

        var tile = args.NewPosition.WithPosition(args.NewPosition.Position.Floored());
        if (ent.Comp.LastTile == tile)
            return;

        ent.Comp.LastTile = tile;
        var coords = _transform.ToMapCoordinates(args.NewPosition);

        // Footprints are their own entities, the mop cleans them the same way.
        _onTile.Clear();
        _lookup.GetEntitiesInRange(coords.MapId, coords.Position, 0.45f, _onTile, LookupFlags.Uncontained);
        foreach (var uid in _onTile)
        {
            RaiseLocalEvent(uid, new FootprintCleanEvent());
        }

        _puddles.Clear();
        _lookup.GetEntitiesInRange(coords, 0.45f, _puddles);
        if (_puddles.Count == 0)
            return;

        if (!TryGetReservoir(ent, out var reservoir))
        {
            Disable(ent, "cyborg-autowash-no-tank");
            return;
        }

        foreach (var puddle in _puddles)
        {
            if (!Wash(ent, reservoir.Value, puddle))
                return;
        }
    }

    /// <summary>
    /// The mop's swap: water from the tank goes into the puddle, as much of the mess goes into the tank.
    /// </summary>
    private bool Wash(Entity<CyborgAutoWashComponent> ent, Entity<SolutionComponent> reservoir,
        Entity<PuddleComponent> puddle)
    {
        if (!_solution.ResolveSolution(puddle.Owner, puddle.Comp.SolutionName, ref puddle.Comp.Solution,
            out var puddleSolution) ||
            puddleSolution.Volume <= 0)
            return true;

        // Already just water, nothing to swap.
        var puddleWater = puddleSolution.GetTotalPrototypeQuantity(_puddle.GetAbsorbentReagents(puddleSolution));
        if (puddleWater == puddleSolution.Volume)
            return true;

        var tankSolution = reservoir.Comp.Solution;
        var water = tankSolution.GetTotalPrototypeQuantity(_puddle.GetAbsorbentReagents(tankSolution));
        if (water <= FixedPoint2.Zero)
        {
            Disable(ent, "cyborg-autowash-no-water");
            return false;
        }

        if (!_powerCell.TryUseCharge(ent.Owner, ent.Comp.ChargePerTile))
        {
            Disable(ent, "cyborg-tools-no-power");
            return false;
        }

        var amount = FixedPoint2.Min(ent.Comp.PickupAmount, water);
        var dirt = puddleSolution.SplitSolutionWithout(amount, _puddle.GetAbsorbentReagents(puddleSolution));
        var clean = tankSolution.SplitSolutionWithOnly(dirt.Volume, _puddle.GetAbsorbentReagents(tankSolution));

        var xform = Transform(puddle);
        if (TryComp<MapGridComponent>(xform.GridUid, out var grid))
            _puddle.DoTileReactions(_map.GetTileRef(xform.GridUid.Value, grid, xform.Coordinates), clean);

        _solution.AddSolution(puddle.Comp.Solution.Value, clean);
        _solution.AddSolution(reservoir, dirt);
        return true;
    }

    /// <summary>
    /// The tank is the janitor kit's internal tank, wherever it is: stored or held.
    /// </summary>
    private bool TryGetReservoir(Entity<CyborgAutoWashComponent> ent,
        [NotNullWhen(true)] out Entity<SolutionComponent>? reservoir)
    {
        reservoir = null;
        if (!TryComp<CyborgComponent>(ent, out var cyborg))
            return false;

        foreach (var tool in _cyborg.EnumerateTools((ent.Owner, cyborg)))
        {
            if (Prototype(tool)?.ID != ent.Comp.ReservoirTool.Id)
                continue;

            if (_solution.TryGetSolution(tool, ent.Comp.ReservoirSolution, out reservoir, out _))
                return true;
        }

        return false;
    }

    private bool HasWater(Entity<CyborgAutoWashComponent> ent)
    {
        if (!TryGetReservoir(ent, out var reservoir))
            return false;

        var solution = reservoir.Value.Comp.Solution;
        return solution.GetTotalPrototypeQuantity(_puddle.GetAbsorbentReagents(solution)) > FixedPoint2.Zero;
    }

    private void Disable(Entity<CyborgAutoWashComponent> ent, LocId reason)
    {
        ent.Comp.Enabled = false;
        Dirty(ent);
        _actions.SetToggled(ent.Comp.ActionEntity, false);
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
        UpdateSound(ent);
        _popup.PopupEntity(Loc.GetString(reason), ent, ent);
    }

    // Server only: the looping stream is an entity everyone nearby hears.
    private void UpdateSound(Entity<CyborgAutoWashComponent> ent)
    {
        if (_net.IsClient)
            return;

        if (!ent.Comp.Enabled)
        {
            if (ent.Comp.LoopStream == null)
                return;

            ent.Comp.LoopStream = _audio.Stop(ent.Comp.LoopStream);
            _audio.PlayPvs(ent.Comp.EndSound, ent);
            return;
        }

        _audio.PlayPvs(ent.Comp.StartSound, ent);
        ent.Comp.LoopStream = _audio.PlayPvs(ent.Comp.LoopSound, ent,
            AudioParams.Default.WithLoop(true).WithVolume(-4f))?.Entity;
    }
}
