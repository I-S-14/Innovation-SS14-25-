// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._IS14.Cord;
using Content.Shared._IS14.Medical.BloodType;
using Content.Shared._IS14.Medical.IvDrip;
using Content.Shared.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Database;
using Content.Shared.Popups;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Medical.IvDrip;

/// <summary>
/// The half of a drip stand that actually moves fluid, and the sprite work that follows
/// from it. Runs on a clock rather than on an interaction: the whole point of a drip is
/// that it keeps going after the doctor has walked off.
/// </summary>
public sealed class IvDripSystem : SharedIvDripSystem
{
    [Dependency] private readonly BloodTypeSystem _bloodType = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly ReactiveSystem _reactive = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedCordSystem _cord = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IvPackComponent, SolutionContainerChangedEvent>(OnPackSolutionChanged);
        SubscribeLocalEvent<IvDripComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<IvDripComponent> ent, ref MapInitEvent args)
    {
        UpdateAppearance(ent);
    }

    /// <summary>
    /// A bag filled or emptied by hand while it hangs has to redraw the stand it is
    /// hanging on, which nothing else would tell the stand about.
    /// </summary>
    private void OnPackSolutionChanged(Entity<IvPackComponent> ent, ref SolutionContainerChangedEvent args)
    {
        if (!_container.TryGetContainingContainer((ent.Owner, null, null), out var container)
            || !TryComp<IvDripComponent>(container.Owner, out var drip)
            || container.ID != drip.SlotId)
        {
            return;
        }

        UpdateAppearance((container.Owner, drip));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<IvDripComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Patient is not { } patient)
                continue;

            Entity<IvDripComponent> ent = (uid, comp);

            // The line is not a leash — nothing drags the patient back. Walking out from
            // under it takes the needle with you, which is the cost of standing up.
            if (TerminatingOrDeleted(patient) || OutOfRange(ent, patient))
            {
                if (!TerminatingOrDeleted(patient))
                    _popup.PopupEntity(Loc.GetString("is14-iv-drip-torn-out"), patient, PopupType.SmallCaution);

                Detach(ent);
                continue;
            }

            if (now < comp.NextTransfer)
                continue;

            comp.NextTransfer = now + comp.TransferInterval;

            SetFlowing(ent, Transfer(ent, patient));
        }
    }

    /// <summary>
    /// One tick of fluid, in whichever direction the stand is set to. Returns whether
    /// anything actually moved — a stand with a dry bag or a full one keeps its needle in
    /// but stops running, so the sprite can say so.
    /// </summary>
    private bool Transfer(Entity<IvDripComponent> ent, EntityUid patient)
    {
        if (GetPack(ent) is not { } pack
            || !TryComp<IvPackComponent>(pack, out var packComp)
            || !_solutions.TryGetSolution(pack, packComp.SolutionName, out var soln, out var contents))
        {
            return false;
        }

        return ent.Comp.Mode == IvDripMode.Draw
            ? Draw(ent, patient, soln.Value, contents)
            : Inject(ent, patient, soln.Value, contents);
    }

    /// <summary>Bag into bloodstream, the same way a syringe would do it.</summary>
    private bool Inject(
        Entity<IvDripComponent> ent,
        EntityUid patient,
        Entity<SolutionComponent> packSolution,
        Solution pack)
    {
        if (pack.Volume <= 0
            || !_solutions.TryGetInjectableSolution(patient, out var target, out var targetSolution))
        {
            return false;
        }

        var amount = FixedPoint2.Min(ent.Comp.TransferAmount, pack.Volume, targetSolution.AvailableVolume);

        if (amount <= 0)
            return false;

        var removed = _solutions.SplitSolution(packSolution, amount);

        // Groups are checked here rather than once at the needle: a doctor can swap the bag
        // halfway through, and the tick that follows has to be judged on what is hanging now.
        _bloodType.PrepareTransfusion(patient, removed, ent.Owner);

        _reactive.DoEntityReaction(patient, removed, ReactionMethod.Injection);
        _solutions.Inject(patient, target.Value, removed);

        _adminLogger.Add(
            LogType.ForceFeed,
            $"{ToPrettyString(ent):drip} injected {ToPrettyString(patient):target} with " +
            $"{SharedSolutionContainerSystem.ToPrettyString(removed):solution}");

        return true;
    }

    /// <summary>Bloodstream into bag. Blood only — a drip is not a dialysis machine.</summary>
    private bool Draw(
        Entity<IvDripComponent> ent,
        EntityUid patient,
        Entity<SolutionComponent> packSolution,
        Solution pack)
    {
        if (!TryComp<BloodstreamComponent>(patient, out var bloodstream)
            || !_solutions.ResolveSolution(patient, bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var blood))
        {
            return false;
        }

        var amount = FixedPoint2.Min(ent.Comp.TransferAmount, blood.Volume, pack.AvailableVolume);

        if (amount <= 0)
            return false;

        var drawn = _solutions.SplitSolution(bloodstream.BloodSolution.Value, amount);

        if (!_solutions.TryAddSolution(packSolution, drawn))
            return false;

        // A bag fills up blank. It used to fill up labelled, which meant the group cost a
        // needle and nothing else — no test, no doctor, no way to be wrong. Whoever draws the
        // blood knows whose arm it came out of; writing that down is their job and their word.
        _adminLogger.Add(
            LogType.ForceFeed,
            $"{ToPrettyString(ent):drip} drew {amount} units of blood from {ToPrettyString(patient):target}");

        return true;
    }

    private void SetFlowing(Entity<IvDripComponent> ent, bool flowing)
    {
        if (ent.Comp.Flowing == flowing)
            return;

        ent.Comp.Flowing = flowing;
        Dirty(ent);

        UpdateAppearance(ent);
    }

    /// <summary>
    /// Everything the stand shows: which pose the pole is in, whether a bag is hanging,
    /// how full it is, and what colour it and the line are. Buckets the fill here so the
    /// client never has to hold an opinion about what a nearly-empty bag looks like.
    /// </summary>
    protected override void UpdateAppearance(Entity<IvDripComponent> ent)
    {
        if (TerminatingOrDeleted(ent) || !TryComp<AppearanceComponent>(ent, out var appearance))
            return;

        var state = (ent.Comp.Patient, ent.Comp.Mode, ent.Comp.Flowing) switch
        {
            (null, _, _) => IvDripVisualState.Idle,
            (_, IvDripMode.Draw, true) => IvDripVisualState.Drawing,
            (_, IvDripMode.Draw, false) => IvDripVisualState.DrawIdle,
            (_, _, true) => IvDripVisualState.Injecting,
            _ => IvDripVisualState.InjectIdle,
        };

        _appearance.SetData(ent, IvDripVisuals.State, state, appearance);

        var solution = GetPackSolution(ent);

        _appearance.SetData(ent, IvDripVisuals.HasPack, solution != null, appearance);

        var color = solution != null && solution.Volume > 0
            ? solution.GetColor(_prototypes)
            : ent.Comp.EmptyLineColor;

        _appearance.SetData(ent, IvDripVisuals.FillColor, color, appearance);
        _appearance.SetData(ent, IvDripVisuals.FillLevel, GetFillLevel(ent, solution), appearance);

        // The line takes the colour of what is going down it, which is the only readout a
        // doctor gets from across the room: red is a transfusion, anything else is a drug.
        _cord.SetColor(ent.Owner, color);
    }

    /// <summary>Which step of the bag gauge a fill fraction lands on.</summary>
    private static int GetFillLevel(Entity<IvDripComponent> ent, Solution? solution)
    {
        if (solution == null)
            return 0;

        var fraction = solution.FillFraction;
        var level = 0;

        for (var i = 0; i < ent.Comp.FillThresholds.Count; i++)
        {
            if (fraction >= ent.Comp.FillThresholds[i])
                level = i;
        }

        return level;
    }
}
