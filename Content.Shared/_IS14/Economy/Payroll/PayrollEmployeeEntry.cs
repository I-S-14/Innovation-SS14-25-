using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.Payroll;

/// <summary>One subordinate row shown in the payroll console.</summary>
[Serializable, NetSerializable]
public sealed class PayrollEmployeeEntry
{
    /// <summary>Crew member entity carrying the <see cref="JobSalaryComponent"/>.</summary>
    public readonly NetEntity Employee;

    public readonly string Name;

    public readonly string JobTitle;

    /// <summary>Salary currently paid each interval.</summary>
    public readonly int Salary;

    /// <summary>Salary the job started with, shown as a reference point for the head.</summary>
    public readonly int BaseSalary;

    /// <summary>Lowest salary the head may set on this employee.</summary>
    public readonly int MinSalary;

    /// <summary>Highest salary the head may set on this employee.</summary>
    public readonly int MaxSalary;

    /// <summary>Unpaid salary accumulated while the fund was empty.</summary>
    public readonly int OwedSalary;

    public PayrollEmployeeEntry(
        NetEntity employee,
        string name,
        string jobTitle,
        int salary,
        int baseSalary,
        int minSalary,
        int maxSalary,
        int owedSalary)
    {
        Employee = employee;
        Name = name;
        JobTitle = jobTitle;
        Salary = salary;
        BaseSalary = baseSalary;
        MinSalary = minSalary;
        MaxSalary = maxSalary;
        OwedSalary = owedSalary;
    }
}
