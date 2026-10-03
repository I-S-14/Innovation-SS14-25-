// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._IS14.Research;

/// <summary>
/// Everything the station has already measured this round, kept per station.
/// This is the novelty rule from the design doc: we pay for new information, not for
/// repeating an action, so every measurement is looked up here first.
/// </summary>
[RegisterComponent]
public sealed partial class IS14ResearchArchiveComponent : Component
{
    /// <summary>Profile key to how many times it has been measured.</summary>
    [DataField]
    public Dictionary<string, int> Measurements = new();
}

/// <summary>
/// How much a measurement pays once the archive has seen its profile before.
/// </summary>
public static class IS14Novelty
{
    public static float Multiplier(int priorCount) => priorCount switch
    {
        0 => 1f,
        1 => 0.3f,
        2 => 0.1f,
        _ => 0.03f,
    };

    /// <summary>Loc id describing the result, shown in the popup.</summary>
    public static string Label(int priorCount) => priorCount switch
    {
        0 => "is14-research-novelty-new",
        1 => "is14-research-novelty-refined",
        2 => "is14-research-novelty-repeat",
        _ => "is14-research-novelty-confirmed",
    };

    /// <summary>A failed experiment still counts for something.</summary>
    public const float FailureMultiplier = 0.15f;
}
