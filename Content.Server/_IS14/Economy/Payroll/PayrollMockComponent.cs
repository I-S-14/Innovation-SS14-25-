namespace Content.Server._IS14.Economy.Payroll;

/// <summary>
/// Marks a fake employee spawned to test the payroll console, so the whole batch — and the
/// accounts created behind it — can be swept away again with a single command.
/// </summary>
[RegisterComponent]
public sealed partial class PayrollMockComponent : Component
{
    /// <summary>Account created together with the mock. Removed when the mock is cleared.</summary>
    [DataField]
    public int AccountNumber;

    /// <summary>ID card handed to a mob mock, so a card that was dropped still gets cleaned up.</summary>
    [DataField]
    public EntityUid? Card;
}
