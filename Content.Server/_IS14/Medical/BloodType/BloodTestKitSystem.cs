// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Medical.BloodType;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.Medical.BloodType;

/// <summary>
/// Running a blood group test and putting the card on screen.
/// </summary>
/// <remarks>
/// Barely does any work of its own: finding the sample and laying out the wells both live in
/// <see cref="BloodTypeSystem"/>, shared with the paper strip. What is left here is the
/// analyser's own bargain — four seconds of standing still, and then it names the group for
/// you instead of making you read the card.
/// </remarks>
public sealed class BloodTestKitSystem : EntitySystem
{
    [Dependency] private readonly BloodLabelSystem _labels = default!;
    [Dependency] private readonly BloodTypeSystem _bloodType = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodTestKitComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<BloodTestKitComponent, BloodTestDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<BloodTestKitComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target)
            return;

        args.Handled = _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.Delay,
            new BloodTestDoAfterEvent(),
            ent.Owner,
            target: target,
            used: ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnDoAfter(Entity<BloodTestKitComponent> ent, ref BloodTestDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Target is not { } target)
            return;

        args.Handled = true;

        var state = Test(ent, target);

        _audio.PlayPvs(ent.Comp.Sound, ent.Owner);
        _ui.SetUiState(ent.Owner, BloodTestKitUiKey.Key, state);
        _ui.OpenUi(ent.Owner, BloodTestKitUiKey.Key, args.User);
    }

    /// <summary>
    /// Reads a sample and lays out the card that will be shown for it.
    /// </summary>
    public BloodTestKitUiState Test(Entity<BloodTestKitComponent> ent, EntityUid target)
    {
        var sample = Identity.Name(target, EntityManager);

        if (!_bloodType.TryGetSample(target, out var read))
            return new BloodTestKitUiState(sample, BloodTestOutcome.NoBlood, new(), null, null, Color.White);

        // A container gets the answer written on it, so the next person to pick it up does
        // not have to run the test again — that is how a shelf of labelled bags happens.
        // A mixture with no name clears the label rather than keeping a stale one.
        if (ent.Comp.Labels && !HasComp<BloodstreamComponent>(target))
            _labels.SetLabel(target, read.Type);

        var wells = _bloodType.BuildWells(read);

        if (read.Type is not { } type || !_protos.TryIndex(type, out var proto))
            return new BloodTestKitUiState(sample, BloodTestOutcome.Untyped, wells, null, null, Color.White);

        return new BloodTestKitUiState(
            sample,
            BloodTestOutcome.Typed,
            wells,
            proto.ShortName,
            proto.Name,
            proto.Color ?? Color.White);
    }
}
