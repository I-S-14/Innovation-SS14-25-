namespace Content.Client.Atmos.Components;

public sealed partial class MaxPressureVisualsComponent
{
    /// <summary>Alternative damage feedback for tank art without integrity-mask states.</summary>
    [DataField]
    public Color? IntegrityColor;

    public Color OriginalColor = Color.White;
}
