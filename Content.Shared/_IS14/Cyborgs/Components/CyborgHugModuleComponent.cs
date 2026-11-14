// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Damage;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// tgstation's cyborg hug module (/obj/item/borg/cyborghug): hug people awake, and on some models
/// or once emagged, shock them down or crush them. Using it in hand switches the mode.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CyborgHugModuleComponent : Component
{
    [DataField, AutoNetworkedField]
    public CyborgHugMode Mode = CyborgHugMode.Hug;

    /// <summary>
    /// tg's peacekeeper hug module may shock without an emag.
    /// </summary>
    [DataField]
    public bool ShockAllowed;

    [DataField]
    public float ShockStamina = 60f;

    [DataField]
    public float ShockCharge = 50f;

    [DataField]
    public DamageSpecifier CrushDamage = new() { DamageDict = { ["Blunt"] = 15 } };

    [DataField]
    public float CrushCharge = 30f;

    [DataField]
    public SoundSpecifier HugSound = new SoundPathSpecifier("/Audio/Effects/thudswoosh.ogg");

    [DataField]
    public SoundSpecifier ShockSound = new SoundPathSpecifier("/Audio/Weapons/egloves.ogg");

    [DataField]
    public SoundSpecifier CrushSound = new SoundPathSpecifier("/Audio/Effects/metal_crunch.ogg");
}

[Serializable, NetSerializable]
public enum CyborgHugMode : byte
{
    Hug,
    Shock,
    Crush,
}
