using Content.Shared.Tools;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.TransitTube.Components;

/// <summary>
/// A section of transit tube. Pods travel from section to section, asking each one
/// where to go next. The shape of a section (and therefore which neighbours it can
/// connect to) comes from a second component, such as
/// <see cref="TransitTubeStraightComponent"/> or <see cref="TransitTubeBendComponent"/>.
/// </summary>
[RegisterComponent]
public sealed partial class TransitTubeComponent : Component
{
    /// <summary>
    /// How long, in seconds, a pod spends leaving this section. Six tiles a second: fast
    /// enough that riding beats walking by a wide margin, slow enough that the pod still
    /// reads as moving rather than blinking from tile to tile.
    /// </summary>
    [DataField]
    public float ExitDelay = 0.167f;

    /// <summary>
    /// How long, in seconds, a pod spends entering this section. Added to the previous
    /// section's <see cref="ExitDelay"/>, so a slow section slows pods down from both ends.
    /// </summary>
    [DataField]
    public float EnterDelay;

    /// <summary>
    /// The tool that nudges this section, and how long it takes.
    /// </summary>
    [DataField]
    public ProtoId<ToolQualityPrototype> AdjustTool = "Screwing";

    [DataField]
    public float AdjustDelay = 0.5f;

    /// <summary>
    /// Sprite state for the shape of this section. It lives here rather than in the sprite
    /// layers because a prototype that spells out its own layers replaces its parent's
    /// list wholesale, and with it the layers that dress the joints.
    /// </summary>
    [DataField]
    public string Shape = "tube";

    /// <summary>
    /// Shape to draw when the section is reckoned 45 degrees round. Null for shapes that
    /// do not come as a pair, such as stops, which draw themselves from their own state.
    /// </summary>
    [DataField]
    public string? DiagonalShape;
}
