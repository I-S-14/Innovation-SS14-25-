// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._IS14.Economy.Gosplan.Units;

/// <summary>
/// How a quota's raw numbers are written out on the soc-competition board. Chosen per
/// quota in YAML with <c>!type:</c>, the same way the metric is — a new unit is a new
/// class here and nothing else.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class PlanQuotaUnit
{
    /// <summary>Renders one value the way the board should show it.</summary>
    public abstract string Format(float value);
}

/// <summary>A 0..1 ratio shown as a percentage.</summary>
public sealed partial class PercentQuotaUnit : PlanQuotaUnit
{
    public override string Format(float value) =>
        Loc.GetString("is14-gosplan-unit-percent", ("value", (int)MathF.Round(value * 100f)));
}

/// <summary>Credits.</summary>
public sealed partial class CreditsQuotaUnit : PlanQuotaUnit
{
    public override string Format(float value) =>
        Loc.GetString("is14-gosplan-unit-credits", ("value", (int)MathF.Round(value)));
}

/// <summary>A plain count of things done.</summary>
public sealed partial class CountQuotaUnit : PlanQuotaUnit
{
    public override string Format(float value) =>
        ((int)MathF.Round(value)).ToString();
}
