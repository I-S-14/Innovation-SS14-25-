namespace Content.Shared._IS14.TransitTube.Components;

/// <summary>
/// A tube section that runs straight through. Its four turns are four different runs:
/// up and down, one diagonal, across, the other diagonal. That way turning one in the
/// editor reaches every line a plain section can make, with nothing to adjust afterwards.
/// </summary>
[RegisterComponent]
public sealed partial class TransitTubeStraightComponent : Component
{
}
