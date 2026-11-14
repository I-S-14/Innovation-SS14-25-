// SPDX-License-Identifier: MIT

using Content.Shared.Chemistry.EntitySystems;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;

namespace Content.Shared.Chemistry.Components.SolutionManager;

/// <remarks>
/// Exists for simple backwards compatibility.
/// On <see cref="MapInitEvent"/> this component will transfer all its data where it can to a <see cref="SolutionManagerComponent"/>
/// Then it will delete itself.
/// This component will be deleted in the indeterminate future.
/// </remarks>
[Obsolete]
[RegisterComponent]
[Access(typeof(SharedSolutionContainerSystem))]
public sealed partial class SolutionContainerManagerComponent : Component
{
    /// <summary>
    /// The default amount of space that will be allocated for solutions in solution containers.
    /// Most solution containers will only contain 1-2 solutions.
    /// </summary>
    public const int DefaultCapacity = 2;

    /// <summary>
    /// The names of each solution container attached to this entity.
    /// Actually accessing them must be done via <see cref="ContainerManagerComponent"/>.
    /// </summary>
    [DataField]
    public HashSet<string> Containers = new(DefaultCapacity);

    /// <summary>
    /// The set of solutions to load onto this entity during mapinit.
    /// </summary>
    /// <remarks>
    /// Should be null after mapinit.
    /// </remarks>
    [DataField]
    public Dictionary<string, Solution>? Solutions;

    //IS14-change: prevent recursive/repeated conversion when a MapInit consumer
    // requests its solution before this compatibility component's own callback.
    internal bool Migrating;
    internal bool Migrated;
}
