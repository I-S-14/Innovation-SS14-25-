// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Diagnostics.CodeAnalysis;
using Content.Goobstation.Common.Footprints;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.Interaction;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
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
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly SharedPuddleSystem _puddle = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    private readonly HashSet<Entity<PuddleComponent>> _puddles = new();
    private readonly HashSet<EntityUid> _onTile = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgAutoWashComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CyborgAutoWashToolComponent, ItemToggleActivateAttemptEvent>(OnToolActivateAttempt);
        SubscribeLocalEvent<CyborgAutoWashToolComponent, ItemToggledEvent>(OnToolToggled);
        // Before the mop logic, which would pour the tank's mess into the container instead.
        SubscribeLocalEvent<CyborgAutoWashToolComponent, AfterInteractEvent>(OnToolAfterInteract,
            before: [typeof(SharedAbsorbentSystem)]);
        SubscribeLocalEvent<CyborgAutoWashComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<CyborgAutoWashComponent, MoveEvent>(OnMove);
    }

    private void OnShutdown(Entity<CyborgAutoWashComponent> ent, ref ComponentShutdown args)
    {
        ent.Comp.Enabled = false;
        ent.Comp.Tool = null;
        _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
        ent.Comp.LoopStream = _audio.Stop(ent.Comp.LoopStream);
    }

    private void OnToolActivateAttempt(Entity<CyborgAutoWashToolComponent> ent,
        ref ItemToggleActivateAttemptEvent args)
    {
        // tg refuses to start with an empty bucket.
        if (HasWater(ent))
            return;

        args.Cancelled = true;
        args.Popup = Loc.GetString("cyborg-autowash-no-water");
    }

    /// <summary>
    /// Clicking a container with water, like a water tank, a sink or a bucket, fills the tank with its water.
    /// Anything else in the container stays there.
    /// </summary>
    private void OnToolAfterInteract(Entity<CyborgAutoWashToolComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || HasComp<PuddleComponent>(target) ||
            !_solution.TryGetDrainableSolution(target, out var sourceSoln, out var source) ||
            !_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out var tankSoln, out var tank))
            return;

        var water = source.GetTotalPrototypeQuantity(_puddle.GetAbsorbentReagents(source));
        if (water <= FixedPoint2.Zero)
            return;

        args.Handled = true;

        var amount = FixedPoint2.Min(water, tank.AvailableVolume);
        if (amount <= FixedPoint2.Zero)
        {
            _popup.PopupClient(Loc.GetString("cyborg-tank-full", ("tool", ent.Owner)), args.User, args.User);
            return;
        }

        var drawn = source.SplitSolutionWithOnly(amount, _puddle.GetAbsorbentReagents(source));
        _solution.UpdateChemicals(sourceSoln.Value);
        _solution.TryAddSolution(tankSoln.Value, drawn);

        _audio.PlayPredicted(ent.Comp.FillSound, target, args.User);
        _popup.PopupClient(Loc.GetString("cyborg-tank-filled", ("amount", drawn.Volume), ("target", target)),
            args.User, args.User);
    }

    private void OnToolToggled(Entity<CyborgAutoWashToolComponent> ent, ref ItemToggledEvent args)
    {
        if (CompOrNull<CyborgToolComponent>(ent)?.Cyborg is not { } cyborg ||
            !TryComp<CyborgAutoWashComponent>(cyborg, out var wash) ||
            wash.Enabled == args.Activated)
            return;

        wash.Enabled = args.Activated;
        wash.Tool = args.Activated ? ent.Owner : null;
        wash.LastTile = null;
        Dirty(cyborg, wash);

        _movementSpeed.RefreshMovementSpeedModifiers(cyborg);
        UpdateSound((cyborg, wash));
        _popup.PopupClient(Loc.GetString(args.Activated ? "cyborg-autowash-on" : "cyborg-autowash-off"), cyborg,
            cyborg);
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

        if (!TryGetTank(ent, out var reservoir))
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

    private bool TryGetTank(Entity<CyborgAutoWashComponent> ent,
        [NotNullWhen(true)] out Entity<SolutionComponent>? tank)
    {
        tank = null;
        return TryComp<CyborgAutoWashToolComponent>(ent.Comp.Tool, out var tool) &&
            _solution.TryGetSolution(ent.Comp.Tool.Value, tool.Solution, out tank, out _);
    }

    private bool HasWater(Entity<CyborgAutoWashToolComponent> tool)
    {
        if (!_solution.TryGetSolution(tool.Owner, tool.Comp.Solution, out _, out var solution))
            return false;

        return solution.GetTotalPrototypeQuantity(_puddle.GetAbsorbentReagents(solution)) > FixedPoint2.Zero;
    }

    /// <summary>
    /// Switches the wand off, which stops washing through <see cref="OnToolToggled"/>.
    /// </summary>
    private void Disable(Entity<CyborgAutoWashComponent> ent, LocId reason)
    {
        if (ent.Comp.Tool is { } tool)
            _toggle.TryDeactivate(tool, predicted: false, showPopup: false);

        // No wand to switch off: stop washing directly.
        if (ent.Comp.Enabled)
        {
            ent.Comp.Enabled = false;
            ent.Comp.Tool = null;
            Dirty(ent);
            _movementSpeed.RefreshMovementSpeedModifiers(ent.Owner);
            UpdateSound(ent);
        }

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
