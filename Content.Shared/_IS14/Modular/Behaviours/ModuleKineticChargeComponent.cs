// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;
using Robust.Shared.Map;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Puts charge back into the core from the wearer walking around.
///
///     The suit's whole economy is a slow leak, and every other answer to it — a spare
///     cell, a plasma core, a recharger on the wall — is something you have to go and
///     fetch. This one is the answer you already have on you, and it is deliberately not
///     enough to run a suit full of modules: it pays for the baseline, not for the toys.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ModuleKineticChargeComponent : Component
{
    /// <summary>
    ///     Charge returned per metre travelled. A tile is a metre.
    /// </summary>
    [DataField]
    public float ChargePerMetre = 0.1f;

    /// <summary>
    ///     Distance past which a jump is treated as a teleport rather than a walk, so
    ///     nobody charges the suit by riding a shuttle or being thrown across the room.
    /// </summary>
    [DataField]
    public float MaxStep = 2f;

    /// <summary>
    ///     Where the wearer was last sampled. Null until the module starts running.
    /// </summary>
    [ViewVariables]
    public MapCoordinates? LastPosition;

    [ViewVariables]
    public bool Running;
}
