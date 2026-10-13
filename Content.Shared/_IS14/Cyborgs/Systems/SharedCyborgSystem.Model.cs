// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared._IS14.Cyborgs.Prototypes;
using Content.Shared.Database;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Cyborgs.Systems;

public abstract partial class SharedCyborgSystem
{
    private void InitializeModel()
    {
        Subs.BuiEvents<CyborgComponent>(CyborgUiKey.Model, subs =>
        {
            subs.Event<CyborgSelectModelMessage>(OnSelectModelMessage);
        });
        Subs.BuiEvents<CyborgComponent>(CyborgUiKey.Skin, subs =>
        {
            subs.Event<CyborgSelectSkinMessage>(OnSelectSkinMessage);
        });
    }

    private void OnSelectModelMessage(Entity<CyborgComponent> ent, ref CyborgSelectModelMessage args)
    {
        // Spawning the tools is server only, so is the whole selection.
        if (_net.IsClient)
            return;

        if (args.Actor != ent.Owner || ent.Comp.Model != null)
            return;

        if (!_proto.TryIndex(args.Model, out var model) || !model.Selectable)
            return;

        _ui.CloseUi(ent.Owner, CyborgUiKey.Model);
        ent.Comp.CanOpenSelfUi = false;

        ApplyModel(ent, args.Model);
        _adminLog.Add(LogType.Action, LogImpact.Low,
            $"{ToPrettyString(ent.Owner):player} picked cyborg model {args.Model}");

        if (args.Skin is { } skin && model.Skins.Contains(skin))
        {
            ent.Comp.SkinChosen = true;
            SetSkin(ent, skin);
        }
        else if (model.Skins.Count > 1)
            OpenSkinMenu(ent);
    }

    private void OnSelectSkinMessage(Entity<CyborgComponent> ent, ref CyborgSelectSkinMessage args)
    {
        if (_net.IsClient)
            return;

        if (args.Actor != ent.Owner)
            return;

        if (!_proto.TryIndex(ent.Comp.Model, out var model) || !model.Skins.Contains(args.Skin))
            return;

        _ui.CloseUi(ent.Owner, CyborgUiKey.Skin);
        ent.Comp.CanOpenSelfUi = false;
        ent.Comp.SkinChosen = true;
        SetSkin(ent, args.Skin);
    }

    public void OpenModelMenu(Entity<CyborgComponent> ent)
    {
        ent.Comp.CanOpenSelfUi = true;
        _ui.TryOpenUi(ent.Owner, CyborgUiKey.Model, ent.Owner);
    }

    public void OpenSkinMenu(Entity<CyborgComponent> ent)
    {
        ent.Comp.CanOpenSelfUi = true;
        _ui.TryOpenUi(ent.Owner, CyborgUiKey.Skin, ent.Owner);
    }

    /// <summary>
    /// Switches the cyborg to a model, replacing the tools and traits of the previous one.
    /// Server only, since it spawns the tools.
    /// </summary>
    public void ApplyModel(Entity<CyborgComponent> ent, ProtoId<CyborgModelPrototype> modelId, bool force = false)
    {
        if (_net.IsClient)
            return;

        if (!_proto.TryIndex(modelId, out var model))
        {
            Log.Error($"Unknown cyborg model {modelId} for {ToPrettyString(ent.Owner)}");
            return;
        }

        if (!force && ent.Comp.Model == modelId)
            return;

        ClearModel(ent);

        ent.Comp.Model = modelId;
        ent.Comp.Skin = model.Skins.Count > 0 ? model.Skins[0] : (ProtoId<CyborgSkinPrototype>?) null;

        EntityManager.AddComponents(ent.Owner, model.AddComponents);

        foreach (var proto in model.BasicTools)
        {
            SpawnTool(ent, proto, emagTool: false);
        }

        foreach (var proto in model.EmagTools)
        {
            SpawnTool(ent, proto, emagTool: true);
        }

        ReapplyUpgrades(ent);
        UpdateRadioChannels(ent, model);
        Dirty(ent);

        var ev = new CyborgModelChangedEvent(modelId);
        RaiseLocalEvent(ent.Owner, ref ev);
    }

    /// <summary>
    /// Takes the model away, as the reclassification board does in tgstation.
    /// The cyborg gets to pick a new one.
    /// </summary>
    public void ResetModel(Entity<CyborgComponent> ent)
    {
        if (_net.IsClient)
            return;

        ClearModel(ent);
        ent.Comp.Model = null;
        ent.Comp.Skin = null;
        ReapplyUpgrades(ent);
        UpdateRadioChannels(ent, null);
        Dirty(ent);

        if (_mind.TryGetMind(ent.Owner, out _, out _))
            OpenModelMenu(ent);
    }

    private void ClearModel(Entity<CyborgComponent> ent)
    {
        DeleteAllTools(ent);

        if (_proto.TryIndex(ent.Comp.Model, out var oldModel))
            EntityManager.RemoveComponents(ent.Owner, oldModel.AddComponents);
    }

    public void SetSkin(Entity<CyborgComponent> ent, ProtoId<CyborgSkinPrototype> skin)
    {
        if (ent.Comp.Skin == skin)
            return;

        ent.Comp.Skin = skin;
        Dirty(ent);
    }

    /// <summary>
    /// The skin currently shown, falling back to the default one.
    /// </summary>
    public CyborgSkinPrototype? GetSkin(CyborgComponent cyborg)
    {
        return _proto.TryIndex(cyborg.Skin ?? cyborg.DefaultSkin, out var skin) ? skin : null;
    }

    protected virtual void UpdateRadioChannels(Entity<CyborgComponent> ent, CyborgModelPrototype? model)
    {
    }
}

/// <summary>
/// Raised on a cyborg after it got a new model.
/// </summary>
[ByRefEvent]
public readonly record struct CyborgModelChangedEvent(ProtoId<CyborgModelPrototype> Model);
