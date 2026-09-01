// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Modsuit.Components;

/// <summary>
///     External plating for a MOD. This is the one piece of the kit that knows about
///     speciality: the shell and all four frames inside it are identical for every
///     theme, and the suit that comes out of the last step is whatever this says.
///
///     Holding the result here rather than branching the construction graph per theme
///     keeps the graph one edge wide. That is not only shorter — a graph that forks
///     eleven ways cannot tell the player what to do next, because until a specific
///     plating is offered there is no single next step to name.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ModsuitAssemblyPlatingComponent : Component
{
    /// <summary>
    ///     Controller the finished build becomes.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Result;
}
