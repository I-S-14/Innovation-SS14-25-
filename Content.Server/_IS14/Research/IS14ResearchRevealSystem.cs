// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Station.Systems;
using Content.Shared._IS14.CCVar;
using Content.Shared._IS14.Research;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._IS14.Research;

/// <summary>
/// Decides which hidden technologies exist this round. At shift start every upgrade group rolls
/// one of its mutually exclusive variants, so the tree is not identical two rounds running.
/// </summary>
public sealed class IS14ResearchRevealSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StationInitializedEvent>(OnStationInitialized);
    }

    private void OnStationInitialized(StationInitializedEvent args)
    {
        RollUpgrades(args.Station);
        RollPriorities(args.Station);
    }

    /// <summary>Rolls every upgrade group for a station. Safe to call again for admin re-rolls.</summary>
    public void RollUpgrades(EntityUid station)
    {
        var revealed = EnsureComp<IS14RevealedTechComponent>(station);

        foreach (var group in _proto.EnumeratePrototypes<IS14ResearchUpgradeGroupPrototype>())
        {
            foreach (var variant in group.Variants)
            {
                revealed.Revealed.Remove(variant.Id);
            }

            if (group.Variants.Count == 0 || !_random.Prob(group.Chance))
                continue;

            var picked = _random.Pick(group.Variants);
            revealed.Revealed.Add(picked.Id);

            Log.Debug($"Research upgrade group {group.ID} rolled {picked.Id} for {ToPrettyString(station)}.");
        }
    }

    /// <summary>
    /// Declares a handful of topics the priority of the shift. Nothing structural moves — only
    /// the price — so a planned route to a technology still works, it is just cheaper somewhere
    /// else than it was last round.
    /// </summary>
    public void RollPriorities(EntityUid station)
    {
        var count = _cfg.GetCVar(IS14CVars.ResearchPriorityCount);
        var discount = _cfg.GetCVar(IS14CVars.ResearchPriorityDiscount);

        var comp = EnsureComp<IS14ResearchPrioritiesComponent>(station);
        comp.Priorities.Clear();

        if (count <= 0 || discount <= 0f)
            return;

        // Only our own tree, and never the cheap first tier: a discount on a 15-point topic is
        // not a decision. Hidden variants are out too — they are already a per-round roll.
        var pool = _proto.EnumeratePrototypes<TechnologyPrototype>()
            .Where(tech => !tech.Hidden
                           && tech.Tier > 1
                           && tech.Discipline.Id.StartsWith("IS14"))
            .Select(tech => tech.ID)
            .OrderBy(id => id)
            .ToList();

        for (var i = 0; i < count && pool.Count > 0; i++)
        {
            var index = _random.Next(pool.Count);
            comp.Priorities[pool[index]] = discount;
            pool.RemoveAt(index);
        }
    }

    /// <summary>Price cut this station has on a topic because it is a priority this shift.</summary>
    public float GetPriority(EntityUid? station, string technologyId)
    {
        return station != null
               && TryComp<IS14ResearchPrioritiesComponent>(station, out var comp)
               && comp.Priorities.TryGetValue(technologyId, out var discount)
            ? discount
            : 0f;
    }

    /// <summary>Whether a hidden technology exists for this station at all.</summary>
    public bool IsRevealed(EntityUid? station, string technologyId)
    {
        return station != null
               && TryComp<IS14RevealedTechComponent>(station, out var revealed)
               && revealed.Revealed.Contains(technologyId);
    }

    /// <summary>Makes a hidden technology available — used by reverse engineering later on.</summary>
    public void Reveal(EntityUid station, string technologyId)
    {
        EnsureComp<IS14RevealedTechComponent>(station).Revealed.Add(technologyId);
    }
}
