// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Research;

/// <summary>
/// Resolves the IS14 side of a technology: its price in the five currencies and its effects.
/// Shared, so the console can grey out cards and list effects without asking the server.
/// </summary>
public sealed class IS14ResearchCostSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    /// <summary>Upstream prices are built for a passive drip; ours are a hundredth of that scale.</summary>
    public const int FallbackDivisor = 100;

    public const int FallbackMinimum = 5;

    /// <summary>
    /// Ceiling on everything that cuts a price together — samples taken apart plus a Gosplan
    /// priority. Research gets cheap, never free.
    /// </summary>
    public const float MaxDiscount = 0.65f;

    /// <summary>Currency an untabulated upstream technology is charged in.</summary>
    public static readonly ProtoId<ResearchPointTypePrototype> FallbackPointType = "Science";

    private readonly Dictionary<string, IS14TechDataPrototype> _data = new();
    private readonly Dictionary<string, List<string>> _exclusive = new();
    private List<ResearchPointTypePrototype>? _sortedTypes;
    private bool _dirty = true;

    public override void Initialize()
    {
        base.Initialize();
        _proto.PrototypesReloaded += OnPrototypesReloaded;
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _proto.PrototypesReloaded -= OnPrototypesReloaded;
    }

    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (args.WasModified<IS14TechDataPrototype>() || args.WasModified<ResearchPointTypePrototype>())
        {
            _dirty = true;
            _sortedTypes = null;
        }
    }

    private void Rebuild()
    {
        _data.Clear();
        _exclusive.Clear();

        foreach (var entry in _proto.EnumeratePrototypes<IS14TechDataPrototype>())
        {
            _data[entry.ID] = entry;

            if (entry.ExclusiveGroup is not { } group)
                continue;

            if (!_exclusive.TryGetValue(group, out var members))
            {
                members = new List<string>();
                _exclusive[group] = members;
            }

            members.Add(entry.ID);
        }

        _dirty = false;
    }

    /// <summary>The IS14 data for a technology, or null when it only has upstream fields.</summary>
    public IS14TechDataPrototype? GetData(string technologyId)
    {
        if (_dirty)
            Rebuild();

        return !string.IsNullOrEmpty(technologyId) && _data.TryGetValue(technologyId, out var data)
            ? data
            : null;
    }

    /// <summary>
    /// Price of a technology. Never empty: technologies with no table entry fall back to their
    /// upstream cost, rescaled and charged as scientific data.
    /// </summary>
    public Dictionary<ProtoId<ResearchPointTypePrototype>, int> GetCost(TechnologyPrototype tech)
    {
        var data = GetData(tech.ID);

        if (data is { Costs.Count: > 0 })
            return data.Costs;

        return new Dictionary<ProtoId<ResearchPointTypePrototype>, int>
        {
            [FallbackPointType] = Math.Max(FallbackMinimum, tech.Cost / FallbackDivisor),
        };
    }

    /// <summary>The sample this technology has to be paid for with, if it is a breakthrough.</summary>
    public IS14BreakthroughRequirement? GetBreakthrough(string technologyId)
    {
        return GetData(technologyId)?.Breakthrough;
    }

    /// <summary>
    /// The other technologies this one rules out. Empty for everything that is not a fork.
    /// </summary>
    public IReadOnlyList<string> GetExclusiveSiblings(string technologyId)
    {
        if (GetData(technologyId)?.ExclusiveGroup is not { } group
            || !_exclusive.TryGetValue(group, out var members))
        {
            return Array.Empty<string>();
        }

        var result = new List<string>(members.Count - 1);

        foreach (var member in members)
        {
            if (member != technologyId)
                result.Add(member);
        }

        return result;
    }

    /// <summary>Everything the technology changes besides recipes.</summary>
    public List<IS14TechEffect> GetEffects(TechnologyPrototype tech)
    {
        return GetData(tech.ID)?.Effects ?? new List<IS14TechEffect>();
    }

    /// <summary>
    /// Price after reverse-engineering credit. Rounded up, and never below one unit, so a pile
    /// of samples makes research cheap but never free.
    /// </summary>
    public static Dictionary<ProtoId<ResearchPointTypePrototype>, int> Discounted(
        Dictionary<ProtoId<ResearchPointTypePrototype>, int> costs,
        float discount)
    {
        if (discount <= 0f)
            return costs;

        var result = new Dictionary<ProtoId<ResearchPointTypePrototype>, int>(costs.Count);

        foreach (var (type, amount) in costs)
        {
            result[type] = Math.Max(1, (int) MathF.Ceiling(amount * (1f - discount)));
        }

        return result;
    }

    /// <summary>Point types in the order the console shows them.</summary>
    public List<ResearchPointTypePrototype> SortedTypes()
    {
        return _sortedTypes ??= _proto.EnumeratePrototypes<ResearchPointTypePrototype>()
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.ID)
            .ToList();
    }
}
