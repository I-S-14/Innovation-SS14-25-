using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.Fines;

/// <summary>One crew member an officer can write a fine against.</summary>
[Serializable, NetSerializable]
public sealed class FineTargetEntry
{
    public uint RecordId;
    public string Name = string.Empty;
    public string JobTitle = string.Empty;

    /// <summary>Credits this person currently owes across all unpaid fines.</summary>
    public int OutstandingAmount;

    public FineTargetEntry()
    {
    }

    public FineTargetEntry(uint recordId, string name, string jobTitle, int outstandingAmount)
    {
        RecordId = recordId;
        Name = name;
        JobTitle = jobTitle;
        OutstandingAmount = outstandingAmount;
    }
}

/// <summary>An article of the station code as offered in the officer's list.</summary>
[Serializable, NetSerializable]
public sealed class FineArticleEntry
{
    public string PresetId = string.Empty;
    public string Name = string.Empty;
    public int Amount;

    public FineArticleEntry()
    {
    }

    public FineArticleEntry(string presetId, string name, int amount)
    {
        PresetId = presetId;
        Name = name;
        Amount = amount;
    }
}

[Serializable, NetSerializable]
public sealed class FineCartridgeUiState : BoundUserInterfaceState
{
    public readonly List<FineTargetEntry> Targets;
    public readonly List<FineArticleEntry> Articles;

    /// <summary>Every fine on the station, newest first.</summary>
    public readonly List<FineRecord> Fines;

    /// <summary>Server-side cap on a single fine.</summary>
    public readonly int MaxAmount;

    public readonly string Status;

    public FineCartridgeUiState(
        List<FineTargetEntry> targets,
        List<FineArticleEntry> articles,
        List<FineRecord> fines,
        int maxAmount,
        string status)
    {
        Targets = targets;
        Articles = articles;
        Fines = fines;
        MaxAmount = maxAmount;
        Status = status;
    }
}
