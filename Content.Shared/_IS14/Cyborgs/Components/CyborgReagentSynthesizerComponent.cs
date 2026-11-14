// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// A cyborg container that synthesizes one chosen reagent from the power cell,
/// like tgstation's borghypo and borgshaker.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class CyborgReagentSynthesizerComponent : Component
{
    [DataField(required: true)]
    public string Solution = string.Empty;

    [DataField(required: true)]
    public List<ProtoId<ReagentPrototype>> Reagents = new();

    /// <summary>
    /// Extra reagents offered once the cyborg is emagged.
    /// </summary>
    [DataField]
    public List<ProtoId<ReagentPrototype>> EmagReagents = new();

    [DataField, AutoNetworkedField]
    public ProtoId<ReagentPrototype>? Selected;

    [DataField]
    public FixedPoint2 RegenAmount = 1;

    [DataField]
    public float ChargePerUnit = 2f;

    [DataField]
    public TimeSpan RegenDelay = TimeSpan.FromSeconds(2);

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextRegen = TimeSpan.Zero;
}
