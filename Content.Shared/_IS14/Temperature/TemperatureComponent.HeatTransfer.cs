namespace Content.Shared.Temperature.Components;

public sealed partial class TemperatureComponent
{
    /// <summary>Explicit capacity on new Wizards prototypes; otherwise use Goob mass times specific heat.</summary>
    [DataField]
    public float? HeatCapacity;

    /// <summary>New heat conductance per exposed area, in W/(K*m²).</summary>
    [DataField]
    public float? ThermalConductivity;
}
