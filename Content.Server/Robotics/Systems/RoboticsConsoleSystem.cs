// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Logs;
using Content.Shared.DeviceNetwork.Systems;
using Content.Server.Radio.EntitySystems;
using Content.Shared.Lock;
using Content.Shared.Database;
using Content.Shared.DeviceNetwork;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Robotics;
using Content.Shared.Robotics.Components;
using Content.Shared.Robotics.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Timing;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.Containers;
using Content.Shared.DeviceNetwork.Events;
using Content.Server.DeviceNetwork.Systems;

namespace Content.Server.Research.Systems;

/// <summary>
/// Handles UI and state receiving for the robotics control console.
/// <c>BorgTransponderComponent<c/> broadcasts state from the station's borgs to consoles.
/// </summary>
public sealed partial class RoboticsConsoleSystem : SharedRoboticsConsoleSystem
{
    [Dependency] private readonly ItemSlotsSystem _slots = default!;
    [Dependency] private DeviceNetworkSystem _deviceNetwork = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private LockSystem _lock = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private UserInterfaceSystem _ui = default!;
    [Dependency] private EntityQuery<DeviceNetworkComponent> _deviceNetworkComp = default!;

    // almost never timing out more than 1 per tick so initialize with that capacity
    private List<DeviceAddress> _removing = new(1);

    public override void Initialize()
    {
        base.Initialize();

        // Corvax-Goob-MutableLaws-Start
        SubscribeLocalEvent<RoboticsConsoleComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<RoboticsConsoleComponent, RoboticsConsoleChangeLawsMessage>(OnChangeLaws);
        SubscribeLocalEvent<RoboticsConsoleComponent, EntRemovedFromContainerMessage>(OnRemoved);
        // Corvax-Goob-MutableLaws-End
        Subs.BuiEvents<RoboticsConsoleComponent>(RoboticsConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<RoboticsConsoleDisableMessage>(OnDisable);
            subs.Event<RoboticsConsoleDestroyMessage>(OnDestroy);
            // TODO: camera stuff
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<RoboticsConsoleComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            // remove cyborgs that havent pinged in a while
            _removing.Clear();
            foreach (var (address, ent) in comp.CyborgsAddresses)
            {
                if (now >= comp.Cyborgs[ent].Timeout)
                    _removing.Add(address);
            }

            // needed to prevent modifying while iterating it
            foreach (var address in _removing)
            {
                if (!comp.CyborgsAddresses.Remove(address, out var ent))
                    continue;

                comp.Cyborgs.Remove(ent);
            }

            if (_removing.Count > 0)
                UpdateUserInterface((uid, comp));
        }
    }

    [SubscribeLocalEvent]
    private void OnPacketReceived(Entity<RoboticsConsoleComponent> ent, ref DeviceNetworkPacketEvent<RoboticsCyborgDataPayload> args)
    {
        var data = args.Data.Data;
        data.Timeout = _timing.CurTime + ent.Comp.Timeout;
        ent.Comp.CyborgsAddresses[args.SenderAddress] = args.Sender;
        ent.Comp.Cyborgs[args.Sender] = data;

        UpdateUserInterface(ent);
    }

    // Corvax-Goob-MutableLaws-Start
    private void OnInserted(Entity<RoboticsConsoleComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateUserInterface(ent);
    }

    private void OnRemoved(Entity<RoboticsConsoleComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateUserInterface(ent);
    }
    // Corvax-Goob-MutableLaws-End

    private void OnOpened(Entity<RoboticsConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUserInterface(ent);
    }

    // Corvax-Goob-MutableLaws-Start
    private void OnChangeLaws(Entity<RoboticsConsoleComponent> ent, ref RoboticsConsoleChangeLawsMessage args)
    {
        if (_lock.IsLocked(ent.Owner))
            return;

        if (!ent.Comp.CyborgsAddresses.TryGetValue(args.Address, out var borg)
            || !ent.Comp.Cyborgs.TryGetValue(borg, out var data))
            return;

        if (!data.IsLawChangable)
            return;

        if (!_slots.TryGetSlot(ent.Owner, ent.Comp.CircuitBoardItemSlot, out var slot) || slot.Item is null)
            return;
        // Corvax-Next-AiRemoteControl-Start
        if (data.IsAiControllable)
            return;
        // Corvax-Next-AiRemoteControl-End

        var payload = new RoboticsCyborgChangeLawsPayload
        {
            CircuitBoard = slot.Item.Value,
        };
        _deviceNetwork.SendPacket(ent.Owner, args.Address, ref payload);

        var message = Loc.GetString(ent.Comp.ChangeLawsMessage, ("name", data.Name));
        _radio.SendRadioMessage(ent, message, ent.Comp.RadioChannel, ent);
        _adminLogger.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(args.Actor):user} changed laws of borg {data.Name} with address {args.Address}");
    }
    // Corvax-Goob-MutableLaws-End

    private void OnDisable(Entity<RoboticsConsoleComponent> ent, ref RoboticsConsoleDisableMessage args)
    {
        if (!ent.Comp.AllowBorgControl)
            return;

        if (_lock.IsLocked(ent.Owner))
            return;

        if (!ent.Comp.CyborgsAddresses.Remove(args.Address, out var uid))
            return;

        if (!ent.Comp.Cyborgs.Remove(uid, out var data))
            return;

        var payload = new RoboticsCyborgDisablePayload();
        _deviceNetwork.SendPacket(ent.Owner, args.Address, ref payload);

        _adminLogger.Add(LogType.Action, LogImpact.High, $"{ToPrettyString(args.Actor):user} disabled borg {data.Name} with address {args.Address}");
    }

    private void OnDestroy(Entity<RoboticsConsoleComponent> ent, ref RoboticsConsoleDestroyMessage args)
    {
        if (!ent.Comp.AllowBorgControl)
            return;

        if (_lock.IsLocked(ent.Owner))
            return;

        var now = _timing.CurTime;
        if (now < ent.Comp.NextDestroy)
            return;

        if (!ent.Comp.CyborgsAddresses.Remove(args.Address, out var uid))
            return;

        if (!ent.Comp.Cyborgs.Remove(uid, out var data))
            return;

        var payload = new RoboticsCyborgDestroyPayload();
        _deviceNetwork.SendPacket(ent.Owner, args.Address, ref payload);

        var message = Loc.GetString(ent.Comp.DestroyMessage, ("name", data.Name));
        _radio.SendRadioMessage(ent, message, ent.Comp.RadioChannel, ent);
        _adminLogger.Add(LogType.Action, LogImpact.Extreme, $"{ToPrettyString(args.Actor):user} destroyed borg {data.Name} with address {args.Address}");

        ent.Comp.NextDestroy = now + ent.Comp.DestroyCooldown;
        Dirty(ent, ent.Comp);
    }

    private void UpdateUserInterface(Entity<RoboticsConsoleComponent> ent)
    {
        var cyborgs = new Dictionary<LocDeviceAddress, CyborgControlData>(ent.Comp.Cyborgs.Count);
        foreach (var (uid, data) in ent.Comp.Cyborgs)
        {
            if (_deviceNetworkComp.TryComp(uid, out var device))
                cyborgs.Add(device, data);
        }
        var state = new RoboticsConsoleState(cyborgs, ent.Comp.AllowBorgControl,
            _slots.TryGetSlot(ent.Owner, ent.Comp.CircuitBoardItemSlot, out var slot) && slot.HasItem);

        _ui.SetUiState(ent.Owner, RoboticsConsoleUiKey.Key, state);
    }
}
