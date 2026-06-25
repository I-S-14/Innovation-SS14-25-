namespace Content.Shared._IS14.Economy;

/// <summary>
/// Makes this entity a holder (provider) of a bank account.
/// Placed on ID cards at round start. Future ATMs/shops check for this component.
/// </summary>
[RegisterComponent]
public sealed partial class BankAccountHolderComponent : Component
{
    [DataField]
    public int AccountNumber;
}
