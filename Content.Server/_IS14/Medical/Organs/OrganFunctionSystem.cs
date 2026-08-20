// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Medical.Organs;
using Content.Shared._Shitmed.Body.Organ;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared.Body.Events;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.Medical.Organs;

/// <summary>
/// Keeps every body's faculty levels in step with the organs it actually has.
/// </summary>
/// <remarks>
/// Event-driven on purpose. A station's worth of humanoids each carrying nine faculties is a
/// few thousand pointless divisions a second if this ticks, and organs change state perhaps
/// once a round for most people. Nothing here polls anything.
/// </remarks>
public sealed class OrganFunctionSystem : SharedOrganFunctionSystem
{
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly SharedBodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<OrganComponent, ComponentStartup>(OnOrganStartup);

        SubscribeLocalEvent<IS14OrganFunctionComponent, OrganIntegrityChangedEvent>(OnIntegrityChanged);
        SubscribeLocalEvent<IS14OrganFunctionComponent, OrganAddedToBodyEvent>(OnAddedToBody);
        SubscribeLocalEvent<IS14OrganFunctionComponent, OrganRemovedFromBodyEvent>(OnRemovedFromBody);
        SubscribeLocalEvent<IS14OrganFunctionComponent, OrganEnabledEvent>(OnEnableChanged);
        SubscribeLocalEvent<IS14OrganFunctionComponent, OrganDisabledEvent>(OnDisableChanged);

        SubscribeLocalEvent<IS14FacultiesComponent, MapInitEvent>(OnBodyMapInit);
    }

    /// <summary>
    /// Gives an organ its function from the prototype named after its slot.
    /// </summary>
    /// <remarks>
    /// Only when it has none of its own: an organ prototype that declares the component is
    /// saying it is not an ordinary one, and a cybernetic heart should not be quietly reset to
    /// the flesh-and-blood defaults.
    /// </remarks>
    /// <remarks>
    /// On startup rather than on map init because the body system already owns
    /// <c>MapInitEvent</c> for organs, and Robust allows exactly one subscriber per component
    /// and event. Startup is earlier anyway, which means an organ has its function before
    /// anything has had the chance to ask about it.
    /// </remarks>
    private void OnOrganStartup(Entity<OrganComponent> ent, ref ComponentStartup args)
    {
        if (HasComp<IS14OrganFunctionComponent>(ent)
            || ent.Comp.SlotId.Length == 0
            || !_protos.TryIndex<IS14OrganFunctionPrototype>(ent.Comp.SlotId, out var proto))
        {
            return;
        }

        var function = AddComp<IS14OrganFunctionComponent>(ent);

        function.Contributions = new Dictionary<string, float>(proto.Contributions);
        function.Reserve = proto.Reserve;
        function.Floor = proto.Floor;
        function.PerfusionSensitivity = proto.PerfusionSensitivity;
        function.InjuryCap = proto.InjuryCap;

        Dirty(ent.Owner, function);

        if (ent.Comp.Body is { } body)
            Recompute(body);
    }

    private void OnBodyMapInit(Entity<IS14FacultiesComponent> ent, ref MapInitEvent args) => Recompute(ent);

    private void OnIntegrityChanged(Entity<IS14OrganFunctionComponent> ent, ref OrganIntegrityChangedEvent args) =>
        RecomputeOwner(ent);

    private void OnAddedToBody(Entity<IS14OrganFunctionComponent> ent, ref OrganAddedToBodyEvent args) =>
        Recompute(args.Body);

    private void OnRemovedFromBody(Entity<IS14OrganFunctionComponent> ent, ref OrganRemovedFromBodyEvent args) =>
        Recompute(args.OldBody);

    private void OnEnableChanged(Entity<IS14OrganFunctionComponent> ent, ref OrganEnabledEvent args) =>
        RecomputeOwner(ent);

    private void OnDisableChanged(Entity<IS14OrganFunctionComponent> ent, ref OrganDisabledEvent args) =>
        RecomputeOwner(ent);

    private void RecomputeOwner(EntityUid organ)
    {
        if (TryComp<OrganComponent>(organ, out var comp) && comp.Body is { } body)
            Recompute(body);
    }

    /// <summary>Re-adds every organ's contribution and tells the network only if it moved.</summary>
    public void Recompute(EntityUid body)
    {
        if (TerminatingOrDeleted(body) || !TryComp<IS14FacultiesComponent>(body, out var faculties))
            return;

        // Everything the body has ever had stays in the table at zero, so an organ that has
        // been torn out reads as absent function rather than as an absent question.
        var levels = new Dictionary<string, float>();

        foreach (var key in faculties.Levels.Keys)
        {
            levels[key] = 0f;
        }

        foreach (var (uid, organ) in _body.GetBodyOrgans(body))
        {
            if (!TryComp<IS14OrganFunctionComponent>(uid, out var function))
                continue;

            var efficiency = GetEfficiency(function, organ);

            foreach (var (faculty, weight) in function.Contributions)
            {
                levels[faculty] = levels.GetValueOrDefault(faculty) + efficiency * weight;
            }
        }

        // Capped at one: a body with spare capacity is a healthy body, not a superhuman one.
        // The spare is what covers for the organ it is about to lose.
        var changed = levels.Count != faculties.Levels.Count;

        foreach (var faculty in levels.Keys)
        {
            var level = Math.Clamp(levels[faculty], 0f, 1f);
            levels[faculty] = level;

            if (!faculties.Levels.TryGetValue(faculty, out var old) || Math.Abs(old - level) > 0.001f)
                changed = true;
        }

        if (!changed)
            return;

        faculties.Levels = levels;
        Dirty(body, faculties);
    }
}
