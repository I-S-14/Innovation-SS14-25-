using Content.Shared.Cargo.Prototypes;

namespace Content.Shared.Cargo;

public sealed partial class CargoOrderData
{
    /// <summary>Packaging selected when the order was placed, retained through delivery.</summary>
    [DataField]
    public CargoProductContainer? Container { get; private set; }
}
