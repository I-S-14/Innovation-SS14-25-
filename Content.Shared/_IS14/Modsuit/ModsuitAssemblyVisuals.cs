// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Modsuit;

/// <summary>
///     Appearance key for a MOD being built. The assembly itself is a construction graph
///     — the engine owns the steps, the tools, the do-afters and the container hand-off —
///     so the only thing left for content code is a key the graph can write a step number
///     into, which a <c>GenericVisualizer</c> turns back into a sprite state.
///
///     The value is a plain int written by <c>VisualizerDataInt</c>, so the visualizer's
///     YAML keys are the numbers, not names. This mirrors <c>MechAssemblyVisuals</c>, the
///     other in-world assembly in the game.
/// </summary>
[Serializable, NetSerializable]
public enum ModsuitAssemblyVisuals : byte
{
    Stage,
}
