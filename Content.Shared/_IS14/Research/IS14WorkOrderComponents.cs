// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Radio;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Research;

/// <summary>
/// A department's terminal for commissioning research: the head picks a topic and signs for it,
/// and when the NIC delivers, the department pays in data and in credits.
/// </summary>
/// <remarks>
/// This is the service economy written down. Up to now the departments were donors by
/// accident — their instruments happened to produce data the NIC could spend. An order makes
/// the trade explicit and, more importantly, two-way: engineering says what it wants, science
/// gets paid for delivering it, and both have a reason to talk mid-shift.
/// </remarks>
[RegisterComponent]
public sealed partial class IS14WorkOrderConsoleComponent : Component
{
    /// <summary>Which department this terminal speaks for. One open order per department.</summary>
    [DataField(required: true)]
    public string Department = string.Empty;

    /// <summary>Player-facing name of the department.</summary>
    [DataField(required: true)]
    public LocId DepartmentName;

    /// <summary>Station account the payment comes out of.</summary>
    [DataField]
    public string Account = "StationTreasury";

    /// <summary>Currency the department pays its bonus in — its own kind of data.</summary>
    [DataField(required: true)]
    public ProtoId<ResearchPointTypePrototype> PointType;

    /// <summary>Bonus data the NIC gets for delivering the order.</summary>
    [DataField]
    public int Bonus = 60;

    /// <summary>Credits moved from the department's budget to the science budget.</summary>
    [DataField]
    public int Payment = 2000;

    [DataField]
    public ProtoId<RadioChannelPrototype>? Channel = "Science";
}

/// <summary>Open orders of a station, by department. Filled in by the department terminals.</summary>
[RegisterComponent]
public sealed partial class IS14WorkOrdersComponent : Component
{
    /// <summary>Department to the technology it is waiting for.</summary>
    [DataField]
    public Dictionary<string, string> Orders = new();
}

/// <summary>
/// Raised on the station when a technology is bought at a NIC console, so anything that cares —
/// work orders now, quotas and publications later — hears about it without being wired in.
/// </summary>
[ByRefEvent]
public readonly record struct IS14TechnologyUnlockedEvent(
    EntityUid Station,
    string TechnologyId,
    EntityUid Console);

[Serializable, NetSerializable]
public enum IS14WorkOrderUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class IS14WorkOrderUiState : BoundUserInterfaceState
{
    public string Department = string.Empty;

    public string DepartmentName = string.Empty;

    /// <summary>Technology this department is waiting for, if any.</summary>
    public string? Order;

    /// <summary>Technologies already researched: they cannot be ordered.</summary>
    public List<string> Unlocked = new();

    /// <summary>Branches the terminal offers, matching the NIC console's own list.</summary>
    public List<string> Branches = new();

    public int Bonus;

    public int Payment;

    public string PointType = string.Empty;

    /// <summary>What the department has in the bank, for the header.</summary>
    public int Budget;
}

[Serializable, NetSerializable]
public sealed class IS14WorkOrderPlaceMessage : BoundUserInterfaceMessage
{
    public readonly string TechnologyId;

    public IS14WorkOrderPlaceMessage(string technologyId)
    {
        TechnologyId = technologyId;
    }
}

[Serializable, NetSerializable]
public sealed class IS14WorkOrderCancelMessage : BoundUserInterfaceMessage;
