// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Radio;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.Research;

/// <summary>
/// The NIC console: spends the station's research data on technologies.
/// </summary>
/// <remarks>
/// Runs alongside the upstream R&amp;D console rather than replacing it. The console is
/// still an upstream research client, so unlocked technologies land in the same server
/// database and lathes keep working exactly as before — only the payment is ours.
/// </remarks>
[RegisterComponent]
public sealed partial class IS14ResearchConsoleComponent : Component
{
    /// <summary>Where unlocks are announced. Null keeps purchases quiet.</summary>
    [DataField]
    public ProtoId<RadioChannelPrototype>? AnnouncementChannel = "Science";

    /// <summary>
    /// Branches this console shows, in tab order. Empty shows everything, which is only useful
    /// for debugging: a console that lists both trees at once is unreadable.
    /// </summary>
    [DataField]
    public List<ProtoId<TechDisciplinePrototype>> Branches = new();
}
