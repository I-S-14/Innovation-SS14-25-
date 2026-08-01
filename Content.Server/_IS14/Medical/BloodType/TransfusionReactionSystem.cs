// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Medical.BloodType;
using Content.Shared.Administration.Logs;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Database;
using Content.Shared.IdentityManagement;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Medical.BloodType;

/// <summary>
/// Telling the room that a transfusion has gone wrong.
/// </summary>
/// <remarks>
/// The harm itself is the hemolysate sitting in the patient's veins and is handled entirely
/// by that reagent's metabolism — there is no damage code here on purpose, so tuning a
/// transfusion reaction is a YAML change. What is here is the part a player needs: a visible
/// sign that the bag currently hanging is the wrong one, in time to take it down.
/// </remarks>
public sealed class TransfusionReactionSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodstreamComponent, BloodTransfusedEvent>(OnTransfused);
        SubscribeLocalEvent<BloodTypeComponent, BloodSensitizedEvent>(OnSensitized);
    }

    private void OnTransfused(Entity<BloodstreamComponent> ent, ref BloodTransfusedEvent args)
    {
        if (args.Rejected.Volume <= 0)
            return;

        _adminLogger.Add(
            LogType.ForceFeed,
            LogImpact.Medium,
            $"{ToPrettyString(args.Recipient):target} rejected " +
            $"{SharedSolutionContainerSystem.ToPrettyString(args.Rejected):solution}" +
            $" from {ToPrettyString(args.Source):source}");

        var comp = EnsureComp<TransfusionReactionComponent>(args.Recipient);
        var now = _timing.CurTime;

        if (now < comp.NextWarning)
            return;

        comp.NextWarning = now + comp.WarningInterval;

        _popup.PopupEntity(
            Loc.GetString(
                "is14-blood-transfusion-rejected",
                ("target", Identity.Entity(args.Recipient, EntityManager))),
            args.Recipient,
            PopupType.MediumCaution);
    }

    /// <summary>
    /// Sensitisation is deliberately silent. Nobody in the fiction can feel their immune
    /// system filing a grudge, and the whole weight of the mechanic is that the bill arrives
    /// later, attached to a transfusion that looked identical to one that worked.
    /// </summary>
    private void OnSensitized(Entity<BloodTypeComponent> ent, ref BloodSensitizedEvent args)
    {
        _adminLogger.Add(
            LogType.ForceFeed,
            LogImpact.Low,
            $"{ToPrettyString(args.Recipient):target} was sensitized to blood antigen {args.Antigen.Id}");
    }
}
