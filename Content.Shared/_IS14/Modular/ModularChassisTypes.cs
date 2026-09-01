// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Modular;

/// <summary>
///     How a module behaves once it is installed in a chassis.
/// </summary>
[Serializable, NetSerializable]
public enum ModuleKind : byte
{
    /// <summary>
    ///     Works for as long as it is installed and its required slots are available.
    ///     Has no on/off control at all.
    /// </summary>
    Passive = 0,

    /// <summary>
    ///     Can be switched on and off. Draws <see cref="ChassisModuleComponent.ActiveDraw"/> while on.
    ///     Any number of these can be on at once.
    /// </summary>
    Toggleable = 1,

    /// <summary>
    ///     Fires once per activation, spends <see cref="ChassisModuleComponent.UseCost"/>
    ///     and then goes on cooldown.
    /// </summary>
    Usable = 2,

    /// <summary>
    ///     Only one active module per chassis at a time. Either hands the user a device
    ///     entity or arms a special click on a target.
    /// </summary>
    Active = 3,
}

/// <summary>
///     Circumstances in which a module is allowed to run, beyond the default
///     "chassis is active and worn".
/// </summary>
[Flags]
[Serializable, NetSerializable]
public enum ModuleAllowFlags : byte
{
    None = 0,

    /// <summary>Usable while the chassis is not equipped by anyone.</summary>
    Unworn = 1 << 0,

    /// <summary>Usable while the chassis itself is switched off.</summary>
    ChassisInactive = 1 << 1,

    /// <summary>Usable while the wearer is stunned, cuffed or otherwise incapacitated.</summary>
    Incapacitated = 1 << 2,
}

/// <summary>
///     Why a module cannot be used right now. Sent to the UI so it can explain
///     a disabled button instead of just greying it out.
/// </summary>
[Serializable, NetSerializable]
public enum ModuleBlockReason : byte
{
    None = 0,
    NoPower = 1,
    Cooldown = 2,
    MissingParts = 3,
    ChassisInactive = 4,
    NotWorn = 5,
    Incapacitated = 6,
    Malfunctioning = 7,
}

/// <summary>
///     What is actually wrong with a piece of a chassis' shell, which decides how it is
///     put right. Derived from what hurt it rather than rolled: plating beaten with a
///     crowbar needs working back out, plating cooked by lasers needs its loom re-run.
/// </summary>
[Serializable, NetSerializable]
public enum ChassisPartFault : byte
{
    /// <summary>Nothing to fix.</summary>
    None = 0,

    /// <summary>Bent plating. Welder.</summary>
    Structural = 1,

    /// <summary>Burnt-through wiring. Cable coil.</summary>
    Electrical = 2,
}

/// <summary>
///     Kinds of configuration control a module can expose to the UI.
///     The UI renders these generically and never needs to know which module it is talking to.
/// </summary>
[Serializable, NetSerializable]
public enum ModuleConfigKind : byte
{
    Bool = 0,
    Number = 1,
    Color = 2,
    Choice = 3,
    Button = 4,
}

/// <summary>
///     One configurable setting exposed by a module.
/// </summary>
[Serializable, NetSerializable]
public sealed class ModuleConfigEntry
{
    public string Key { get; }
    public string Label { get; }
    public ModuleConfigKind Kind { get; }

    /// <summary>
    ///     Current value. Meaning depends on <see cref="Kind"/>:
    ///     bool for <see cref="ModuleConfigKind.Bool"/>, float for Number,
    ///     Color for Color, string for Choice, null for Button.
    /// </summary>
    public object? Value { get; }

    /// <summary>Valid options, only meaningful for <see cref="ModuleConfigKind.Choice"/>.</summary>
    public string[]? Choices { get; }

    /// <summary>Inclusive bounds, only meaningful for <see cref="ModuleConfigKind.Number"/>.</summary>
    public float Min { get; }
    public float Max { get; }

    /// <summary>
    ///     Granularity of a <see cref="ModuleConfigKind.Number"/>. Zero is continuous.
    ///     A dial that only means anything in whole degrees should refuse to sit between
    ///     them rather than quietly rounding behind the player's back.
    /// </summary>
    public float Step { get; }

    public ModuleConfigEntry(
        string key,
        string label,
        ModuleConfigKind kind,
        object? value = null,
        string[]? choices = null,
        float min = 0f,
        float max = 0f,
        float step = 0f)
    {
        Key = key;
        Label = label;
        Kind = kind;
        Value = value;
        Choices = choices;
        Min = min;
        Max = max;
        Step = step;
    }
}
