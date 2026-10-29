// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Radio;
using Content.Shared.Stacks;
using Content.Shared.Whitelist;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Shared._IS14.Cyborgs.Prototypes;

/// <summary>
/// A cyborg model in the tgstation sense: a fixed kit of tools, radio channels and traits
/// the cyborg receives when it picks this model. Inherits, so event models can reuse a station kit.
/// </summary>
[Prototype]
public sealed partial class CyborgModelPrototype : IPrototype, IInheritingPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<CyborgModelPrototype>))]
    public string[]? Parents { get; private set; }

    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; private set; }

    [DataField(required: true)]
    public LocId Name;

    [DataField]
    public LocId? Description;

    /// <summary>
    /// Whether the model shows up in the model selection menu.
    /// Syndicate and event models are applied directly and stay hidden.
    /// </summary>
    [DataField]
    public bool Selectable = true;

    /// <summary>
    /// Position in the selection menu, lower comes first.
    /// </summary>
    [DataField]
    public int Order;

    /// <summary>
    /// State of the model button in the cyborg HUD, from screen_cyborg.rsi.
    /// </summary>
    [DataField]
    public string HudIcon = "standard";

    /// <summary>
    /// Tools spawned into the cyborg when the model is applied.
    /// </summary>
    [DataField]
    public List<EntProtoId> BasicTools = new();

    /// <summary>
    /// Tools that become available once the cyborg is emagged.
    /// </summary>
    [DataField]
    public List<EntProtoId> EmagTools = new();

    /// <summary>
    /// Stacks the cyborg may pick up into a free module slot, like the engineering cyborg's sheets and cables.
    /// Everything else stays out of its hands.
    /// </summary>
    [DataField]
    public List<ProtoId<StackPrototype>> PickupStacks = new();

    /// <summary>
    /// Other things the cyborg may pick up into a free module slot.
    /// </summary>
    [DataField]
    public EntityWhitelist? PickupWhitelist;

    /// <summary>
    /// Entity prototypes the cyborg may pick up, with everything inheriting from them, for items that share
    /// no tag or component, like plates. Abstract prototypes can't be listed: the parent walk skips them.
    /// </summary>
    [DataField]
    public List<EntProtoId> PickupPrototypes = new();

    /// <summary>
    /// Radio channels added on top of <see cref="Components.CyborgComponent.InherentRadioChannels"/>.
    /// </summary>
    [DataField]
    public List<ProtoId<RadioChannelPrototype>> RadioChannels = new();

    /// <summary>
    /// Skins this model can use. The first one is the default.
    /// </summary>
    [DataField(required: true)]
    public List<ProtoId<CyborgSkinPrototype>> Skins = new();

    /// <summary>
    /// Components added to the cyborg while it has this model, e.g. mesons or gravity negation.
    /// They are removed again when the model is reset.
    /// </summary>
    [DataField]
    public ComponentRegistry AddComponents = new();
}
