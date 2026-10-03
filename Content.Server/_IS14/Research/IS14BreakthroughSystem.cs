// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Research;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.Research;

/// <summary>
/// Keeps track of which breakthroughs a station has paid for, and answers the one question the
/// destructive analyzer asks: "is this thing in my tube the key to anything?"
/// </summary>
public sealed class IS14BreakthroughSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    /// <summary>Whether this station may research a breakthrough technology at all.</summary>
    public bool IsComplete(EntityUid? station, string technologyId)
    {
        return station != null
               && TryComp<IS14BreakthroughComponent>(station, out var comp)
               && comp.Completed.Contains(technologyId);
    }

    /// <summary>Records a breakthrough. Irreversible on purpose: the sample is gone.</summary>
    public void Complete(EntityUid station, string technologyId)
    {
        EnsureComp<IS14BreakthroughComponent>(station).Completed.Add(technologyId);
    }

    /// <summary>
    /// Finds the technology this sample would open for the station: a breakthrough it has not
    /// paid for yet and whose requirement the object satisfies.
    /// </summary>
    public bool TryMatchSample(
        EntityUid? station,
        string? samplePrototype,
        out string technologyId,
        out IS14BreakthroughRequirement requirement)
    {
        technologyId = string.Empty;
        requirement = default!;

        if (string.IsNullOrEmpty(samplePrototype))
            return false;

        foreach (var data in _proto.EnumeratePrototypes<IS14TechDataPrototype>())
        {
            if (data.Breakthrough is not { } breakthrough || IsComplete(station, data.ID))
                continue;

            if (!IS14PrototypeKin.MatchesAny(_proto, samplePrototype, breakthrough.Samples))
                continue;

            technologyId = data.ID;
            requirement = breakthrough;
            return true;
        }

        return false;
    }

    /// <summary>Breakthroughs already paid for, for the console state.</summary>
    public List<string> Completed(EntityUid? station)
    {
        return station != null && TryComp<IS14BreakthroughComponent>(station, out var comp)
            ? new List<string>(comp.Completed)
            : new List<string>();
    }

    /// <summary>Technologies this station still owes a sample for. Used by tests and admins.</summary>
    public List<string> Outstanding(EntityUid? station)
    {
        var result = new List<string>();

        foreach (var data in _proto.EnumeratePrototypes<IS14TechDataPrototype>())
        {
            if (data.Breakthrough != null && !IsComplete(station, data.ID))
                result.Add(data.ID);
        }

        return result;
    }
}
