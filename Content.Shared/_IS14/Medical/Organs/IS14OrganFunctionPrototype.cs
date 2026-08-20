// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Medical.Organs;

/// <summary>
/// What the organ in a given body slot does, for every species at once.
/// </summary>
/// <remarks>
/// Keyed by <c>Organ.slotId</c>: a prototype with id <c>heart</c> describes every heart in the
/// game. Species organs almost all inherit from the human ones, so writing the component into
/// prototypes would mean either editing a dozen upstream files or missing a dozen species.
/// This costs one lookup when an organ is created and nothing afterwards.
/// </remarks>
[Prototype("is14OrganFunction")]
public sealed partial class IS14OrganFunctionPrototype : IPrototype
{
    /// <summary>The organ slot this describes, e.g. <c>heart</c>.</summary>
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public Dictionary<string, float> Contributions = new();

    /// <inheritdoc cref="IS14OrganFunctionComponent.Reserve"/>
    [DataField]
    public float Reserve = 0.6f;

    /// <inheritdoc cref="IS14OrganFunctionComponent.Floor"/>
    [DataField]
    public float Floor = 0.05f;

    /// <inheritdoc cref="IS14OrganFunctionComponent.PerfusionSensitivity"/>
    [DataField]
    public float PerfusionSensitivity;

    /// <inheritdoc cref="IS14OrganFunctionComponent.InjuryCap"/>
    [DataField]
    public float InjuryCap = 0.85f;
}
