// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Content.Shared.Alert;
using Content.Shared.Chat.Prototypes;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// tgstation's tippable cyborgs: shove one with an empty hand to tip it over onto its head.
/// A tipped cyborg can only turn around and call for help until someone rights it,
/// or it rights itself after a while. Beeping and buzzing for help speeds that up.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class CyborgTippableComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Tipped;

    [DataField]
    public TimeSpan TipTime = TimeSpan.FromSeconds(3);

    [DataField]
    public TimeSpan UntipTime = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How long a tipped cyborg is stuck before it may get up by itself, through the alert or its own verb.
    /// </summary>
    [DataField]
    public TimeSpan SelfRightTime = TimeSpan.FromSeconds(30);

    /// <summary>
    /// How long it takes a tipped cyborg to roll back onto its wheels once it may.
    /// </summary>
    [DataField]
    public TimeSpan SelfRightDoAfter = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Fallback for a cyborg nobody is playing: it rights itself this long after tipping.
    /// </summary>
    [DataField]
    public TimeSpan AutoRightTime = TimeSpan.FromSeconds(90);

    /// <summary>
    /// When a tipped cyborg may get back up on its own.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan SelfRightAt;

    /// <summary>
    /// When a tipped cyborg gets back up whether or not it tries.
    /// </summary>
    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoNetworkedField, AutoPausedField]
    public TimeSpan AutoRightAt;

    [DataField]
    public ProtoId<AlertPrototype> TippedAlert = "IS14CyborgTipped";

    /// <summary>
    /// Emotes that count as calling for help: each one takes a quarter off the remaining time, like tg.
    /// </summary>
    [DataField]
    public List<ProtoId<EmotePrototype>> HelpEmotes = new() { "Beep", "Buzz", "Buzz-Two", "Chime", "Ping" };

    [DataField]
    public float HelpEmoteFactor = 0.75f;

    [DataField]
    public SoundSpecifier TipSound = new SoundPathSpecifier("/Audio/Effects/metal_thud2.ogg");
}

/// <summary>
/// Clicking the tipped alert: try to roll back over.
/// </summary>
public sealed partial class CyborgTippedAlertEvent : BaseAlertEvent;
