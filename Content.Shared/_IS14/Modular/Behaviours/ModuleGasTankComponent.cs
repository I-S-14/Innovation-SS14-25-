// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Atmos;
using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Gives the chassis an internal volume and a compressor to fill it from whatever
///     the wearer is standing in.
///
///     This is the module the rest of the atmospheric kit hangs off: the suit becomes a
///     tank, which is what internals connect to and what a jetpack burns. A suit without
///     one has nothing to breathe and nothing to push against.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ModuleGasTankComponent : Component
{
    /// <summary>
    ///     Internal volume in litres. The whole difference between the small and the
    ///     large module is how long you can stay out.
    /// </summary>
    [DataField]
    public float Volume = 10f;

    /// <summary>
    ///     Pressure the compressor works up to, in kPa. A filled canister by default —
    ///     past that the tank starts leaking on its own.
    /// </summary>
    [DataField]
    public float TargetPressure = 1013.25f;

    /// <summary>
    ///     Moles moved per second while the compressor is running. Deliberately tiny:
    ///     the compressor is meant to keep a bottle topped up over a shift, not to fill
    ///     one while you stand in the airlock.
    /// </summary>
    [DataField]
    public float FilterRate = 0.03f;

    /// <summary>
    ///     Watts drawn while it is actually pulling gas. Idle scrubbing costs nothing
    ///     because it is not doing anything.
    /// </summary>
    [DataField]
    public float FilterDraw = 3f;

    /// <summary>
    ///     Gases the wearer has told it to keep. Everything else is left in the room.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<Gas> Filtered = new() { Gas.Oxygen };

    /// <summary>
    ///     Gases this module is able to separate at all, in the order the interface
    ///     offers them.
    /// </summary>
    [DataField]
    public List<Gas> Available = new() { Gas.Oxygen, Gas.Nitrogen, Gas.CarbonDioxide };

    /// <summary>
    ///     Whether the compressor is drawing from the room. The tank stays whatever the
    ///     module is doing; this is only the pump.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Filtering = true;

    /// <summary>
    ///     What was in the bottle when the module was last pulled. The gas belongs to
    ///     the module, not to the suit: swapping a module between two suits should carry
    ///     the air across rather than dumping it on the floor of whichever room you
    ///     happened to be standing in.
    /// </summary>
    [DataField]
    public GasMixture? Stored;

    [ViewVariables]
    public bool Applied;
}
