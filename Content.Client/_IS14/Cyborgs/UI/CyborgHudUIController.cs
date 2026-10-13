// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Client.Examine;
using Content.Client.Hands.Systems;
using Content.Client.UserInterface.Systems.Gameplay;
using Content.Client.UserInterface.Systems.Hotbar.Widgets;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Light.Components;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controllers;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._IS14.Cyborgs.UI;

/// <summary>
/// Swaps the hands bar for the tgstation-style cyborg HUD while the local player is a cyborg.
/// </summary>
public sealed class CyborgHudUIController : UIController, IOnSystemChanged<CyborgSystem>, IOnSystemChanged<HandsSystem>
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    [UISystemDependency] private readonly CyborgSystem _cyborg = default!;
    [UISystemDependency] private readonly HandsSystem _hands = default!;
    [UISystemDependency] private readonly ExamineSystem _examine = default!;

    // The hands, status panels and inventory row of the hotbar, hidden while the HUD is up.
    private const string HotbarRowName = "Hotbar";

    private CyborgHudGui? _gui;
    private Control? _hiddenHotbarRow;

    /// <summary>
    /// The HUD while it is on screen.
    /// </summary>
    public CyborgHudGui? Hud => _gui?.Parent != null ? _gui : null;

    public override void Initialize()
    {
        base.Initialize();

        var gameplayLoad = UIManager.GetUIController<GameplayStateLoadController>();
        gameplayLoad.OnScreenLoad += OnScreenLoad;
        gameplayLoad.OnScreenUnload += RemoveHud;
    }

    public void OnSystemLoaded(CyborgSystem system)
    {
        system.LocalCyborgAttached += OnAttached;
        system.LocalCyborgDetached += RemoveHud;
        system.LocalCyborgUpdated += OnUpdated;
    }

    public void OnSystemUnloaded(CyborgSystem system)
    {
        system.LocalCyborgAttached -= OnAttached;
        system.LocalCyborgDetached -= RemoveHud;
        system.LocalCyborgUpdated -= OnUpdated;
    }

    public void OnSystemLoaded(HandsSystem system)
    {
        system.OnPlayerSetActiveHand += OnActiveHandChanged;
        system.OnPlayerItemAdded += OnHandItemChanged;
        system.OnPlayerItemRemoved += OnHandItemChanged;
    }

    public void OnSystemUnloaded(HandsSystem system)
    {
        system.OnPlayerSetActiveHand -= OnActiveHandChanged;
        system.OnPlayerItemAdded -= OnHandItemChanged;
        system.OnPlayerItemRemoved -= OnHandItemChanged;
    }

    private void OnScreenLoad()
    {
        if (TryGetLocalCyborg(out var cyborg))
            OnAttached(cyborg);
    }

    private void OnAttached(Entity<CyborgComponent> cyborg)
    {
        if (UIManager.ActiveScreen?.GetWidget<HotbarGui>() is not { } hotbar)
            return;

        if (_gui == null)
        {
            _gui = new CyborgHudGui();
            _gui.ToolPressed += tool => _cyborg.RequestToggleTool(tool);
            _gui.ToolSecondaryPressed += tool => _examine.DoExamine(tool);
            _gui.SlotPressed += OnSlotPressed;
            _gui.SlotSecondaryPressed += hand => _cyborg.RequestStoreTool(hand);
            _gui.StorePressed += () => _cyborg.RequestStoreTool(null);
            _gui.ModelPressed += OnModelPressed;
            _gui.LampPressed += () => _cyborg.RequestToggleLamp();
        }

        if (_gui.Parent != hotbar)
        {
            _gui.Orphan();
            hotbar.AddChild(_gui);
        }

        _hiddenHotbarRow = hotbar.FindControl<Control>(HotbarRowName);
        _hiddenHotbarRow.Visible = false;

        Refresh();
    }

    private void RemoveHud()
    {
        if (_hiddenHotbarRow != null)
            _hiddenHotbarRow.Visible = true;

        _hiddenHotbarRow = null;
        _gui?.Orphan();
    }

    private void OnUpdated(Entity<CyborgComponent> cyborg)
    {
        Refresh();
    }

    private void OnActiveHandChanged(string? hand)
    {
        Refresh();
    }

    private void OnHandItemChanged(string hand, EntityUid item)
    {
        Refresh();
    }

    private void OnSlotPressed(string hand)
    {
        if (_hands.TryGetPlayerHands(out var hands))
            _hands.UIHandClick(hands.Value, hand);
    }

    private void OnModelPressed()
    {
        if (_gui == null || !TryGetLocalCyborg(out var cyborg))
            return;

        // Without a model there is nothing to list, so the button reopens the model picker.
        if (cyborg.Comp.Model == null)
        {
            _cyborg.RequestModelMenu();
            return;
        }

        _gui.SetToolsOpen(!_gui.ToolsOpen);
    }

    // The lamp state comes from the light's own component state, cheaper to read each frame than to hook.
    public override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (Hud is not { } hud || _player.LocalEntity is not { } uid)
            return;

        hud.SetLamp(EntityManager.TryGetComponent<HandheldLightComponent>(uid, out var light) && light.Activated);
    }

    private bool TryGetLocalCyborg(out Entity<CyborgComponent> cyborg)
    {
        cyborg = default;
        if (_player.LocalEntity is not { } uid ||
            !EntityManager.TryGetComponent<CyborgComponent>(uid, out var comp))
            return false;

        cyborg = (uid, comp);
        return true;
    }

    private void Refresh()
    {
        if (_gui?.Parent == null || !TryGetLocalCyborg(out var cyborg) || !_hands.TryGetPlayerHands(out var hands))
            return;

        var comp = cyborg.Comp;

        if (_proto.TryIndex(comp.Model, out var model))
            _gui.SetModel(model.HudIcon, Loc.GetString(model.Name));
        else
            _gui.SetModel("nomod", Loc.GetString("cyborg-hud-no-model"));

        var activeHand = hands.Value.Comp.ActiveHandId;
        var slots = new List<(string, EntityUid?, string)>();
        for (var i = 0; i < comp.ToolSlots; i++)
        {
            var hand = _cyborg.GetToolHandId(comp, i);
            _hands.TryGetHeldItem(hands.Value.AsNullable(), hand, out var item);

            // screen_cyborg only has three numbered slots.
            var number = Math.Clamp(i + 1, 1, 3);
            var background = !comp.Active ? $"inv{number}_b"
                : hand == activeHand ? $"inv{number}_a"
                : $"inv{number}";
            slots.Add((hand, item, background));
        }

        _gui.SetSlots(slots);

        var tools = new List<(EntityUid, string, bool)>();
        foreach (var tool in _cyborg.EnumerateTools(cyborg))
        {
            tools.Add((tool, EntityManager.GetComponent<MetaDataComponent>(tool).EntityName,
                _cyborg.IsToolHeld(cyborg, tool, out _)));
        }

        _gui.SetTools(tools);

        _hands.TryGetHand(hands.Value.AsNullable(), activeHand, out var active);

        _hands.TryGetActiveItem(hands.Value.AsNullable(), out var activeItem);
        _gui.SetStatus(activeItem, active);
    }
}
