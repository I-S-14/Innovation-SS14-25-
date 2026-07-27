using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.Payroll;

/// <summary>How a payroll action turned out, used to colour the console's log tab.</summary>
[Serializable, NetSerializable]
public enum PayrollLogKind : byte
{
    /// <summary>Money paid out or pay raised.</summary>
    Positive,

    /// <summary>Money collected or pay cut.</summary>
    Negative,

    /// <summary>The console refused the action.</summary>
    Denied,
}

/// <summary>One line in the payroll console's local action log.</summary>
[Serializable, NetSerializable]
public sealed class PayrollLogEntry
{
    /// <summary>Round time the action happened at.</summary>
    public readonly TimeSpan Timestamp;

    /// <summary>Localized description of the action.</summary>
    public readonly string Message;

    public readonly PayrollLogKind Kind;

    public PayrollLogEntry(TimeSpan timestamp, string message, PayrollLogKind kind)
    {
        Timestamp = timestamp;
        Message = message;
        Kind = kind;
    }
}
