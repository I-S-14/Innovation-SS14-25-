// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Alert;
using Content.Shared._IS14.Cyborgs;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared._IS14.Cyborgs.Systems;
using Content.Shared._IS14.Cyborgs.Prototypes;
using System.Numerics;
using Content.Shared.Clothing;
using Content.Shared.Mobs;
using Content.Shared.Movement.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Containers;
using Robust.Shared.Player;
using Robust.Shared.Timing;

namespace Content.Client._IS14.Cyborgs;

public sealed class CyborgSystem : SharedCyborgSystem
{
    [Dependency] private readonly AppearanceSystem _appearance = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPlayerManager _player = default!;

    private const string MovementLayerKey = "movement";
    private static readonly TimeSpan AlertUpdateDelay = TimeSpan.FromSeconds(0.5f);
    private TimeSpan _nextAlertUpdate = TimeSpan.Zero;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgComponent, AfterAutoHandleStateEvent>(OnAfterHandleState);
        SubscribeLocalEvent<CyborgComponent, AppearanceChangeEvent>(OnAppearanceChanged);
        SubscribeLocalEvent<CyborgComponent, LocalPlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<CyborgComponent, LocalPlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<EquipmentVisualsUpdatedEvent>(OnEquipmentVisualsUpdated);
        SubscribeLocalEvent<CyborgComponent, ComponentShutdown>((uid, _, _) => _hats.Remove(uid));
    }

    private const string HatSlot = "head";

    /// <summary>
    /// Hat layers of each cyborg and the skin offset already added to them, so a skin change can move them again.
    /// </summary>
    private readonly Dictionary<EntityUid, (HashSet<string> Keys, Vector2 Applied, bool Hidden)> _hats = new();

    // The clothing system positions the hat for the inventory slot, the skin then nudges it like tg's hat_offset.
    private void OnEquipmentVisualsUpdated(EquipmentVisualsUpdatedEvent args)
    {
        if (args.Slot != HatSlot || !TryComp<CyborgComponent>(args.Equipee, out var cyborg))
            return;

        _hats[args.Equipee] = (new HashSet<string>(args.RevealedLayers), Vector2.Zero, false);
        UpdateHat((args.Equipee, cyborg));
    }

    private void UpdateHat(Entity<CyborgComponent> ent)
    {
        if (!_hats.TryGetValue(ent.Owner, out var hat) || !TryComp<SpriteComponent>(ent, out var sprite) ||
            GetSkin(ent.Comp) is not { } skin)
            return;

        var target = new Vector2(skin.HatOffset.X, skin.HatOffset.Y) / EyeManager.PixelsPerMeter;
        var delta = target - hat.Applied;
        foreach (var key in hat.Keys)
        {
            if (!_sprite.LayerMapTryGet((ent.Owner, sprite), key, out var index, false) ||
                sprite[index] is not SpriteComponent.Layer layer)
                continue;

            if (delta != Vector2.Zero)
                _sprite.LayerSetOffset(layer, layer.Offset + delta);

            // Skins with a built-in hat or no head hide the worn one, and show it again after a skin change.
            if (skin.NoHat != hat.Hidden)
                _sprite.LayerSetVisible(layer, !skin.NoHat);
        }

        _hats[ent.Owner] = (hat.Keys, target, skin.NoHat);
    }

    private void OnAfterHandleState(Entity<CyborgComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        UpdateAppearance(ent);
        UpdateToolsUi(ent);
    }

    private void OnAppearanceChanged(Entity<CyborgComponent> ent, ref AppearanceChangeEvent args)
    {
        UpdateAppearance(ent);
    }

    protected override void OnInserted(Entity<CyborgComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        base.OnInserted(ent, ref args);
        UpdateToolsUi(ent);
    }

    protected override void OnRemoved(Entity<CyborgComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        base.OnRemoved(ent, ref args);
        UpdateToolsUi(ent);
    }

    /// <summary>
    /// The local player became a cyborg.
    /// </summary>
    public event Action<Entity<CyborgComponent>>? LocalCyborgAttached;

    /// <summary>
    /// The local player stopped being a cyborg.
    /// </summary>
    public event Action? LocalCyborgDetached;

    /// <summary>
    /// The local cyborg's model, tools or slots changed.
    /// </summary>
    public event Action<Entity<CyborgComponent>>? LocalCyborgUpdated;

    public void RequestToggleTool(EntityUid tool)
    {
        RaisePredictiveEvent(new CyborgToggleToolRequest(GetNetEntity(tool)));
    }

    public void RequestStoreTool(string? hand)
    {
        RaisePredictiveEvent(new CyborgStoreToolRequest(hand));
    }

    public void RequestToggleLamp()
    {
        RaiseNetworkEvent(new CyborgToggleLampRequest());
    }

    public void RequestModelMenu()
    {
        RaisePredictiveEvent(new CyborgOpenModelMenuRequest());
    }

    private void UpdateToolsUi(Entity<CyborgComponent> ent)
    {
        if (ent.Owner == _player.LocalEntity)
            LocalCyborgUpdated?.Invoke(ent);
    }

    private void UpdateAppearance(Entity<CyborgComponent> ent)
    {
        if (!TryComp<SpriteComponent>(ent, out var sprite) || GetSkin(ent.Comp) is not { } skin)
            return;

        var spriteEnt = (ent.Owner, sprite);

        var moving = false;
        if (TryComp<SpriteMovementComponent>(ent, out var movement))
        {
            UpdateMovementLayers(movement, skin);
            moving = movement.IsMoving;
        }

        var body = moving && skin.MovingBodyState != null ? skin.MovingBodyState : skin.BodyState;
        _sprite.LayerSetRsi(spriteEnt, CyborgVisualLayers.Body, skin.Sprite, body);

        _appearance.TryGetData<bool>(ent.Owner, CyborgVisuals.HasMind, out var hasMind);
        _appearance.TryGetData<bool>(ent.Owner, CyborgVisuals.HasBrain, out var hasBrain);
        var alive = !_appearance.TryGetData<MobState>(ent.Owner, MobStateVisuals.State, out var state) ||
                    state == MobState.Alive;

        var eyes = hasMind ? skin.EyesState : skin.NoMindEyesState;
        var showEyes = alive && (hasMind || hasBrain) && eyes != null;
        if (eyes != null)
            _sprite.LayerSetRsi(spriteEnt, CyborgVisualLayers.Eyes, skin.Sprite, eyes);
        _sprite.LayerSetVisible(spriteEnt, CyborgVisualLayers.Eyes, showEyes);

        // Visibility of the headlamp layer is driven by ToggleableVisuals, only the state is ours.
        if (skin.LightState != null)
            _sprite.LayerSetRsi(spriteEnt, CyborgVisualLayers.Light, skin.Sprite, skin.LightState);

        UpdateHat(ent);
    }

    // The movement layers aren't networked, so every client fills them from the skin itself.
    private static void UpdateMovementLayers(SpriteMovementComponent movement, CyborgSkinPrototype skin)
    {
        movement.MovementLayers.Clear();
        movement.NoMovementLayers.Clear();

        if (skin.MovingBodyState == null)
            return;

        var rsi = skin.Sprite.ToString();
        movement.MovementLayers[MovementLayerKey] = new PrototypeLayerData { RsiPath = rsi, State = skin.MovingBodyState };
        movement.NoMovementLayers[MovementLayerKey] = new PrototypeLayerData { RsiPath = rsi, State = skin.BodyState };
    }

    private void OnPlayerAttached(Entity<CyborgComponent> ent, ref LocalPlayerAttachedEvent args)
    {
        UpdateBatteryAlert(ent);
        LocalCyborgAttached?.Invoke(ent);
    }

    private void OnPlayerDetached(Entity<CyborgComponent> ent, ref LocalPlayerDetachedEvent args)
    {
        _alerts.ClearAlert(ent.Owner, ent.Comp.BatteryAlert);
        _alerts.ClearAlert(ent.Owner, ent.Comp.NoBatteryAlert);
        LocalCyborgDetached?.Invoke();
    }

    private void UpdateBatteryAlert(Entity<CyborgComponent> ent)
    {
        if (!TryComp<PowerCellSlotComponent>(ent, out var slot))
            return;

        if (!_powerCell.TryGetBatteryFromSlot((ent.Owner, slot), out var battery))
        {
            _alerts.ShowAlert(ent.Owner, ent.Comp.NoBatteryAlert);
            return;
        }

        // Alert levels go from 0 to 10, 0 only when there is no charge to draw at all.
        var chargeLevel = (short) MathF.Round(_battery.GetChargeLevel(battery.Value.AsNullable()) * 10f);
        if (chargeLevel == 0 && _powerCell.HasDrawCharge((ent.Owner, null, slot)))
            chargeLevel = 1;

        _alerts.ShowAlert(ent.Owner, ent.Comp.BatteryAlert, chargeLevel);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // The charge isn't networked, so the alert is refreshed on the client.
        if (_player.LocalEntity is not { } local || _timing.CurTime < _nextAlertUpdate)
            return;

        _nextAlertUpdate = _timing.CurTime + AlertUpdateDelay;

        if (TryComp<CyborgComponent>(local, out var cyborg))
            UpdateBatteryAlert((local, cyborg));
    }
}
