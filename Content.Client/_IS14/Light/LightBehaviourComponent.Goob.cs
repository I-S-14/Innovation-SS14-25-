namespace Content.Client.Light.Components;

public sealed partial class LightBehaviourComponent
{
    // Retains Goob's guard against restarting the same animation on every appearance update.
    public string? CurrentExpendableBehaviour;
}
