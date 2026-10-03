// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._IS14.Research;

/// <summary>
/// Best figures the station has ever reached, per named record: peak power generation, the
/// hottest mixture that has been measured, and whatever else is worth beating.
/// </summary>
/// <remarks>
/// This is the other half of the novelty rule. The archive stops repetition from paying;
/// records make <em>exceeding</em> yourself pay. Engineering gets nothing for a reactor that
/// simply runs, and a lot for a reactor that ran harder than it ever has — which is the only
/// way to make a department risk its own infrastructure on purpose.
/// </remarks>
[RegisterComponent]
public sealed partial class IS14StationRecordsComponent : Component
{
    /// <summary>Record key to the best value seen this shift.</summary>
    [DataField]
    public Dictionary<string, float> Best = new();
}
