// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

using Robust.Shared.Audio;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// tgstation's janitor auto-wash: while on, the cyborg mops every tile it drives over with water
/// from its internal tank, taking the mess into the tank. Slower, and paid from the power cell.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyborgAutoWashComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Enabled;

    [DataField]
    public EntProtoId Action = "IS14ActionCyborgAutoWash";

    [DataField, AutoNetworkedField]
    public EntityUid? ActionEntity;

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
    /// The built-in tool whose solution is the washing tank: water out, mess in.
    /// </summary>
    [DataField]
    public EntProtoId ReservoirTool = "IS14CyborgToolBucket";

    [DataField]
    public string ReservoirSolution = "drink";

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

public sealed partial class CyborgToggleAutoWashEvent : InstantActionEvent;
