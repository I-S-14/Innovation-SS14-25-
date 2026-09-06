namespace Content.Shared._IS14.OS.Components;

/// <summary>
///     An expansion board. Memory is the platform's only real constraint, so this item is the
///     platform's only real upgrade — and the reason a department buys anything from science.
/// </summary>
[RegisterComponent]
public sealed partial class IS14OsMemoryBoardComponent : Component
{
    [DataField]
    public int Amount = 64;
}
