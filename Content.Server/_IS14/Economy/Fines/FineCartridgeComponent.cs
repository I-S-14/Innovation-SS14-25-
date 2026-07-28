namespace Content.Server._IS14.Economy.Fines;

/// <summary>
/// PDA program Security uses to write fines. Holds no state of its own — everything
/// lives on the station so any officer's PDA shows the same ledger.
/// </summary>
[RegisterComponent]
public sealed partial class FineCartridgeComponent : Component
{
}
