using Content.Shared._IS14.Economy;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.Economy.Components;

/// <summary>
/// Added to a station entity (via map prototype) to declare which bank accounts
/// should be created for it at round start.
/// </summary>
[RegisterComponent]
public sealed partial class StationEconomyComponent : Component
{
    /// <summary>
    /// List of <c>stationBankAccount</c> prototype IDs to create when the station initializes.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<StationAccountPrototype>> Accounts = new();
}
