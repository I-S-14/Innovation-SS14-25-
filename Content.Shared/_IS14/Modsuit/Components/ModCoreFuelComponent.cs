// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Systems;
using Content.Shared.Stacks;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Modsuit.Components;

/// <summary>
///     A core that burns something instead of taking a charge off a wire.
///
///     Fuel is matched by stack type rather than by tag, so the core accepts refined
///     sheets and raw ore at different rates without anyone having to tag the ore —
///     which is the difference between a shift spent at a recharger and a shift spent
///     in the mines.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(ModCoreFuelSystem))]
public sealed partial class ModCoreFuelComponent : Component
{
    /// <summary>
    ///     Accepted stack types and what one unit of each is worth, in joules.
    /// </summary>
    [DataField(required: true)]
    public Dictionary<ProtoId<StackPrototype>, float> Fuel = new();

    [DataField]
    public SoundSpecifier? RefuelSound = new SoundPathSpecifier("/Audio/_IS14/Modsuit/module_click.ogg");
}
