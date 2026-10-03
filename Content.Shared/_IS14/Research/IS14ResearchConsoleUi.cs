// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Research;

[Serializable, NetSerializable]
public enum IS14ResearchConsoleUiKey : byte
{
    Key,
}

/// <summary>
/// Everything the NIC console cannot work out for itself. Prototypes are shared, so the
/// client resolves names, costs, prerequisites and affordability on its own — only
/// balances and what has actually been researched have to travel.
/// </summary>
[Serializable, NetSerializable]
public sealed class IS14ResearchConsoleUiState : BoundUserInterfaceState
{
    /// <summary>Point type ID to balance.</summary>
    public Dictionary<string, int> Points = new();

    /// <summary>Point type ID to what came in over the last few minutes.</summary>
    public Dictionary<string, int> RecentIncome = new();

    /// <summary>Technology IDs already researched on the connected server.</summary>
    public List<string> Unlocked = new();

    /// <summary>False when the console has no research server: nothing can be bought.</summary>
    public bool ServerConnected;

    public string ServerName = string.Empty;

    /// <summary>How long the income figure covers, in minutes, for the header label.</summary>
    public int IncomeWindowMinutes = 5;

    /// <summary>
    /// Branches this console shows, in tab order. Empty means every discipline, which is what a
    /// console wired to the upstream tree wants.
    /// </summary>
    public List<string> Branches = new();

    /// <summary>
    /// Reverse-engineering credit per technology, 0..1. Earned by taking samples apart in the
    /// destructive analyzer, and already reflected in what the console charges.
    /// </summary>
    public Dictionary<string, float> Discounts = new();

    /// <summary>
    /// Hidden technologies that exist for this station: one variant per randomised upgrade slot,
    /// plus anything reverse engineering has uncovered. Everything else hidden is not drawn.
    /// </summary>
    public List<string> Revealed = new();

    /// <summary>
    /// Topics Gosplan made a priority this shift, and by how much they are cheaper, 0..1.
    /// Shown separately from reverse-engineering credit because it is a different thing.
    /// </summary>
    public Dictionary<string, float> Priorities = new();

    /// <summary>
    /// Breakthrough technologies whose sample this station has already destroyed. Anything else
    /// with a sample requirement cannot be bought at any price.
    /// </summary>
    public List<string> Breakthroughs = new();
}

[Serializable, NetSerializable]
public sealed class IS14ResearchConsoleUnlockMessage : BoundUserInterfaceMessage
{
    public readonly string TechnologyId;

    public IS14ResearchConsoleUnlockMessage(string technologyId)
    {
        TechnologyId = technologyId;
    }
}
