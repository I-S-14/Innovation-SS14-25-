using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.OS.Prototypes;

/// <summary>
///     Which stock PDA a job's loadout should actually hand out.
///
///     Keyed on the PDA the starting gear already gives rather than on the job, so one table
///     covers every role at once — including roles added later — and no upstream loadout file
///     has to be touched. Anything left out of the table keeps its stock PDA, which is how
///     antagonist and off-station kit stays untouched.
/// </summary>
[Prototype("is14PdaSwap")]
public sealed partial class IS14PdaSwapPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public Dictionary<EntProtoId, EntProtoId> Swaps = new();
}
