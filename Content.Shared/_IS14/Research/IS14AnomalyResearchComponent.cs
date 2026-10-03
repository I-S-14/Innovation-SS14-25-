// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Research;

/// <summary>
/// What an anomalous vessel is worth to us. Added to the vessel at runtime rather than written
/// into the upstream prototype, so the footprint outside <c>_IS14</c> stays nil.
/// </summary>
/// <remarks>
/// Upstream a vessel ticks research points forever, which is the passive income this whole
/// department was built to replace. Here it pays twice and then stops: once for stabilising a
/// type of anomaly nobody has filed yet, and again whenever it holds one at a severity the
/// station has never recorded. Keeping an anomaly in a jar earns nothing; catching a new kind
/// of anomaly, or riding one further than anyone dared, earns well.
/// </remarks>
[RegisterComponent]
public sealed partial class IS14AnomalyResearchComponent : Component
{
    [DataField]
    public ProtoId<ResearchPointTypePrototype> PointType = "Science";

    /// <summary>Payout for the first stabilisation of an anomaly type.</summary>
    [DataField]
    public int StabilisationValue = 90;

    /// <summary>Payout for a severity record that doubles the standing one.</summary>
    [DataField]
    public int RecordValue = 70;

    /// <summary>How often a vessel is read.</summary>
    [DataField]
    public TimeSpan Interval = TimeSpan.FromSeconds(10);

    /// <summary>The anomaly this vessel has already filed, so it is filed once. Runtime.</summary>
    [ViewVariables]
    public EntityUid? Filed;

    [ViewVariables]
    public TimeSpan NextSample;
}
