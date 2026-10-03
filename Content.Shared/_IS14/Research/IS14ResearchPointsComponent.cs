// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Research;

/// <summary>
/// The station's research data balances. Lives on the station entity, like the economy's
/// accounts do, so any device anywhere on the grid can pay into it without wiring.
/// </summary>
[RegisterComponent]
public sealed partial class IS14ResearchPointsComponent : Component
{
    [DataField]
    public Dictionary<ProtoId<ResearchPointTypePrototype>, int> Points = new();

    /// <summary>
    /// Recent payments, newest last. Drives the "+N за 5 минут" figure on the console,
    /// which is how players see which department is actually working.
    /// Runtime only — a restarted round starts the ledger over.
    /// </summary>
    [ViewVariables]
    public List<IS14ResearchIncomeRecord> RecentIncome = new();
}

public readonly record struct IS14ResearchIncomeRecord(TimeSpan Time, string PointType, int Amount);

/// <summary>Raised on the station after any balance changes, so open consoles refresh.</summary>
[ByRefEvent]
public readonly record struct IS14ResearchPointsChangedEvent(EntityUid Station);
