// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// tgstation's janitor auto-wash: while on, the cyborg mops every tile it drives over with water
/// from its internal tank, taking the mess into the tank. Slower, and paid from the power cell.
/// Switched on and off with the vacuum wand, see <see cref="CyborgAutoWashToolComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyborgAutoWashComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;

    /// <summary>
    /// The wand that switched washing on, its solution is the tank.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Tool;

    /// <summary>
    /// Speed multiplier while washing.
    /// </summary>
    [DataField]
    public float SpeedModifier = 0.7f;

    /// <summary>
    /// Power cell charge spent per washed tile.
    /// </summary>
    [DataField]
    public float ChargePerTile = 2f;

    /// <summary>
    /// Most of a puddle swapped per tile, like one swing of the mop.
    /// </summary>
    [DataField]
    public FixedPoint2 PickupAmount = 50;

    [ViewVariables]
    public EntityCoordinates? LastTile;

    // tg's /datum/looping_sound/wash.
    [DataField]
    public SoundSpecifier StartSound = new SoundPathSpecifier("/Audio/_IS14/Cyborgs/wash_start.ogg");

    [DataField]
    public SoundSpecifier LoopSound = new SoundPathSpecifier("/Audio/_IS14/Cyborgs/wash1.ogg");

    [DataField]
    public SoundSpecifier EndSound = new SoundPathSpecifier("/Audio/_IS14/Cyborgs/wash_end.ogg");

    [ViewVariables]
    public EntityUid? LoopStream;
}

/// <summary>
/// The janitor's vacuum wand: a mop whose solution is the washing tank. Toggling it on in a tool slot
/// starts the cyborg's <see cref="CyborgAutoWashComponent"/>, stowing it stops washing.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyborgAutoWashToolComponent : Component
{
    /// <summary>
    /// The washing tank: water out, mess in.
    /// </summary>
    [DataField]
    public string Solution = "absorbed";

    /// <summary>
    /// Played when the tank is filled from a container.
    /// </summary>
    [DataField]
    public SoundSpecifier FillSound = new SoundPathSpecifier("/Audio/Effects/Fluids/slosh.ogg");
}
