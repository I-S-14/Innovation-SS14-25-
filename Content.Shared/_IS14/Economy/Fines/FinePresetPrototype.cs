using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Economy.Fines;

/// <summary>
/// A chargeable offence with a standard penalty. Officers pick one instead of
/// inventing a number, which keeps fines predictable enough to argue about.
/// </summary>
[Prototype("finePreset")]
public sealed partial class FinePresetPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>Name of the article as it appears on the fine.</summary>
    [DataField(required: true)]
    public LocId Name;

    /// <summary>Standard penalty in credits. The officer may adjust it within the server limit.</summary>
    [DataField(required: true)]
    public int Amount;

    /// <summary>Display order in the officer's list. Lower comes first.</summary>
    [DataField]
    public int Order;
}
