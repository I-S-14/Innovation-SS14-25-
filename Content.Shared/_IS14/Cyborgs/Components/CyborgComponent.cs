// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs.Prototypes;
using Content.Shared.Alert;
using Content.Shared.Radio;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.Containers;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// A tgstation-style cyborg: a chassis with a brain slot, a single model that
/// grants a fixed kit of tools, and a few active tool slots.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true), AutoGenerateComponentPause]
public sealed partial class CyborgComponent : Component
{
    #region Brain

    [DataField]
    public string BrainContainerId = "cyborg_brain";

    [ViewVariables]
    public ContainerSlot BrainContainer = default!;

    public EntityUid? BrainEntity => BrainContainer?.ContainedEntity;

    /// <summary>
    /// What can be installed as the brain. Null allows anything with a borg brain component.
    /// </summary>
    [DataField]
    public EntityWhitelist? BrainWhitelist;

    #endregion

    #region Model

    /// <summary>
    /// The current model. Null means the cyborg has not picked one yet.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<CyborgModelPrototype>? Model;

    /// <summary>
    /// The current skin. When null the <see cref="DefaultSkin"/> is shown.
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<CyborgSkinPrototype>? Skin;

    /// <summary>
    /// Whether the player already picked a skin, so the skin menu isn't offered again.
    /// </summary>
    [DataField]
    public bool SkinChosen;

    /// <summary>
    /// Skin shown before a model is picked.
    /// </summary>
    [DataField]
    public ProtoId<CyborgSkinPrototype> DefaultSkin = "IS14CyborgSkinStandard";

    /// <summary>
    /// Channels the cyborg always has, regardless of model.
    /// </summary>
    [DataField]
    public List<ProtoId<RadioChannelPrototype>> InherentRadioChannels = new() { "Common" };

    /// <summary>
    /// Channels the cyborg hears but can't speak on, like the xenoborgs' mothership channel.
    /// </summary>
    [DataField]
    public List<ProtoId<RadioChannelPrototype>> ListenRadioChannels = new();

    /// <summary>
    /// Whether the emag tools of the model are available.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool EmagToolsUnlocked;

    #endregion

    #region Tools

    [DataField]
    public string ToolContainerId = "cyborg_tools";

    [ViewVariables]
    public Container ToolContainer = default!;

    /// <summary>
    /// Number of tools the cyborg can hold out at once.
    /// </summary>
    [DataField]
    public int ToolSlots = 3;

    [DataField]
    public string ToolHandPrefix = "cyborg-slot";

    /// <summary>
    /// Slots knocked offline by chassis damage, counted from the last one, like tg's disabled_modules.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int BrokenSlots;

    /// <summary>
    /// Damage, as a fraction of the critical threshold, at which the last, second to last... slot goes offline.
    /// Steeper than tg's half/zero/minus half health, so the last slot is gone by the time the chassis is critical.
    /// </summary>
    [DataField]
    public List<float> SlotBreakFractions = new() { 0.5f, 0.75f, 1f };

    [DataField]
    public SoundSpecifier? SlotBreakSound = new SoundPathSpecifier("/Audio/Machines/warning_buzzer.ogg");

    #endregion

    #region Upgrades

    /// <summary>
    /// tg's silicon tablet: the built-in IS14 OS device, opened from the HUD.
    /// </summary>
    [DataField]
    public EntProtoId? OsDevice = "IS14CyborgOsDevice";

    [DataField]
    public string OsContainerId = "cyborg_os";

    [ViewVariables]
    public ContainerSlot OsContainer = default!;

    public EntityUid? OsEntity => OsContainer?.ContainedEntity;

    [DataField]
    public string UpgradeContainerId = "cyborg_upgrades";

    [ViewVariables]
    public Container UpgradeContainer = default!;

    #endregion

    #region Power

    /// <summary>
    /// Whether the cyborg is powered, alive and has a player. Inactive cyborgs drop their tools and move slowly.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField]
    public SoundSpecifier? ActivateSound = new SoundPathSpecifier("/Audio/Items/flashlight_on.ogg");

    [DataField]
    public SoundSpecifier? DeactivateSound = new SoundPathSpecifier("/Audio/Items/flashlight_off.ogg");

    /// <summary>
    /// tg's transform animation after picking a model: the cyborg is stuck in place in a puff of smoke
    /// while its tools are rebuilt. Null when not transforming.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? TransformEnd;

    [DataField]
    public TimeSpan TransformTime = TimeSpan.FromSeconds(4.8);

    [DataField]
    public SoundSpecifier TransformSound = new SoundCollectionSpecifier("IS14CyborgTransform");

    [DataField]
    public EntProtoId TransformEffect = "IS14CyborgTransformSmoke";

    [ViewVariables]
    public TimeSpan NextTransformSound;

    /// <summary>
    /// tg's borg death sound.
    /// </summary>
    [DataField]
    public SoundSpecifier? DeathSound = new SoundPathSpecifier("/Audio/_IS14/Cyborgs/borg_deathsound.ogg");

    /// <summary>
    /// tg's "live again" chime, played when a brain goes into the chassis.
    /// </summary>
    [DataField]
    public SoundSpecifier? BrainInsertSound = new SoundPathSpecifier("/Audio/_IS14/Cyborgs/liveagain.ogg");

    [DataField(customTypeSerializer: typeof(TimeOffsetSerializer)), AutoPausedField]
    public TimeSpan NextBatteryUpdate = TimeSpan.Zero;

    [DataField]
    public ProtoId<AlertPrototype> BatteryAlert = "BorgBattery";

    [DataField]
    public ProtoId<AlertPrototype> NoBatteryAlert = "BorgBatteryNone";

    #endregion

    /// <summary>
    /// Lets the cyborg open its own UIs that normally need someone else (the model picker).
    /// </summary>
    [DataField]
    public bool CanOpenSelfUi;
}
