// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Radio;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Research;

/// <summary>
/// Telemetry recorder: watches what the station's power plant is actually doing and pays
/// industrial data when engineering beats its own peak.
/// </summary>
/// <remarks>
/// Deliberately paid per record and not per second. An engine that simply runs is worth
/// nothing — this is the whole difference between our industrial data and the passive drip
/// it replaces. The department earns by pushing output past where it has ever been, which is
/// exactly the behaviour that makes an engineering shift interesting to watch.
/// </remarks>
[RegisterComponent]
public sealed partial class IS14EngineTelemetryComponent : Component
{
    [DataField]
    public ProtoId<ResearchPointTypePrototype> PointType = "Industrial";

    /// <summary>Payout for a record that doubles the previous one.</summary>
    [DataField]
    public int BaseValue = 120;

    /// <summary>How often the recorder reads the grid.</summary>
    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(5);

    /// <summary>Generation below this is not a result worth filing, in watts.</summary>
    [DataField]
    public float MinimumWatts = 50_000f;

    /// <summary>Ratio over the standing record that counts as a full result.</summary>
    [DataField]
    public float FullShare = 1.5f;

    [DataField]
    public ProtoId<RadioChannelPrototype>? Channel;

    /// <summary>Record key, so several recorders on one station share one record.</summary>
    [DataField]
    public string RecordKey = "engine:peak";

    /// <summary>Absolute game time of the next reading. Runtime.</summary>
    [ViewVariables]
    public TimeSpan NextSample;
}
