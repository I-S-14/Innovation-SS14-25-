using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.Payroll;

/// <summary>Head request to change a subordinate's recurring salary.</summary>
[Serializable, NetSerializable]
public sealed class PayrollSetSalaryMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Employee;

    public readonly int Salary;

    public PayrollSetSalaryMessage(NetEntity employee, int salary)
    {
        Employee = employee;
        Salary = salary;
    }
}

/// <summary>Head request to pay a one-off bonus from the department fund.</summary>
[Serializable, NetSerializable]
public sealed class PayrollBonusMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Employee;

    public readonly int Amount;

    public PayrollBonusMessage(NetEntity employee, int amount)
    {
        Employee = employee;
        Amount = amount;
    }
}

/// <summary>Head request to fine a subordinate; the money goes back into the department fund.</summary>
[Serializable, NetSerializable]
public sealed class PayrollFineMessage : BoundUserInterfaceMessage
{
    public readonly NetEntity Employee;

    public readonly int Amount;

    public PayrollFineMessage(NetEntity employee, int amount)
    {
        Employee = employee;
        Amount = amount;
    }
}
