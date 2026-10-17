// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// tgstation's connected_ai and lawupdate: a cyborg slaved to the station AI follows its laws,
/// and the AI can lock it down. The wires behind the cover cut or restore all of this.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyborgAiLinkComponent : Component
{
    /// <summary>
    /// The AI this cyborg answers to, the entity held in its core.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Ai;

    /// <summary>
    /// Whether the AI's laws are copied over. tg's lawupdate, cut with the law sync wire.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool LawSync = true;

    /// <summary>
    /// The AI connection wire is cut: no AI can link to this cyborg.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool LinkCut;

    /// <summary>
    /// tg's lockdown: the cyborg can't move or act until released.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Lockdown;

    /// <summary>
    /// Freshly built and freelancer cyborgs don't look for an AI.
    /// </summary>
    [DataField]
    public bool AutoLink = true;

    [DataField]
    public SoundSpecifier LockdownSound = new SoundPathSpecifier("/Audio/Machines/warning_buzzer.ogg");
}

[Serializable, NetSerializable]
public enum CyborgWireStatus : byte
{
    AiLink,
    LawSync,
    Lockdown,
    ResetModel,
}
