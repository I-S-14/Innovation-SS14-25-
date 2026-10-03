// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Explosion;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Ordnance;

/// <summary>
/// Turns an unwrenched manual valve into the tank transfer valve: two gas tanks on one valve, and
/// opening it dumps one into the other.
/// </summary>
/// <remarks>
/// It rides on the ordinary pipe valve rather than being its own device, so a bomb is built out of
/// station stock: unwrench a valve, hang two tanks on it, open it. While the valve is anchored in a
/// pipe none of this exists — no tanks go in, no timer is fitted — and it behaves exactly as
/// upstream's.
/// <para/>
/// The yield comes entirely from what the department mixed — how hot the gas is, how much fuel the
/// oxidiser can reach, and whether that fuel is plasma or tritium — because a bomb should be a
/// chemistry problem rather than a crafting recipe. <c>IS14TransferValveSystem.GetYield</c> is
/// where that mixture becomes a number, and why it is not left to the tank's own burst rules.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IS14TransferValveComponent : Component
{
    /// <summary>Slot holding the tank that receives everything when the valve opens.</summary>
    [DataField]
    public string ReceiverSlot = "is14_valve_tank_a";

    /// <summary>Slot holding the tank that is emptied into the other one.</summary>
    [DataField]
    public string DonorSlot = "is14_valve_tank_b";

    /// <summary>
    /// Trigger keys that open the valve. The timer's output key by default, so the assembly waits
    /// out its countdown instead of dumping the tanks the moment somebody opens it.
    /// </summary>
    [DataField]
    public HashSet<string> KeysIn = new() { "timer" };

    /// <summary>
    /// Slot for the trigger the valve is opened by. Empty means there is no timer to speak of and
    /// the valve opens the moment somebody opens it, which is all a bare valve can do.
    /// </summary>
    [DataField]
    public string TriggerSlot = "is14_valve_trigger";

    /// <summary>True once the valve has been opened. A valve only fires once.</summary>
    [DataField, AutoNetworkedField]
    public bool Open;

    [DataField]
    public SoundSpecifier? OpenSound = new SoundPathSpecifier("/Audio/Effects/refill.ogg");

    /// <summary>
    /// Joules of burnt fuel per point of explosion intensity — the one dial between what the
    /// department mixed and how big the hole is.
    /// </summary>
    [DataField]
    public float EnergyPerIntensity = 400f;

    /// <summary>Ceiling on the yield, so one perfect mix cannot take the station with it.</summary>
    [DataField]
    public float MaxIntensity = 1500f;

    [DataField]
    public float IntensitySlope = 5f;

    [DataField]
    public float MaxTileIntensity = 25f;

    [DataField]
    public ProtoId<ExplosionPrototype> ExplosionType = "Default";
}

/// <summary>What the client needs to know to draw the assembly.</summary>
[Serializable, NetSerializable]
public enum IS14TransferValveVisuals : byte
{
    /// <summary>How many tanks are on the valve: 0, 1 or 2.</summary>
    Tanks,

    /// <summary>Whether a trigger is wired to it.</summary>
    Trigger,
}

/// <summary>
/// Sprite layers of the assembly. A loaded valve stops looking like plumbing and takes the whole
/// transfer-valve icon instead, rather than wearing tanks as a pile of overlays.
/// </summary>
[Serializable, NetSerializable]
public enum IS14TransferValveVisualLayers : byte
{
    Assembly,
    Trigger,
}
