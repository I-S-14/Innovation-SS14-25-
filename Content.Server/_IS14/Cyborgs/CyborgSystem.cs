// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Linq;
using Content.Server.Administration.Managers;
using Content.Server.Light.EntitySystems;
using Content.Shared._IS14.Cyborgs;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Light.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Shared.Timing;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared._IS14.Cyborgs.Prototypes;
using Content.Shared._IS14.Cyborgs.Systems;
using Content.Shared.Radio;
using Content.Shared.Radio.Components;
using Content.Shared.Roles;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.Cyborgs;

public sealed class CyborgSystem : SharedCyborgSystem
{
    [Dependency] private readonly IBanManager _banManager = default!;

    [Dependency] private readonly HandheldLightSystem _light = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    private static readonly TimeSpan MaintenanceRefresh = TimeSpan.FromSeconds(1);
    private TimeSpan _nextMaintenanceRefresh;

    private static readonly ProtoId<JobPrototype> BorgJobId = "Borg";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<CyborgToggleLampRequest>(OnToggleLampRequest);

        Subs.BuiEvents<CyborgComponent>(CyborgUiKey.Maintenance, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnMaintenanceOpened);
            subs.Event<CyborgMaintenanceEjectBrainMessage>(OnMaintenanceEjectBrain);
            subs.Event<CyborgMaintenanceEjectCellMessage>(OnMaintenanceEjectCell);
            subs.Event<CyborgMaintenanceRemoveUpgradeMessage>(OnMaintenanceRemoveUpgrade);
        });
    }

    private void OnMaintenanceOpened(Entity<CyborgComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateMaintenanceUi(ent);
    }

    private void OnMaintenanceEjectBrain(Entity<CyborgComponent> ent, ref CyborgMaintenanceEjectBrainMessage args)
    {
        TryEjectBrain(ent, args.Actor);
        UpdateMaintenanceUi(ent);
    }

    private void OnMaintenanceEjectCell(Entity<CyborgComponent> ent, ref CyborgMaintenanceEjectCellMessage args)
    {
        if (TryComp<PowerCellSlotComponent>(ent, out var cellSlot) &&
            _itemSlots.TryGetSlot(ent.Owner, cellSlot.CellSlotId, out var slot))
            _itemSlots.TryEjectToHands(ent.Owner, slot, args.Actor);

        UpdateMaintenanceUi(ent);
    }

    private void OnMaintenanceRemoveUpgrade(Entity<CyborgComponent> ent, ref CyborgMaintenanceRemoveUpgradeMessage args)
    {
        if (TryGetEntity(args.Board, out var board))
            TryRemoveUpgrade(ent, board.Value, args.Actor);

        UpdateMaintenanceUi(ent);
    }

    private void UpdateMaintenanceUi(Entity<CyborgComponent> ent)
    {
        if (!_ui.IsUiOpen(ent.Owner, CyborgUiKey.Maintenance))
            return;

        float? charge = null;
        if (_powerCell.TryGetBatteryFromSlot(ent.Owner, out var battery))
            charge = _battery.GetChargeLevel(battery.Value.AsNullable());

        string? model = null;
        if (_proto.TryIndex(ent.Comp.Model, out var modelProto))
            model = Loc.GetString(modelProto.Name);

        var upgrades = new List<(NetEntity, string)>();
        foreach (var board in ent.Comp.UpgradeContainer.ContainedEntities)
        {
            upgrades.Add((GetNetEntity(board), Name(board)));
        }

        var brain = ent.Comp.BrainEntity is { } brainEnt ? Name(brainEnt) : null;
        _ui.SetUiState(ent.Owner, CyborgUiKey.Maintenance, new CyborgMaintenanceBuiState(brain, charge, model, upgrades));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // The cell drains while the window is open, keep the charge bar current.
        if (_timing.CurTime < _nextMaintenanceRefresh)
            return;

        _nextMaintenanceRefresh = _timing.CurTime + MaintenanceRefresh;
        var query = EntityQueryEnumerator<CyborgComponent>();
        while (query.MoveNext(out var uid, out var cyborg))
        {
            UpdateMaintenanceUi((uid, cyborg));
        }
    }

    // The lamp can only be toggled on the server, so the HUD button isn't predicted.
    private void OnToggleLampRequest(CyborgToggleLampRequest msg, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } uid || !HasComp<CyborgComponent>(uid) ||
            !TryComp<HandheldLightComponent>(uid, out var light))
            return;

        _light.ToggleStatus(uid, (uid, light));
    }

    public override bool CanPlayerBeBorged(ICommonSession session)
    {
        return _banManager.GetJobBans(session.UserId)?.Contains(BorgJobId) != true;
    }

    protected override void UpdateRadioChannels(Entity<CyborgComponent> ent, CyborgModelPrototype? model)
    {
        var channels = new HashSet<ProtoId<RadioChannelPrototype>>(ent.Comp.InherentRadioChannels);
        if (model != null)
            channels.UnionWith(model.RadioChannels);

        if (TryComp<IntrinsicRadioTransmitterComponent>(ent, out var transmitter))
            transmitter.Channels = new HashSet<ProtoId<RadioChannelPrototype>>(channels);

        if (TryComp<ActiveRadioComponent>(ent, out var activeRadio))
            activeRadio.Channels = new HashSet<ProtoId<RadioChannelPrototype>>(channels);
    }
}
