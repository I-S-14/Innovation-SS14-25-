// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Body.Systems;
using Content.Shared._IS14.Medical.BloodType;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Forensics;
using Content.Shared.Forensics.Components;

namespace Content.Server._IS14.Medical.BloodType;

/// <summary>
/// Writes a mob's blood group onto the blood in its veins.
/// </summary>
/// <remarks>
/// Nothing reads a group off a mob at transfusion time — it is read off the reagent, the
/// same way DNA is. That keeps a drawn bag meaningful after the donor has left the room, or
/// died, or been replaced by something wearing his face. The cost is that the stamp has to
/// be kept on the blood, which is all this system does.
/// </remarks>
public sealed class BloodTypeStampSystem : EntitySystem
{
    [Dependency] private readonly BloodLabelSystem _labels = default!;
    [Dependency] private readonly BloodTypeSystem _bloodType = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Both of these hang off components upstream has not already claimed for the event:
        // the engine permits exactly one subscription per component-event pair, and
        // BloodstreamComponent's MapInit and GenerateDna are both spoken for.
        SubscribeLocalEvent<BloodstreamComponent, ComponentStartup>(OnBloodstreamStartup);
        SubscribeLocalEvent<PresetBloodTypeComponent, MapInitEvent>(OnPresetMapInit);

        // Upstream rewrites the reference solution's reagent data wholesale when DNA changes,
        // which drops our stamp on the floor. Running after it puts the stamp back.
        SubscribeLocalEvent<DnaComponent, GenerateDnaEvent>(
            OnDnaGenerated,
            after: new[] { typeof(BloodstreamSystem) });
    }

    private void OnBloodstreamStartup(Entity<BloodstreamComponent> ent, ref ComponentStartup args)
    {
        // Every mob with blood gets the component, even though its group is derived rather
        // than stored. It is what carries sensitisation, it is where the group shows up in
        // view-variables, and it gives the metabolism hook something of ours to hang off.
        EnsureComp<BloodTypeComponent>(ent);

        Restamp(ent);
    }

    private void OnPresetMapInit(Entity<PresetBloodTypeComponent> ent, ref MapInitEvent args)
    {
        if (!_solutions.TryGetSolution(ent.Owner, ent.Comp.Solution, out var soln, out var solution))
            return;

        _bloodType.StampBloodType(solution, ent.Comp.Type);
        _solutions.UpdateChemicals(soln.Value);

        if (ent.Comp.Label)
            _labels.SetLabel(ent.Owner, ent.Comp.Type);
    }

    private void OnDnaGenerated(Entity<DnaComponent> ent, ref GenerateDnaEvent args)
    {
        // A scrambled genome is a different person, and the roll keys on DNA, so the group
        // that comes back out here is genuinely a new one.
        if (TryComp<BloodstreamComponent>(ent, out var bloodstream))
            Restamp((ent.Owner, bloodstream));
    }

    /// <summary>
    /// Stamps the group onto both the reference solution — the mould every drop of naturally
    /// regenerated blood is cast from — and whatever is already in the veins.
    /// </summary>
    public void Restamp(Entity<BloodstreamComponent> ent)
    {
        if (_bloodType.GetBloodType(ent.Owner) is not { } type)
            return;

        _bloodType.StampBloodType(ent.Comp.BloodReferenceSolution, type);

        if (_solutions.TryGetSolution(ent.Owner, ent.Comp.BloodSolutionName, out var soln, out var blood))
        {
            _bloodType.StampBloodType(blood, type);
            _solutions.UpdateChemicals(soln.Value);
        }
    }
}
