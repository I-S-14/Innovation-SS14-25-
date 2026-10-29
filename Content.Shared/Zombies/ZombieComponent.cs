// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body;
using Content.Shared.Chat.Prototypes;
using Content.Shared.Chemistry.Components;
using Content.Shared.Damage;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.FixedPoint;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Roles;
using Content.Shared.StatusIcon;
using Content.Shared._EinsteinEngines.Language;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Zombies;

[RegisterComponent, NetworkedComponent]
public sealed partial class ZombieComponent : Component
{
    /// <summary>
    ///     This is the forced language a zombie should have when they are zombified and try to speak.
    /// </summary>
    /// <remarks>
    ///     This is intended as a fallback to prevent zombies from using sign language or any other
    ///     language that bypasses accent filter, and it prevents them from understanding everything
    ///     else while being zombified.
    /// </remarks>
    [DataField]
    public ProtoId<LanguagePrototype> ForcedLanguage = "Xeno"; // Xeno until we make a zombie/ignorant language that is unobtainable.

    /// <summary>
    /// The baseline infection chance you have if you have no protective gear
    /// </summary>
    [DataField]
    public float BaseZombieInfectionChance = 0.75f;

    /// <summary>
    /// The minimum infection chance possible. This is simply to prevent
    /// being overly protected by bundling up.
    /// </summary>
    [DataField]
    public float MinZombieInfectionChance = 0.05f;

    /// <summary>
    /// How effective each resistance type on a piece of armor is. Using a damage specifier for this seems illegal.
    /// </summary>
    public DamageSpecifier ResistanceEffectiveness = new()
    {
        DamageDict = new ()
        {
            {"Slash", 1.0}, // Goobstation change
            {"Piercing", 0.5}, // Goobstation change
            {"Blunt", 0.2}, // Goobstation change
        }
    };

    [DataField]
    public float ZombieMovementSpeedDebuff = 0.70f;

    /// <summary>
    /// The skin color of the zombie
    /// </summary>
    [DataField]
    public Color SkinColor = new(0.45f, 0.51f, 0.29f);

    /// <summary>
    /// The eye color of the zombie
    /// </summary>
    [DataField]
    public Color EyeColor = new(0.96f, 0.13f, 0.24f);

    /// <summary>
    /// The attack arc of the zombie
    /// </summary>
    [DataField("attackArc")]
    public EntProtoId AttackAnimation = "WeaponArcBite";

    /// <summary>
    /// The role prototype of the zombie antag role
    /// </summary>
    [DataField]
    public ProtoId<AntagPrototype> ZombieRoleId = "Zombie";

    [DataField]
    public Dictionary<ProtoId<OrganCategoryPrototype>, OrganProfileData> BeforeZombifiedProfiles;

    [DataField]
    public Dictionary<ProtoId<OrganCategoryPrototype>, Dictionary<HumanoidVisualLayers, List<Marking>>> BeforeZombifiedMarkings;

    [DataField("emoteId")]
    public ProtoId<EmoteSoundsPrototype>? EmoteSoundsId = "Zombie";

    [DataField(customTypeSerializer:typeof(TimeOffsetSerializer))]
    public TimeSpan NextTick;

    [DataField("zombieStatusIcon")]
    public ProtoId<StatusIconPrototype> StatusIcon { get; set; } = "ZombieFaction";

    /// <summary>
    /// Healing each second
    /// </summary>
    [DataField]
    public DamageSpecifier PassiveHealing = new()
    {
        DamageDict = new () ///Changed to be higher for goob // CorvaxGoob edit
        {
            { "Blunt", -3 },
            { "Slash", -2 },
            { "Piercing", -2 },
            { "Heat", -2 },
            { "Shock", -2 }
        }
    };

    /// <summary>
    /// A multiplier applied to <see cref="PassiveHealing"/> when the entity is in critical condition.
    /// </summary>
    [DataField("passiveHealingCritMultiplier")]
    public float PassiveHealingCritMultiplier = 2.5f; ///Goobchange //CorvaxGoob edit 5 -> 2.5

    /// <summary>
    /// Healing given when a zombie bites a living being.
    /// </summary>
    [DataField]
    public DamageSpecifier HealingOnBite = new()
    {
        DamageDict = new() ///Changed to be higher for goob
        {
            { "Blunt", -25 },
            { "Slash", -25 },
            { "Piercing", -25 },
            { "Heat", -25 },
            { "Shock", -25 }
        }
    };

    /// <summary>
    /// The damage dealt on bite, dehardcoded for your enjoyment
    /// </summary>
    [DataField]
    public DamageSpecifier DamageOnBite = new()
    {
        DamageDict = new()
        {
            { "Slash", 13 },
            { "Piercing", 7 },
            { "Structural", 10 }
        }
    };

    /// <summary>
    ///     Path to antagonist alert sound.
    /// </summary>
    [DataField]
    public SoundSpecifier GreetSoundNotification = new SoundPathSpecifier("/Audio/Ambience/Antag/zombie_start.ogg");

    /// <summary>
    ///     Hit sound on zombie bite.
    /// </summary>
    [DataField]
    public SoundSpecifier BiteSound = new SoundPathSpecifier("/Audio/Effects/bite.ogg");

    /// <summary>
    /// The blood refresh of the humanoid to restore in case of cloning.
    /// </summary>
    [DataField]
    public FixedPoint2 BeforeZombifiedBloodRefresh = new();

    /// <summary>
    /// The blood reagents of the humanoid to restore in case of cloning
    /// </summary>
    [DataField]
    public Solution BeforeZombifiedBloodReagents = new();

    /// <summary>
    /// The blood reagents to give the zombie. In case you want zombies that bleed milk, or something.
    /// </summary>
    [DataField]
    public Solution NewBloodReagents = new([new("ZombieBlood", 1)]);

    [DataField]
    public EntityUid? BeforeZombificationReferenceEnt; // Goob - reference clone of before zombified
}
