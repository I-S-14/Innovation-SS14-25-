// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     A module whose whole job is a readout. Switching it on opens its interface;
///     closing the interface switches it back off, so the module's <c>activeDraw</c>
///     bills the wearer for exactly as long as they are looking at it.
///
///     The readout itself is an ordinary bound interface declared on the module
///     prototype, which means anything with a window — a station map, a crew monitor,
///     a mass scanner — becomes a module without a line of C#.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ModuleInterfaceComponent : Component
{
    /// <summary>
    ///     Interface key to open. Must match one declared in the module's own
    ///     <c>UserInterface</c>.
    /// </summary>
    [DataField(required: true, customTypeSerializer: typeof(EnumSerializer))]
    public Enum? Key;
}
