// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Goobstation.Maths.FixedPoint;
using Content.Server.DeviceNetwork.Systems;
using Content.Server.Silicons.Laws;
using Content.Shared._CorvaxNext.Silicons.Borgs.Components;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared._IS14.Cyborgs.Systems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.DeviceNetwork.Events;
using Content.Shared.Emag.Systems;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Robotics;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Trigger.Systems;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Cyborgs;

/// <summary>
/// Robotics console support for tgstation-style cyborgs, a port of the upstream borg transponder.
/// </summary>
public sealed class CyborgTransponderSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SharedCyborgSystem _cyborg = default!;
    [Dependency] private readonly DeviceNetworkSystem _deviceNetwork = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly ItemSlotsSystem _itemSlots = default!;
    [Dependency] private readonly SiliconLawSystem _law = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly MobThresholdSystem _mobThreshold = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgTransponderComponent, DeviceNetworkPacketEvent>(OnPacketReceived);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CyborgTransponderComponent, CyborgComponent, DeviceNetworkComponent>();
        while (query.MoveNext(out var uid, out var transponder, out var cyborg, out var device))
        {
            if (transponder.NextDisable is { } nextDisable && now >= nextDisable)
                DoDisable((uid, transponder, cyborg));

            if (now < transponder.NextBroadcast)
                continue;

            transponder.NextBroadcast = now + transponder.BroadcastDelay;

            var charge = 0f;
            if (_powerCell.TryGetBatteryFromSlot(uid, out var battery))
                charge = _battery.GetChargeLevel(battery.Value.AsNullable());

            var data = new CyborgControlData(
                _cyborg.GetSkin(cyborg)?.Icon,
                Loc.GetString(transponder.ChassisName),
                Name(uid),
                charge,
                CalcHp(uid),
                cyborg.UpgradeContainer.ContainedEntities.Count,
                HasBrain(cyborg) && !transponder.FakeDisabled,
                transponder.NextDisable == null && !transponder.FakeDisabling,
                HasComp<AiRemoteControllerComponent>(uid));

            var payload = new NetworkPayload
            {
                [DeviceNetworkConstants.Command] = DeviceNetworkConstants.CmdUpdatedState,
                [RoboticsConsoleConstants.NET_CYBORG_DATA] = data,
            };
            _deviceNetwork.QueuePacket(uid, null, payload, device: device);
        }
    }

    private void OnPacketReceived(Entity<CyborgTransponderComponent> ent, ref DeviceNetworkPacketEvent args)
    {
        if (!args.Data.TryGetValue(DeviceNetworkConstants.Command, out string? command))
            return;

        switch (command)
        {
            case RoboticsConsoleConstants.NET_CHANGE_LAWS_COMMAND:
                if (args.Data.TryGetValue(RoboticsConsoleConstants.NET_CIRCUIT_BOARD, out EntityUid board))
                    ChangeLaws(ent, board);
                break;
            case RoboticsConsoleConstants.NET_DISABLE_COMMAND:
                Disable(ent);
                break;
            case RoboticsConsoleConstants.NET_DESTROY_COMMAND:
                Destroy(ent);
                break;
        }
    }

    private void ChangeLaws(EntityUid uid, EntityUid board)
    {
        if (CheckEmagged(uid, "destroyed") || !TryComp<SiliconLawProviderComponent>(board, out var law))
            return;

        _law.SetLaws(_law.GetLawset(law.Laws).Laws, uid, law.LawUploadSound);
    }

    private void Disable(Entity<CyborgTransponderComponent> ent)
    {
        if (!TryComp<CyborgComponent>(ent, out var cyborg) || cyborg.BrainEntity == null
            || ent.Comp.NextDisable != null)
            return;

        // Refresh the console right away.
        ent.Comp.NextBroadcast = _timing.CurTime;

        if (CheckEmagged(ent, "disabled"))
            ent.Comp.FakeDisabling = true;
        else
            _popup.PopupEntity(Loc.GetString("borg-transponder-disabling-popup"), ent);

        ent.Comp.NextDisable = _timing.CurTime + ent.Comp.DisableDelay;
    }

    private void DoDisable(Entity<CyborgTransponderComponent, CyborgComponent> ent)
    {
        ent.Comp1.NextDisable = null;
        if (ent.Comp1.FakeDisabling)
        {
            ent.Comp1.FakeDisabled = true;
            ent.Comp1.FakeDisabling = false;
            return;
        }

        if (ent.Comp2.BrainEntity == null)
            return;

        _popup.PopupEntity(Loc.GetString("borg-transponder-disabled-popup", ("name", Name(ent))), ent);
        _cyborg.TryEjectBrain((ent.Owner, ent.Comp2));
    }

    public void Destroy(Entity<CyborgTransponderComponent> ent)
    {
        // Stealthy until someone notices the cyborg didn't explode.
        if (CheckEmagged(ent, "destroyed"))
        {
            RemComp<CyborgTransponderComponent>(ent);
            return;
        }

        _popup.PopupEntity(Loc.GetString("borg-transponder-destroying-popup", ("name", Name(ent))), ent);
        _trigger.ActivateTimerTrigger(ent.Owner);

        // Don't let it run into people while it ticks.
        RemComp<InputMoverComponent>(ent);
    }

    private bool CheckEmagged(EntityUid uid, string name)
    {
        if (!_emag.CheckFlag(uid, EmagType.Interaction))
            return false;

        _popup.PopupEntity(Loc.GetString($"borg-transponder-emagged-{name}-popup"), uid, uid, PopupType.LargeCaution);
        return true;
    }

    private bool HasBrain(CyborgComponent cyborg)
    {
        if (cyborg.BrainEntity is not { } brain)
            return false;

        // An empty MMI doesn't count.
        return !TryComp<MMIComponent>(brain, out var mmi) || _itemSlots.GetItemOrNull(brain, mmi.BrainSlotId) != null;
    }

    /// <summary>
    /// 1 when undamaged, 0 once critical.
    /// </summary>
    private float CalcHp(EntityUid uid)
    {
        if (!TryComp<DamageableComponent>(uid, out var damageable))
            return 1;

        if (!_mobState.IsAlive(uid))
            return 0;

        if (!_mobThreshold.TryGetThresholdForState(uid, MobState.Critical, out var threshold))
            return 1;

        return 1 - ((FixedPoint2) (damageable.TotalDamage / threshold)).Float();
    }
}
