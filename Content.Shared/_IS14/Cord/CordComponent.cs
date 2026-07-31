// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Cord;

/// <summary>
/// Draws a physical line between this entity and another one — a cable, a hose, a rope,
/// a chain. What the line looks like is not this component's business: the path comes
/// from a <see cref="CordShape"/> and the colouring from a <see cref="CordEffect"/>,
/// both chosen in YAML with <c>!type:</c>, so a new kind of cord is a prototype edit
/// rather than a new system.
/// </summary>
/// <remarks>
/// Nothing draws while <see cref="Anchor"/> is null, so attaching and detaching a cord
/// is one networked field rather than adding and removing a component.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CordComponent : Component
{
    /// <summary>The other end. Null means the cord is coiled up and nothing is drawn.</summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Anchor;

    /// <summary>
    /// Set by whatever owns the cord to mean "something is running through this right
    /// now". Effects are free to ignore it; the ones that don't use it to stay dark
    /// until the cord matters.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Energized;

    /// <summary>
    /// Length at which the cord is fully paid out, in metres. Shapes use it to decide
    /// how much slack they have left, so a cord visibly pulls straight as it runs out.
    /// Zero means the cord never reads as taut.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float SlackLength = 3f;

    /// <summary>Colour of the cord itself, before any effect is applied.</summary>
    [DataField]
    public Color Color = Color.FromHex("#2A2D36");

    /// <summary>Thickness in metres. One tile is one metre.</summary>
    [DataField]
    public float Width = 0.0625f;

    /// <summary>The path the cord takes between its two ends.</summary>
    [DataField]
    public CordShape Shape = new StraightCordShape();

    /// <summary>Optional per-segment colouring, for cords that do something visible.</summary>
    [DataField]
    public CordEffect? Effect;

    /// <summary>
    /// How slack the cord is right now, 0 when taut and 1 when fully coiled. Derived
    /// here rather than in each shape so they all agree on what taut means.
    /// </summary>
    public float GetSlack(float length)
    {
        if (SlackLength <= 0f)
            return 1f;

        return Math.Clamp(1f - length / SlackLength, 0f, 1f);
    }
}
