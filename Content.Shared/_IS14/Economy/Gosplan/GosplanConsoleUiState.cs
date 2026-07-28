using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.Gosplan;

/// <summary>One quota as shown on the soc-competition board.</summary>
[Serializable, NetSerializable]
public sealed class PlanQuotaEntry
{
    public string QuotaId = string.Empty;
    public string Name = string.Empty;
    public string Description = string.Empty;

    /// <summary>Station account prototype ID, used by the board to colour the department.</summary>
    public string FundId = string.Empty;

    public string FundName = string.Empty;

    /// <summary>
    /// Where the department is right now, already written in the quota's own unit —
    /// the unit knows how to render itself and lives server-side with the prototype.
    /// </summary>
    public string CurrentText = string.Empty;

    /// <summary>What counts as 100%, in the same unit.</summary>
    public string TargetText = string.Empty;

    /// <summary>Current / Target, uncapped so the board can show 130%.</summary>
    public float Fulfillment;

    /// <summary>Fulfillment scored at the end of the previous period, or null on the first one.</summary>
    public float? LastFulfillment;

    /// <summary>Consecutive failed periods. Two means sanctions have already landed.</summary>
    public int FailStreak;

    /// <summary>Credits the fund would be paid if the period were scored right now.</summary>
    public int ProjectedPayout;

    /// <summary>Credits the fund is paid at exactly 100%.</summary>
    public int FullPayout;
}

/// <summary>A finished plan period, kept for the board's history tab.</summary>
[Serializable, NetSerializable]
public sealed class PlanPeriodEntry
{
    public int PeriodIndex;

    /// <summary>Department that scored highest, or empty if nobody did.</summary>
    public string LeaderFundName = string.Empty;

    /// <summary>Average fulfillment across every quota, 0..n.</summary>
    public float AverageFulfillment;

    /// <summary>Total credits Gosplan paid the station for this period.</summary>
    public int TotalPayout;

    /// <summary>Departments that fell below the failure threshold.</summary>
    public List<string> FailedFunds = new();

    public PlanPeriodEntry()
    {
    }

    public PlanPeriodEntry(int periodIndex, string leaderFundName, float averageFulfillment, int totalPayout, List<string> failedFunds)
    {
        PeriodIndex = periodIndex;
        LeaderFundName = leaderFundName;
        AverageFulfillment = averageFulfillment;
        TotalPayout = totalPayout;
        FailedFunds = failedFunds;
    }
}

[Serializable, NetSerializable]
public sealed class GosplanConsoleUiState : BoundUserInterfaceState
{
    /// <summary>False when the plan is switched off or the station has no economy.</summary>
    public bool Active;

    /// <summary>1-based number of the period currently being worked.</summary>
    public int PeriodIndex;

    /// <summary>Seconds until the current period is scored.</summary>
    public int SecondsLeft;

    /// <summary>Full length of a period, so the board can show how much of it is gone.</summary>
    public int PeriodSeconds;

    public List<PlanQuotaEntry> Quotas = new();
    public List<PlanPeriodEntry> History = new();

    /// <summary>Department holding the red banner, or empty if it hasn't been awarded yet.</summary>
    public string BannerFundName = string.Empty;

    /// <summary>Period the banner was last awarded for. Zero while nobody holds it.</summary>
    public int BannerPeriodIndex;

    /// <summary>Bonus pool top-up that comes with the banner.</summary>
    public int BannerBonus;

    /// <summary>Fulfillment below this counts as a failed quota. Marked on every gauge.</summary>
    public float FailThreshold = 0.5f;

    /// <summary>Fulfillment that counts as overfulfillment and competes for the banner.</summary>
    public float OverThreshold = 1f;

    /// <summary>Highest fulfillment that still earns extra credits — the end of every gauge.</summary>
    public float PayoutCap = 1.5f;
}
