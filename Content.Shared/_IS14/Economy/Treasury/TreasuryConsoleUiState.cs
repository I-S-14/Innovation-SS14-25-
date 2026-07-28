using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.Treasury;

[Serializable, NetSerializable]
public sealed class TreasuryConsoleUiState : BoundUserInterfaceState
{
    /// <summary>Station accounts in prototype declaration order; the treasury is marked via IsTreasury.</summary>
    public readonly List<TreasuryAccountEntry> Accounts;

    /// <summary>Localized status line from the last operation (empty — nothing to show).</summary>
    public readonly string Status;

    public TreasuryConsoleUiState(List<TreasuryAccountEntry> accounts, string status = "")
    {
        Accounts = accounts;
        Status = status;
    }
}
