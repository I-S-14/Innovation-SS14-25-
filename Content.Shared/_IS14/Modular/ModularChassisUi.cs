// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Atmos;
using Content.Shared.Inventory;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Modular;

[Serializable, NetSerializable]
public enum ModularChassisUiKey : byte
{
    Key,
}

/// <summary>
///     One module as the UI needs to see it.
/// </summary>
[Serializable, NetSerializable]
public sealed class ChassisModuleUiEntry
{
    public NetEntity Module;
    public string Name = string.Empty;
    public string Description = string.Empty;
    public ModuleKind Kind;
    public int Complexity;
    public float IdleDraw;
    public float ActiveDraw;
    public float UseCost;
    public bool Active;
    public bool Enabled;
    public bool Removable;

    /// <summary>
    ///     Host slots this module needs. The interface draws the matching pieces of the
    ///     shell under its icon, so "why is this dead?" is answerable without reading.
    /// </summary>
    public List<SlotFlags> RequiredSlots = new();

    /// <summary>Locale id overriding the generic button wording, or null.</summary>
    public string? ActionText;

    /// <summary>Texture path overriding the generic button icon, or null.</summary>
    public string? ActionIcon;

    /// <summary>Seconds left on cooldown, 0 when ready.</summary>
    public float Cooldown;

    /// <summary>Total cooldown length, so the UI can draw a progress fraction.</summary>
    public float CooldownMax;

    /// <summary>Why the module cannot be triggered, or <see cref="ModuleBlockReason.None"/>.</summary>
    public ModuleBlockReason BlockReason;

    public List<ModuleConfigEntry> Config = new();
}

/// <summary>
///     One suit part as the UI needs to see it. Empty for chassis that have no parts.
/// </summary>
[Serializable, NetSerializable]
public sealed class ChassisPartUiEntry
{
    public NetEntity Part;
    public string Name = string.Empty;
    public string Slot = string.Empty;

    /// <summary>Slot flag this piece contributes, used to pair modules with it.</summary>
    public SlotFlags SlotFlag;

    public bool Deployed;
    public bool Sealed;

    public float Integrity;
    public float MaxIntegrity;

    /// <summary>Fraction of <see cref="MaxIntegrity"/> at which this piece stops carrying modules.</summary>
    public float ModuleThreshold = 0.66f;

    /// <summary>Fraction at which it stops holding pressure at all.</summary>
    public float UnsealThreshold = 0.33f;

    /// <summary>Condition has dropped past the point where this piece carries modules.</summary>
    public bool Broken;

    /// <summary>Condition has dropped past the point where this piece can seal.</summary>
    public bool Ruptured;

    /// <summary>What servicing this piece is asking for.</summary>
    public ChassisPartFault Fault;
}

/// <summary>
///     One gas in the bottle, as a share of what is in there.
/// </summary>
[Serializable, NetSerializable]
public sealed class ChassisGasUiEntry
{
    public Gas Gas;
    public float Fraction;

    public ChassisGasUiEntry(Gas gas, float fraction)
    {
        Gas = gas;
        Fraction = fraction;
    }
}

[Serializable, NetSerializable]
public sealed class ModularChassisUiState : BoundUserInterfaceState
{
    /// <summary>Suit name, so the window titles itself with what you are wearing.</summary>
    public string ChassisName = string.Empty;

    /// <summary>Any part deployed at all — drives the deploy button's label.</summary>
    public bool AnyDeployed;

    /// <summary>ID lock state, or null when the chassis has no lock.</summary>
    public bool? Locked;

    /// <summary>Access has been wiped by the hacking wire or an emag.</summary>
    public bool AccessWiped;

    /// <summary>Wire sabotage broke the panel interface.</summary>
    public bool InterfaceBroken;

    /// <summary>The suit is electrified and will shock whoever handles it.</summary>
    public bool Electrified;

    public float Charge;
    public float MaxCharge;
    public string? CoreName;

    /// <summary>
    ///     The installed core itself, so the readout can draw the thing that is in there
    ///     rather than a picture of the one that usually is.
    /// </summary>
    public NetEntity? Core;

    /// <summary>Cell sitting in the installed core, when it is the kind that takes one.</summary>
    public string? CoreCellName;

    /// <summary>
    ///     The installed core runs off a swappable cell. Separates "no core" from "core
    ///     with nothing in it", which look identical from the charge readout alone.
    /// </summary>
    public bool CoreTakesCell;

    /// <summary>The installed core has a hopper the panel can open.</summary>
    public bool CoreHasHopper;

    /// <summary>Internal volume, when a module has given the chassis one.</summary>
    public bool TankPresent;
    public float TankPressure;
    public float TankTargetPressure;
    public bool TankPumping;

    /// <summary>What the compressor is set to keep, for the gauge's caption.</summary>
    public string TankContents = string.Empty;

    /// <summary>
    ///     The module the bottle belongs to. The gauge's switches are ordinary module
    ///     settings underneath, so it needs to know who to send them to.
    /// </summary>
    public NetEntity? TankModule;

    /// <summary>Compressor switch position, whether or not it can actually run.</summary>
    public bool TankPumpEnabled;

    /// <summary>
    ///     Whether the compressor is allowed to run — it needs the chestplate sealed,
    ///     which is the difference between a bottle you carry and a bottle you fill.
    /// </summary>
    public bool TankCanPump;

    /// <summary>Release valve, venting the bottle into the room.</summary>
    public bool TankValveOpen;

    /// <summary>Suit sealed tightly enough to breathe out of the bottle.</summary>
    public bool TankCanBreathe;

    /// <summary>The wearer's lungs are hooked up to it right now.</summary>
    public bool TankInternalsOn;

    /// <summary>Kelvin inside the bottle.</summary>
    public float TankTemperature;

    /// <summary>Nothing in there at all, in which case the temperature means nothing.</summary>
    public bool TankEmpty;

    /// <summary>What is actually in there, largest share first.</summary>
    public List<ChassisGasUiEntry> TankGases = new();

    public int UsedComplexity;
    public int MaxComplexity;

    public bool Active;
    public bool PanelOpen;
    public bool Malfunctioning;

    /// <summary>Total watts being drawn right now.</summary>
    public float Draw;

    public string WearerName = string.Empty;
    public string WearerJob = string.Empty;

    public NetEntity? SelectedModule;

    public List<ChassisModuleUiEntry> Modules = new();
    public List<ChassisPartUiEntry> Parts = new();
}

/// <summary>Toggle, use or select a module.</summary>
[Serializable, NetSerializable]
public sealed class ChassisSelectModuleMessage : BoundUserInterfaceMessage
{
    public NetEntity Module;

    public ChassisSelectModuleMessage(NetEntity module)
    {
        Module = module;
    }
}

/// <summary>Edit one of a module's settings.</summary>
[Serializable, NetSerializable]
public sealed class ChassisConfigureModuleMessage : BoundUserInterfaceMessage
{
    public NetEntity Module;
    public string Key;
    public object? Value;

    public ChassisConfigureModuleMessage(NetEntity module, string key, object? value)
    {
        Module = module;
        Key = key;
        Value = value;
    }
}

/// <summary>Take the cell out of the installed core.</summary>
[Serializable, NetSerializable]
public sealed class ChassisEjectCellMessage : BoundUserInterfaceMessage;

/// <summary>Put the cell in hand into the installed core.</summary>
[Serializable, NetSerializable]
public sealed class ChassisInsertCellMessage : BoundUserInterfaceMessage;

/// <summary>Open the fuel hopper on the installed core.</summary>
[Serializable, NetSerializable]
public sealed class ChassisOpenHopperMessage : BoundUserInterfaceMessage;

/// <summary>Pull a module out of the chassis.</summary>
[Serializable, NetSerializable]
public sealed class ChassisEjectModuleMessage : BoundUserInterfaceMessage
{
    public NetEntity Module;

    public ChassisEjectModuleMessage(NetEntity module)
    {
        Module = module;
    }
}

/// <summary>Deploy or retract one suit part.</summary>
[Serializable, NetSerializable]
public sealed class ChassisTogglePartMessage : BoundUserInterfaceMessage
{
    public NetEntity Part;

    public ChassisTogglePartMessage(NetEntity part)
    {
        Part = part;
    }
}

/// <summary>Seal or unseal one suit part on its own.</summary>
[Serializable, NetSerializable]
public sealed class ChassisSealPartMessage : BoundUserInterfaceMessage
{
    public NetEntity Part;

    public ChassisSealPartMessage(NetEntity part)
    {
        Part = part;
    }
}

/// <summary>Switch the whole chassis on or off.</summary>
[Serializable, NetSerializable]
public sealed class ChassisToggleActiveMessage : BoundUserInterfaceMessage;

/// <summary>Deploy or retract every part at once.</summary>
[Serializable, NetSerializable]
public sealed class ChassisToggleDeployMessage : BoundUserInterfaceMessage;
