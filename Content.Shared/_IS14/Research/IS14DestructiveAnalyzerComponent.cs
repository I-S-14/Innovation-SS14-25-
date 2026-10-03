// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Radio;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Research;

/// <summary>
/// Destructive analyzer: takes an object apart for good and reads far more out of it than any
/// scan would. Borrowed from tgstation's R&amp;D, where destroying the sample is the point.
/// </summary>
/// <remarks>
/// Two things make it more than a bin that pays out. The yield scales with what the object is
/// physically made of, so a machine part is worth more than a spoon; and taking apart something
/// the station cannot build yet is reverse engineering — it discounts the technology that would
/// unlock it, which turns any captured or salvaged item into a shortcut.
/// </remarks>
[RegisterComponent]
public sealed partial class IS14DestructiveAnalyzerComponent : Component
{
    /// <summary>Currency the teardown itself pays in.</summary>
    [DataField]
    public ProtoId<ResearchPointTypePrototype> PointType = "Industrial";

    /// <summary>Yield for a sample of <see cref="ReferenceMaterial"/> worth of materials.</summary>
    [DataField]
    public int BaseValue = 30;

    /// <summary>Material units that pay exactly <see cref="BaseValue"/>.</summary>
    [DataField]
    public int ReferenceMaterial = 400;

    [DataField]
    public float MinScale = 0.4f;

    [DataField]
    public float MaxScale = 3f;

    /// <summary>
    /// How much of a technology's price one teardown of something it unlocks knocks off.
    /// </summary>
    [DataField]
    public float ReverseEngineeringDiscount = 0.15f;

    /// <summary>Ceiling on the accumulated discount, so a pile of samples cannot make it free.</summary>
    [DataField]
    public float MaxDiscount = 0.5f;

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;

    [DataField]
    public TimeSpan Duration = TimeSpan.FromSeconds(4);

    /// <summary>Absolute game time the current teardown finishes. Runtime.</summary>
    [ViewVariables]
    public TimeSpan? FinishesAt;

    /// <summary>
    /// What is being taken apart. The object itself is consumed the moment it goes in — the
    /// cycle is the analysis, not the destruction.
    /// </summary>
    [ViewVariables]
    public string? PendingPrototype;

    /// <summary>Material units the sample was worth. Runtime.</summary>
    [ViewVariables]
    public int PendingMaterials;

    /// <summary>Who fed it in, so the result can be reported to them. Runtime.</summary>
    [ViewVariables]
    public EntityUid? User;

    /// <summary>
    /// Technology whose breakthrough this cycle pays for, when the sample was the key to one.
    /// Runtime.
    /// </summary>
    [ViewVariables]
    public string? PendingBreakthrough;

    [DataField]
    public string ProfileNamespace = "teardown";

    [DataField]
    public SoundSpecifier? StartSound = new SoundPathSpecifier("/Audio/Machines/circuitprinter.ogg");

    [DataField]
    public ProtoId<RadioChannelPrototype>? Channel;
}

/// <summary>
/// Reverse-engineering credit the station has built up, per technology.
/// </summary>
[RegisterComponent]
public sealed partial class IS14ResearchDiscountComponent : Component
{
    /// <summary>Technology ID to the fraction of its price already paid for in samples.</summary>
    [DataField]
    public Dictionary<string, float> Discounts = new();
}
