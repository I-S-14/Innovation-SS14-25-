// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs.Prototypes;
using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Cyborgs;

[Serializable, NetSerializable]
public enum CyborgUiKey : byte
{
    /// <summary>
    /// Window for picking a model and its skin.
    /// </summary>
    Model,

    /// <summary>
    /// The model window locked to the current model, for picking a skin only.
    /// </summary>
    Skin,

    /// <summary>
    /// Window others open through the cover: brain, cell and upgrade boards.
    /// </summary>
    Maintenance,
}

[Serializable, NetSerializable]
public sealed class CyborgMaintenanceBuiState(
    string? brain,
    float? charge,
    string? model,
    List<(NetEntity Board, string Name)> upgrades) : BoundUserInterfaceState
{
    public string? Brain = brain;

    /// <summary>
    /// Cell charge from 0 to 1, null without a cell.
    /// </summary>
    public float? Charge = charge;

    public string? Model = model;

    public List<(NetEntity Board, string Name)> Upgrades = upgrades;
}

[Serializable, NetSerializable]
public sealed class CyborgMaintenanceEjectBrainMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CyborgMaintenanceEjectCellMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class CyborgMaintenanceRemoveUpgradeMessage(NetEntity board) : BoundUserInterfaceMessage
{
    public NetEntity Board = board;
}

[Serializable, NetSerializable]
public sealed class CyborgSelectModelMessage(
    ProtoId<CyborgModelPrototype> model,
    ProtoId<CyborgSkinPrototype>? skin = null) : BoundUserInterfaceMessage
{
    public ProtoId<CyborgModelPrototype> Model = model;

    /// <summary>
    /// Skin picked in the same window. Without one the skin menu opens afterwards.
    /// </summary>
    public ProtoId<CyborgSkinPrototype>? Skin = skin;
}

[Serializable, NetSerializable]
public sealed class CyborgSelectSkinMessage(ProtoId<CyborgSkinPrototype> skin) : BoundUserInterfaceMessage
{
    public ProtoId<CyborgSkinPrototype> Skin = skin;
}

/// <summary>
/// Sent from the cyborg HUD: takes a stored tool out into a free slot, or puts a held tool back.
/// </summary>
[Serializable, NetSerializable]
public sealed class CyborgToggleToolRequest(NetEntity tool) : EntityEventArgs
{
    public NetEntity Tool = tool;
}

/// <summary>
/// Sent from the cyborg HUD: puts the tool of a slot back into storage. Null means the active slot.
/// </summary>
[Serializable, NetSerializable]
public sealed class CyborgStoreToolRequest(string? hand) : EntityEventArgs
{
    public string? Hand = hand;
}

/// <summary>
/// Sent from the cyborg HUD when a cyborg without a model presses the model button.
/// </summary>
[Serializable, NetSerializable]
public sealed class CyborgOpenModelMenuRequest : EntityEventArgs;

[Serializable, NetSerializable]
public enum CyborgReagentUiKey : byte
{
    Key,
}

/// <summary>
/// Picks the reagent a cyborg hypospray or shaker synthesizes.
/// </summary>
[Serializable, NetSerializable]
public sealed class CyborgSelectReagentMessage(ProtoId<ReagentPrototype> reagent) : BoundUserInterfaceMessage
{
    public ProtoId<ReagentPrototype> Reagent = reagent;
}

/// <summary>
/// Sent from the cyborg HUD lamp button.
/// </summary>
[Serializable, NetSerializable]
public sealed class CyborgToggleLampRequest : EntityEventArgs;

[Serializable, NetSerializable]
public enum CyborgVisuals : byte
{
    HasMind,
    HasBrain,
}

[Serializable, NetSerializable]
public enum CyborgVisualLayers : byte
{
    Body,
    Eyes,
    Light,
}

[Serializable, NetSerializable]
public enum CyborgDispenserUiKey : byte
{
    Key,
}

/// <summary>
/// Picks what a cyborg dispenser prints, sent from its radial menu.
/// </summary>
[Serializable, NetSerializable]
public sealed class CyborgDispenserSelectMessage(int index) : BoundUserInterfaceMessage
{
    public int Index = index;
}

/// <summary>
/// Sent from the cyborg HUD: opens or closes the built-in OS device.
/// </summary>
[Serializable, NetSerializable]
public sealed class CyborgToggleOsRequest : EntityEventArgs;
