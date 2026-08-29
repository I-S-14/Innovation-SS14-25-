// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Modsuit;

/// <summary>
///     Keys the wire panel uses to remember what it has done to a suit.
///
///     Shared rather than server-side on purpose: these travel inside
///     <c>WiresBoundUserInterfaceState</c>, so the client has to be able to name the type.
///     A key defined in <c>Content.Server</c> serialises fine right up until somebody
///     opens the panel, and then the server dies mid-tick.
/// </summary>
[Serializable, NetSerializable]
public enum ModsuitWireKey : byte
{
    /// <summary>Shared by both power leads: one light counting how many are left.</summary>
    PowerStatus,
    DeployStatus,
    SealStatus,
    ShockStatus,
    InterfaceStatus,

    /// <summary>
    ///     Belongs to no wire: the circuit's own readout, lit while the suit is being
    ///     driven too hard. Set through <c>WiresSystem.SetStatus</c>.
    /// </summary>
    OverloadStatus,
}
