// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Radio;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Research;

/// <summary>
/// Tachyon-doppler array: measures explosions at a distance and keeps a log of them.
/// </summary>
/// <remarks>
/// Rebuilt after tgstation's array, which is a far better machine than a flat payout box:
/// it only sees blasts in the direction it faces, it prints a numbered record with the numbers
/// it measured, and the department is paid for **beating its own record** rather than for
/// detonating the same charge over and over.
/// </remarks>
[RegisterComponent]
public sealed partial class IS14DopplerArrayComponent : Component
{
    [DataField]
    public ProtoId<ResearchPointTypePrototype> PointType = "Military";

    /// <summary>Payout for a record-breaking blast of exactly <see cref="ReferenceIntensity"/>.</summary>
    [DataField]
    public int BaseValue = 40;

    /// <summary>
    /// Blasts closer than this are inside the station: logged with a warning, never paid.
    /// Blowing up the corridors for data has to stay a bad idea mechanically, not just legally.
    /// </summary>
    [DataField]
    public float MinDistance = 8f;

    [DataField]
    public float MaxDistance = 120f;

    /// <summary>
    /// Half-angle of the sensor cone, in degrees. The array is directional exactly like tg's:
    /// point it at the test site, or it measures nothing at all.
    /// </summary>
    [DataField]
    public float FieldOfView = 50f;

    /// <summary>Anything weaker is noise.</summary>
    [DataField]
    public float MinIntensity = 5f;

    /// <summary>Intensity that pays exactly <see cref="BaseValue"/> when it sets a record.</summary>
    [DataField]
    public float ReferenceIntensity = 60f;

    /// <summary>Cap on intensity scaling, so one enormous bomb is not a whole branch.</summary>
    [DataField]
    public float MaxScale = 3f;

    /// <summary>
    /// Share of the payout a blast gets when it does not beat the station's record. Small on
    /// purpose: repeating a known charge is a confirmation, not a discovery.
    /// </summary>
    [DataField]
    public float RepeatFraction = 0.1f;

    /// <summary>Where readouts are announced. The array usually works with nobody next to it.</summary>
    [DataField]
    public ProtoId<RadioChannelPrototype>? Channel = "Science";

    /// <summary>Log of everything this array has measured, newest last.</summary>
    [DataField]
    public List<IS14OrdnanceRecord> Records = new();

    /// <summary>Oldest records are dropped past this, so the log cannot grow forever.</summary>
    [DataField]
    public int MaxRecords = 24;

    /// <summary>Running number of the next record.</summary>
    [DataField]
    public int RecordNumber = 1;

    /// <summary>Printed protocol of a record — paper, so it can be stamped and filed.</summary>
    [DataField]
    public EntProtoId ProtocolPrototype = "Paper";
}

/// <summary>
/// The station's strongest measured blast. What the array pays against: research money is in
/// beating it, not in detonating the same charge again.
/// </summary>
[RegisterComponent]
public sealed partial class IS14OrdnanceRecordsComponent : Component
{
    [DataField]
    public float BestIntensity;
}

/// <summary>One measured explosion, as the array logged it.</summary>
[DataDefinition, Serializable, NetSerializable]
public partial struct IS14OrdnanceRecord
{
    [DataField]
    public int Number;

    /// <summary>Station time of the reading, already formatted.</summary>
    [DataField]
    public string Timestamp = string.Empty;

    /// <summary>Grid coordinates of the epicentre, kept for the printed protocol.</summary>
    [DataField]
    public string Coordinates = string.Empty;

    /// <summary>
    /// Nearest station beacon to the epicentre. A place name is what the department actually
    /// needs off the radio; raw grid numbers mean nothing while you are running to the site.
    /// </summary>
    [DataField]
    public string Location = string.Empty;

    [DataField]
    public float TotalIntensity;

    [DataField]
    public float Slope;

    [DataField]
    public float MaxTileIntensity;

    /// <summary>Estimated radius of total destruction, in tiles.</summary>
    [DataField]
    public float EpicenterRadius;

    /// <summary>Estimated radius of heavy damage.</summary>
    [DataField]
    public float OuterRadius;

    /// <summary>Estimated radius the shockwave reached at all.</summary>
    [DataField]
    public float ShockwaveRadius;

    /// <summary>Distance from the array to the epicentre.</summary>
    [DataField]
    public float Distance;

    /// <summary>Research data this reading paid out.</summary>
    [DataField]
    public int Payout;

    /// <summary>True when the blast beat the station's previous best of its type.</summary>
    [DataField]
    public bool Record;

    /// <summary>True when it went off too close to be counted.</summary>
    [DataField]
    public bool TooClose;
}

[Serializable, NetSerializable]
public enum IS14DopplerArrayUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class IS14DopplerArrayUiState : BoundUserInterfaceState
{
    public List<IS14OrdnanceRecord> Records = new();

    /// <summary>Station's strongest reading so far: what the next shot has to beat.</summary>
    public float BestIntensity;

    /// <summary>Which way the array is pointing, in words.</summary>
    public string Facing = string.Empty;
}

[Serializable, NetSerializable]
public sealed class IS14DopplerPrintRecordMessage : BoundUserInterfaceMessage
{
    public readonly int Number;

    public IS14DopplerPrintRecordMessage(int number)
    {
        Number = number;
    }
}

[Serializable, NetSerializable]
public sealed class IS14DopplerDeleteRecordMessage : BoundUserInterfaceMessage
{
    public readonly int Number;

    public IS14DopplerDeleteRecordMessage(int number)
    {
        Number = number;
    }
}
