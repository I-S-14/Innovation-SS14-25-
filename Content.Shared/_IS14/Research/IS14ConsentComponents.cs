// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Research;

/// <summary>
/// A consent form. Signed by the volunteer themselves — it is the piece of paper that turns
/// an experiment on a living person into research instead of a crime.
/// </summary>
/// <remarks>
/// Why paper and not a flag on the scanner: consent has to be a physical object someone can
/// refuse to sign, hand over, forge or lose. It also gives the assistant a trade — they walk
/// out of the lab with credits from the NIC fund, which is how money flows from science back
/// into the crew.
/// </remarks>
[RegisterComponent]
public sealed partial class IS14ConsentFormComponent : Component
{
    [DataField]
    public bool Signed;

    /// <summary>Who signed, for the examine text.</summary>
    [DataField]
    public string SubjectName = string.Empty;

    [DataField]
    public string SubjectTitle = string.Empty;

    /// <summary>
    /// Identity of the person behind the body. The scanner checks the subject against this,
    /// so a form signed by one volunteer does not cover another.
    /// </summary>
    [DataField]
    public string SubjectKey = string.Empty;

    [DataField]
    public TimeSpan SignTime = TimeSpan.FromSeconds(3);
}

/// <summary>
/// Examines a living volunteer: blood, reactions, stress data. Pays biological data for a
/// person nobody has examined yet, and pays the volunteer in credits.
/// </summary>
[RegisterComponent]
public sealed partial class IS14VolunteerScannerComponent : Component
{
    [DataField]
    public ProtoId<ResearchPointTypePrototype> PointType = "Biological";

    [DataField]
    public int BaseValue = 45;

    /// <summary>Credits the volunteer is paid out of the department's budget.</summary>
    [DataField]
    public int Fee = 150;

    /// <summary>Station account the fee comes out of.</summary>
    [DataField]
    public string Account = "StationResearch";

    /// <summary>
    /// What an examination without a signed form is worth. It still works — a forced
    /// experiment has to be mechanically possible for the people who would do it — but the
    /// data is sloppy and the attempt is in the logs.
    /// </summary>
    [DataField]
    public float UnconsentedMultiplier = 0.4f;

    [DataField]
    public string ProfileNamespace = "volunteer";

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(4);

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Items/beep.ogg");

    [DataField]
    public ProtoId<RadioChannelPrototype>? Channel;
}

[Serializable, NetSerializable]
public sealed partial class IS14ConsentSignDoAfterEvent : SimpleDoAfterEvent;

[Serializable, NetSerializable]
public sealed partial class IS14VolunteerScanDoAfterEvent : SimpleDoAfterEvent;
