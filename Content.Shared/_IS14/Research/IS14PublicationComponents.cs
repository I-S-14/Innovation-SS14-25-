// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Research;

/// <summary>
/// A scientific paper about one technology. Worth nothing by itself: it has to be stamped by
/// the people who answer for it and handed in.
/// </summary>
/// <remarks>
/// This is where science stops only spending money and starts earning it. The paper also makes
/// the bureaucracy of the setting do real work — a result is not a result until the research
/// director and the captain have put their stamps on it.
/// </remarks>
[RegisterComponent]
public sealed partial class IS14PublicationComponent : Component
{
    /// <summary>The technology this paper reports on.</summary>
    [DataField]
    public string TechnologyId = string.Empty;

    /// <summary>Set once it has been handed in, so a copy cannot be filed twice.</summary>
    [DataField]
    public bool Filed;
}

/// <summary>Prints a paper about the last technology the station researched.</summary>
[RegisterComponent]
public sealed partial class IS14PublicationPrinterComponent : Component
{
    [DataField]
    public EntProtoId PaperPrototype = "IS14PublicationPaper";

    [DataField]
    public TimeSpan Cooldown = TimeSpan.FromSeconds(10);

    [ViewVariables]
    public TimeSpan NextReady;

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");
}

/// <summary>
/// Accepts stamped papers on behalf of the Academy of Sciences: pays the station in credits and
/// a little scientific data, once per technology.
/// </summary>
[RegisterComponent]
public sealed partial class IS14PublicationReceiverComponent : Component
{
    /// <summary>
    /// Stamps the paper must carry, by their stamped name. Both of them: a result signed off
    /// by one person is an opinion.
    /// </summary>
    [DataField]
    public List<string> RequiredStamps = new()
    {
        "stamp-component-stamped-name-rd",
        "stamp-component-stamped-name-captain",
    };

    [DataField]
    public ProtoId<ResearchPointTypePrototype> PointType = "Science";

    /// <summary>Data the publication is worth, before the tier of what it reports on.</summary>
    [DataField]
    public int BaseValue = 25;

    /// <summary>Credits the Academy transfers for a published result.</summary>
    [DataField]
    public int Payment = 1200;

    /// <summary>Account the fee lands in.</summary>
    [DataField]
    public string Account = "StationResearch";

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Machines/high_tech_confirm.ogg");

    [DataField]
    public ProtoId<RadioChannelPrototype>? Channel = "Science";
}

/// <summary>
/// What the station has researched and published. Lives next to the research balances, like
/// every other piece of shift state.
/// </summary>
[RegisterComponent]
public sealed partial class IS14PublicationsComponent : Component
{
    /// <summary>Last technology bought here — what the printer writes a paper about.</summary>
    [DataField]
    public string? LastUnlocked;

    /// <summary>Technologies already published. One paper per result.</summary>
    [DataField]
    public HashSet<string> Filed = new();
}
