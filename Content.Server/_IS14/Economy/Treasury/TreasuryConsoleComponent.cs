namespace Content.Server._IS14.Economy.Treasury;

/// <summary>
/// Marks a console that displays station account balances and lets command
/// transfer credits from the treasury to department funds.
/// </summary>
[RegisterComponent]
public sealed partial class TreasuryConsoleComponent : Component
{
    /// <summary>
    /// Localized result of the last transfer. Kept here because the console now has two front
    /// ends — the standalone window and the OS application — and both read the same answer.
    /// </summary>
    [ViewVariables]
    public string Status = string.Empty;
}
