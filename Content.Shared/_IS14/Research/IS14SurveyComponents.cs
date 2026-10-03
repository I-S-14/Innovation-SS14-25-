// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Research;

/// <summary>
/// A sociology questionnaire. Worthless until an actual crew member fills it in, which
/// is the entire design of social data: it cannot be ground out alone or scripted.
/// </summary>
[RegisterComponent]
public sealed partial class IS14SurveyFormComponent : Component
{
    [DataField]
    public bool Filled;

    /// <summary>Name shown on the filled form.</summary>
    [DataField]
    public string RespondentName = string.Empty;

    /// <summary>Job title, for flavour and for the payout weight.</summary>
    [DataField]
    public string RespondentTitle = string.Empty;

    /// <summary>
    /// Identity the archive remembers. One person, one useful survey — asking the same
    /// assistant twelve times is a repeat measurement.
    /// </summary>
    [DataField]
    public string RespondentKey = string.Empty;

    /// <summary>Multiplier from the respondent's position. A captain's answers are worth more.</summary>
    [DataField]
    public float Weight = 1f;

    [DataField]
    public TimeSpan FillTime = TimeSpan.FromSeconds(4);
}

/// <summary>Prints blank questionnaires. Someone still has to walk them around the station.</summary>
[RegisterComponent]
public sealed partial class IS14SurveyPrinterComponent : Component
{
    [DataField]
    public EntProtoId FormPrototype = "IS14SurveyForm";

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(8);

    [ViewVariables]
    public TimeSpan NextReady;

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");
}

[Serializable, NetSerializable]
public sealed partial class IS14SurveyFillDoAfterEvent : SimpleDoAfterEvent;
