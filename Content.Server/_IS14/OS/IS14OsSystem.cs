using System.Linq;
using System.Numerics;
using Content.Server.AlertLevel;
using Content.Server.PDA.Ringer;
using Content.Server.Station.Systems;
using Content.Server.Store.Systems;
using Content.Server.Traitor.Uplink;
using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.Prototypes;
using Content.Shared._IS14.OS.UI;
using Content.Shared.Access.Components;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.PDA;
using Content.Shared.PDA.Ringer;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._IS14.OS;

/// <summary>
///     The OS core: lid and power, windows, and the composite UI state.
///     Design: Docs/_IS14/os-design.md §4-§6.
/// </summary>
public sealed class IS14OsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IS14OsMemorySystem _memory = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly UnpoweredFlashlightSystem _flashlight = default!;
    [Dependency] private readonly RingerSystem _ringer = default!;
    [Dependency] private readonly StoreSystem _store = default!;
    [Dependency] private readonly IS14OsPowerSystem _power = default!;

    /// <summary>
    ///     Devices whose state changed for a passive reason. Flushed at most once a second so a
    ///     busy station does not push a PDA state to everyone in PVS on every event (§4.5).
    /// </summary>
    private readonly HashSet<EntityUid> _pendingRefresh = new();
    private readonly List<EntityUid> _refreshBuffer = new();

    private TimeSpan _nextFlush;

    private static readonly TimeSpan FlushInterval = TimeSpan.FromSeconds(1);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14OsDeviceComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<IS14OsDeviceComponent, UseInHandEvent>(OnUseInHand);

        SubscribeLocalEvent<IS14OsDeviceComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<IS14OsDeviceComponent, BoundUIClosedEvent>(OnUiClosed);

        SubscribeLocalEvent<IS14OsDeviceComponent, IS14OsShellMessage>(OnShellMessage);
        SubscribeLocalEvent<IS14OsDeviceComponent, IS14OsAppMessage>(OnAppMessage);

        SubscribeLocalEvent<IS14OsDeviceComponent, EntGotInsertedIntoContainerMessage>(OnStowed);
        SubscribeLocalEvent<IS14OsDeviceComponent, DroppedEvent>(OnDropped);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;

        // Finish cold boots.
        var query = EntityQueryEnumerator<IS14OsDeviceComponent>();
        while (query.MoveNext(out var uid, out var device))
        {
            if (device.BootEnd is not { } end || now < end)
                continue;

            device.BootEnd = null;
            OpenDefaultApp((uid, device));
            UpdateUi(uid, device);
        }

        if (now < _nextFlush)
            return;

        _nextFlush = now + FlushInterval;

        if (_pendingRefresh.Count == 0)
            return;

        // UpdateUi removes from the set, so buffer first — iterating the set directly would
        // throw. (Same trap as the economy consoles.)
        _refreshBuffer.Clear();
        _refreshBuffer.AddRange(_pendingRefresh);
        _pendingRefresh.Clear();

        foreach (var uid in _refreshBuffer)
        {
            if (Exists(uid))
                UpdateUi(uid);
        }
    }

    /// <summary>Queues a passive refresh. Player actions call <see cref="UpdateUi"/> directly.</summary>
    public void MarkDirty(EntityUid uid)
    {
        _pendingRefresh.Add(uid);
    }

    private void OnMapInit(Entity<IS14OsDeviceComponent> ent, ref MapInitEvent args)
    {
        var memory = EnsureComp<IS14OsMemoryComponent>(ent);
        _memory.SetupDevice((ent.Owner, ent.Comp, memory));

        if (ent.Comp.Lidless)
        {
            ent.Comp.LidOpen = true;
            PowerOn(ent, cold: true);
        }

        UpdateVisuals(ent);
    }

    #region Lid and power

    /// <summary>
    ///     Z on a held device. The lid is the session: opening it boots the OS and shows the
    ///     screen, closing it shuts everything down. One keypress, same as before the OS existed.
    /// </summary>
    private void OnUseInHand(Entity<IS14OsDeviceComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.LidOpen)
            CloseLid(ent);
        else
            OpenLid(ent, args.User, openUi: true);
    }

    public void OpenLid(Entity<IS14OsDeviceComponent> ent, EntityUid? user, bool openUi)
    {
        if (ent.Comp.LidOpen)
        {
            if (openUi && user != null)
                _ui.TryOpenUi(ent.Owner, IS14OsUiKey.Key, user.Value);

            return;
        }

        ent.Comp.LidOpen = true;
        _audio.PlayPvs(ent.Comp.LidSound, ent);
        PowerOn(ent, cold: _timing.CurTime - ent.Comp.LastShutdown > GetSleepGrace(ent.Comp));
        UpdateVisuals(ent);

        if (openUi && user != null)
            _ui.TryOpenUi(ent.Owner, IS14OsUiKey.Key, user.Value);
    }

    public void CloseLid(Entity<IS14OsDeviceComponent> ent)
    {
        if (!ent.Comp.LidOpen || ent.Comp.Lidless)
            return;

        ent.Comp.LidOpen = false;
        _audio.PlayPvs(ent.Comp.LidSound, ent);
        PowerOff(ent);
        UpdateVisuals(ent);

        _ui.CloseUi(ent.Owner, IS14OsUiKey.Key);
    }

    private void PowerOn(Entity<IS14OsDeviceComponent> ent, bool cold)
    {
        ent.Comp.Powered = true;
        ent.Comp.BootEnd = cold ? _timing.CurTime + GetBootTime(ent.Comp) : null;

        if (cold)
        {
            _audio.PlayPvs(ent.Comp.BootSound, ent);
            return;
        }

        // Waking from sleep skips the boot screen, so the default app has to open right here.
        OpenDefaultApp(ent);
    }

    /// <summary>Brings up the device's home application, if it is installed and nothing is open.</summary>
    private void OpenDefaultApp(Entity<IS14OsDeviceComponent> ent)
    {
        if (ent.Comp.DefaultApp is not { } app || ent.Comp.Open.Count > 0)
            return;

        OpenApp(ent, app, null);
    }

    private void PowerOff(Entity<IS14OsDeviceComponent> ent)
    {
        ent.Comp.Powered = false;
        ent.Comp.BootEnd = null;
        ent.Comp.LastShutdown = _timing.CurTime;
        ent.Comp.Open.Clear();
        ent.Comp.Minimized.Clear();
    }

    /// <summary>Stowing or dropping the device shuts the lid — no sessions running in a pocket.</summary>
    private void OnStowed(Entity<IS14OsDeviceComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        CloseLid(ent);
    }

    private void OnDropped(Entity<IS14OsDeviceComponent> ent, ref DroppedEvent args)
    {
        CloseLid(ent);
    }

    private void UpdateVisuals(Entity<IS14OsDeviceComponent> ent)
    {
        _appearance.SetData(ent, IS14OsVisuals.LidOpen, ent.Comp.LidOpen);
        _appearance.SetData(ent, IS14OsVisuals.ScreenOn, ent.Comp.LidOpen && ent.Comp.Powered);
    }

    private TimeSpan GetBootTime(IS14OsDeviceComponent device)
    {
        return _memory.GetProfile(device)?.BootTime ?? TimeSpan.FromSeconds(1.5);
    }

    private TimeSpan GetSleepGrace(IS14OsDeviceComponent device)
    {
        return _memory.GetProfile(device)?.SleepGrace ?? TimeSpan.FromSeconds(30);
    }

    #endregion

    #region Windows

    public bool OpenApp(Entity<IS14OsDeviceComponent> ent, ProtoId<IS14OsAppPrototype> app, EntityUid? user)
    {
        if (!ent.Comp.Powered || !TryComp(ent, out IS14OsMemoryComponent? memory))
            return false;

        if (!_memory.IsInstalled(memory, app))
            return false;

        if (ent.Comp.Open.Contains(app))
        {
            FocusApp(ent, app);
            return true;
        }

        var max = Math.Max(1, _memory.GetProfile(ent.Comp)?.MaxWindows ?? 1);

        // Make room: the oldest still-visible window steps aside. On a handheld (max 1) this is
        // exactly the phone behaviour — opening an app puts the previous one in the taskbar.
        while (CountVisible(ent.Comp) >= max)
        {
            var victim = ent.Comp.Open.FirstOrDefault(a => !ent.Comp.Minimized.Contains(a));
            if (victim.Id == null)
                break;

            ent.Comp.Minimized.Add(victim);
        }

        ent.Comp.Open.Add(app);
        ent.Comp.Minimized.Remove(app);

        var ev = new OsAppOpenedEvent(app, user);
        RaiseLocalEvent(ent, ref ev);
        return true;
    }

    public void CloseApp(Entity<IS14OsDeviceComponent> ent, ProtoId<IS14OsAppPrototype> app)
    {
        if (!ent.Comp.Open.Remove(app))
            return;

        ent.Comp.Minimized.Remove(app);

        var ev = new OsAppClosedEvent(app);
        RaiseLocalEvent(ent, ref ev);
    }

    public void MinimizeApp(Entity<IS14OsDeviceComponent> ent, ProtoId<IS14OsAppPrototype> app)
    {
        if (ent.Comp.Open.Contains(app))
            ent.Comp.Minimized.Add(app);
    }

    public void FocusApp(Entity<IS14OsDeviceComponent> ent, ProtoId<IS14OsAppPrototype> app)
    {
        if (!ent.Comp.Open.Contains(app))
            return;

        ent.Comp.Minimized.Remove(app);

        // Last entry is the focused window.
        ent.Comp.Open.Remove(app);
        ent.Comp.Open.Add(app);

        var max = Math.Max(1, _memory.GetProfile(ent.Comp)?.MaxWindows ?? 1);
        while (CountVisible(ent.Comp) > max)
        {
            var victim = ent.Comp.Open.FirstOrDefault(a => a != app && !ent.Comp.Minimized.Contains(a));
            if (victim.Id == null)
                break;

            ent.Comp.Minimized.Add(victim);
        }
    }

    private static int CountVisible(IS14OsDeviceComponent device)
    {
        var count = 0;
        foreach (var app in device.Open)
        {
            if (!device.Minimized.Contains(app))
                count++;
        }

        return count;
    }

    #endregion

    #region UI

    private void OnUiOpened(Entity<IS14OsDeviceComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!IS14OsUiKey.Key.Equals(args.UiKey))
            return;

        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnUiClosed(Entity<IS14OsDeviceComponent> ent, ref BoundUIClosedEvent args)
    {
        if (!IS14OsUiKey.Key.Equals(args.UiKey))
            return;

        if (_ui.IsUiOpen(ent.Owner, IS14OsUiKey.Key))
            return;

        // Nobody is looking any more: drop the state so it stops riding along with PVS, and
        // shut the lid — closing the window is closing the device (§5.3, variant A).
        _ui.SetUiState(ent.Owner, IS14OsUiKey.Key, null);
        _pendingRefresh.Remove(ent.Owner);
        CloseLid(ent);
    }

    private void OnShellMessage(Entity<IS14OsDeviceComponent> ent, ref IS14OsShellMessage args)
    {
        var actor = args.Actor;

        switch (args.Action)
        {
            case OsShellAction.OpenApp when args.App is { } open:
                OpenApp(ent, open, actor);
                break;

            case OsShellAction.CloseApp when args.App is { } close:
                CloseApp(ent, close);
                break;

            case OsShellAction.MinimizeApp when args.App is { } min:
                MinimizeApp(ent, min);
                break;

            case OsShellAction.FocusApp when args.App is { } focus:
                FocusApp(ent, focus);
                break;

            case OsShellAction.UninstallApp when args.App is { } uninstall:
                if (TryComp(ent, out IS14OsMemoryComponent? memory)
                    && memory.Installed.TryGetValue(uninstall, out var entry))
                {
                    if (entry.Undeletable)
                    {
                        _popup.PopupEntity(Loc.GetString("is14-os-cannot-uninstall"), ent, actor);
                        break;
                    }

                    CloseApp(ent, uninstall);
                    _memory.Uninstall((ent.Owner, ent.Comp, memory), uninstall);
                }

                break;

            case OsShellAction.SetTheme when args.Arg is { } theme:
                if (_proto.TryIndex<IS14OsThemePrototype>(theme, out var themeProto) && !themeProto.Unlockable)
                    ent.Comp.Theme = theme;

                break;

            case OsShellAction.ToggleFlashlight:
                _flashlight.TryToggleLight(ent.Owner, actor);
                break;

            // The old PDA menu was the only way to reach the ringtone and the uplink. The OS
            // replaces that menu, so it has to keep those doors open (Docs §16.2).
            case OsShellAction.ShowRingtone:
                if (HasComp<RingerComponent>(ent))
                    _ringer.TryToggleRingerUi(ent.Owner, actor);

                return;

            case OsShellAction.ShowUplink:
                // Re-check the lock server side: a malicious client must not open a locked uplink.
                if (HasComp<UplinkComponent>(ent) && IsUplinkUnlocked(ent))
                    _store.ToggleUi(actor, ent.Owner);

                return;

            case OsShellAction.LockUplink:
                if (TryComp(ent, out RingerUplinkComponent? uplink))
                    _ringer.LockUplink((ent.Owner, uplink));

                break;

            case OsShellAction.CloseLid:
                CloseLid(ent);
                return;
        }

        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnAppMessage(Entity<IS14OsDeviceComponent> ent, ref IS14OsAppMessage args)
    {
        // Never trust the client that an app exists, is installed, or is open.
        if (!ent.Comp.Powered || !ent.Comp.Open.Contains(args.App))
            return;

        if (!TryComp(ent, out IS14OsMemoryComponent? memory) || !_memory.IsInstalled(memory, args.App))
            return;

        var appEv = new OsAppEventRaised(args.App, args.Event, args.Actor);
        RaiseLocalEvent(ent.Owner, ref appEv);
        UpdateUi(ent.Owner, ent.Comp);
    }

    /// <summary>Rebuilds and pushes the composite state.</summary>
    public void UpdateUi(EntityUid uid, IS14OsDeviceComponent? device = null)
    {
        if (!Resolve(uid, ref device, false))
            return;

        if (!_ui.HasUi(uid, IS14OsUiKey.Key) || !_ui.IsUiOpen(uid, IS14OsUiKey.Key))
            return;

        _pendingRefresh.Remove(uid);

        var memory = CompOrNull<IS14OsMemoryComponent>(uid);
        var profile = _memory.GetProfile(device);

        var shell = new OsShellState
        {
            Powered = device.Powered,
            Booting = device.BootEnd != null,
            BootEnd = device.BootEnd ?? TimeSpan.Zero,
            BootStart = device.BootEnd is { } end && profile != null ? end - profile.BootTime : TimeSpan.Zero,
            ProfileId = device.Profile.Id,
            ThemeId = device.Theme.Id,
            ShellMode = profile?.ShellMode ?? OsShellMode.Fullscreen,
            MaxWindows = profile?.MaxWindows ?? 1,
            ScreenSize = profile?.ScreenSize ?? new Vector2(400, 320),
            MemoryTotal = memory != null ? _memory.GetTotalMemory((uid, device, memory)) : 0,
            MemorySystem = _memory.GetSystemMemory(device),
            MemoryUsed = memory?.UsedMemory ?? 0,
            MemorySlotsFree = Math.Max(0, (profile?.MemorySlots ?? 0) - (memory?.UsedSlots ?? 0)),
            Battery = _power.GetCharge(uid),
            DeviceName = Name(uid),
            Open = new List<ProtoId<IS14OsAppPrototype>>(device.Open),
            Minimized = new List<ProtoId<IS14OsAppPrototype>>(device.Minimized),
        };

        if (memory != null)
            shell.Installed.AddRange(memory.Installed.Keys);

        foreach (var theme in _proto.EnumeratePrototypes<IS14OsThemePrototype>())
        {
            if (!theme.Unlockable)
                shell.Themes.Add(theme.ID);
        }

        FillStatus(uid, shell);

        var windows = new List<OsWindowState>(device.Open.Count);
        foreach (var app in device.Open)
        {
            var stateEv = new OsAppGetStateEvent(app);
            RaiseLocalEvent(uid, ref stateEv);
            windows.Add(new OsWindowState(app, device.Minimized.Contains(app), stateEv.State));
        }

        _ui.SetUiState(uid, IS14OsUiKey.Key, new IS14OsUiState(shell, windows));
    }

    /// <summary>
    ///     Owner, station and alert readouts. These live in the shell state because the taskbar
    ///     shows them too; the Status app just renders the same data.
    /// </summary>
    private void FillStatus(EntityUid uid, OsShellState shell)
    {
        if (TryComp(uid, out PdaComponent? pda))
        {
            shell.OwnerName = pda.OwnerName;
            shell.FlashlightOn = pda.FlashlightOn;

            if (TryComp(pda.ContainedId, out IdCardComponent? id))
            {
                shell.IdName = id.FullName;
                shell.IdJob = id.LocalizedJobTitle;
            }
        }

        // IS14 PDAs do not carry UnpoweredFlashlight today, so this hides the button rather
        // than offering a switch that does nothing.
        shell.HasFlashlight = HasComp<UnpoweredFlashlightComponent>(uid);
        shell.HasRinger = HasComp<RingerComponent>(uid);
        shell.HasUplink = HasComp<UplinkComponent>(uid) && IsUplinkUnlocked(uid);

        if (TryComp(uid, out DeviceNetworkComponent? network))
            shell.Address = network.Address;

        var station = _station.GetOwningStation(uid);
        if (station == null)
            return;

        shell.StationName = Name(station.Value);

        if (!TryComp(station, out AlertLevelComponent? alert) || alert.AlertLevels == null)
            return;

        shell.AlertLevel = alert.CurrentLevel;

        if (alert.AlertLevels.Levels.TryGetValue(alert.CurrentLevel, out var details))
        {
            shell.AlertColor = details.Color;
            shell.AlertInstructions = details.AlertLevelInstruction;
        }
    }

    private bool IsUplinkUnlocked(EntityUid uid)
    {
        return !TryComp<RingerUplinkComponent>(uid, out var uplink) || uplink.Unlocked;
    }

    #endregion
}

[ByRefEvent]
public record struct OsAppOpenedEvent(ProtoId<IS14OsAppPrototype> App, EntityUid? User);

[ByRefEvent]
public record struct OsAppClosedEvent(ProtoId<IS14OsAppPrototype> App);

/// <summary>Apps answer this with their own state when the shell rebuilds.</summary>
[ByRefEvent]
public record struct OsAppGetStateEvent(ProtoId<IS14OsAppPrototype> App)
{
    public IS14OsAppState? State = null;
}
