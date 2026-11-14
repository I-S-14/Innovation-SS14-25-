// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// An upgrade board, the equivalent of tgstation's <c>/obj/item/borg/upgrade</c>.
/// Installed by someone else through the open cover, it stays inside the cyborg.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyborgUpgradeComponent : Component
{
    /// <summary>
    /// Models that accept this board. Empty means every model, including none.
    /// </summary>
    [DataField]
    public List<ProtoId<CyborgModelPrototype>> Models = new();

    /// <summary>
    /// Tools added to the cyborg's kit.
    /// </summary>
    [DataField]
    public List<EntProtoId> Tools = new();

    /// <summary>
    /// Basic tools of the model taken away while the board is installed, like tg's items_to_remove.
    /// Usually the ones <see cref="Tools"/> improve on.
    /// </summary>
    [DataField]
    public List<EntProtoId> Replaces = new();

    /// <summary>
    /// Components added to the cyborg while the board is installed.
    /// </summary>
    [DataField]
    public ComponentRegistry AddComponents = new();

    /// <summary>
    /// Unlocks the emag tools of the model, like tg's illegal equipment module.
    /// </summary>
    [DataField]
    public bool UnlockEmagTools;

    /// <summary>
    /// Takes the model away and is used up, like tg's reclassification board.
    /// </summary>
    [DataField]
    public bool ResetModel;

    /// <summary>
    /// Switches the cyborg to this model and is used up, like tg's borg model pickers.
    /// </summary>
    [DataField]
    public ProtoId<CyborgModelPrototype>? SetModel;

    /// <summary>
    /// Played on the cyborg when the board goes in.
    /// </summary>
    [DataField]
    public SoundSpecifier? InstallSound;
}
