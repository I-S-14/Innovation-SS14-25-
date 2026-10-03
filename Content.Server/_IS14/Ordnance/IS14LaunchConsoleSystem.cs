// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.DeviceLinking.Systems;
using Content.Server.Power.EntitySystems;
using Content.Shared._IS14.Ordnance;
using Content.Shared.DeviceLinking;
using Content.Shared.DeviceLinking.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Ordnance;

/// <summary>
/// Runs the launch sequence and fires its three signals: one before the shot, one at the shot, one
/// after it. What each signal does is decided by whatever the chamber is wired to.
/// </summary>
public sealed class IS14LaunchConsoleSystem : EntitySystem
{
    [Dependency] private readonly DeviceLinkSystem _link = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Links change while somebody stands at the console with a configurator, and the window
        // shows how many devices are wired — so refresh on those instead of polling every frame.
        SubscribeLocalEvent<IS14LaunchConsoleComponent, NewLinkEvent>((uid, comp, _) => UpdateUi((uid, comp)));
        SubscribeLocalEvent<IS14LaunchConsoleComponent, PortDisconnectedEvent>((uid, comp, _) => UpdateUi((uid, comp)));

        Subs.BuiEvents<IS14LaunchConsoleComponent>(IS14LaunchConsoleUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>((uid, comp, _) => UpdateUi((uid, comp)));
            subs.Event<IS14LaunchConsoleStartMessage>(OnStart);
            subs.Event<IS14LaunchConsoleAbortMessage>(OnAbort);
            subs.Event<IS14LaunchConsoleSetCountdownMessage>(OnSetCountdown);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<IS14LaunchConsoleComponent>();
        while (query.MoveNext(out var uid, out var console))
        {
            var ent = (uid, console);

            if (console.LaunchAt is { } launchAt)
                RunSequence(ent, launchAt);

            // Only when the number on screen would actually change. Pushing a state every frame
            // made the delay field unusable: each push overwrote what the player was typing.
            var second = console.LaunchAt is { } at
                ? Math.Max(0, (int) Math.Ceiling((at - _timing.CurTime).TotalSeconds))
                : -1;

            if (second == console.LastShownSecond)
                continue;

            console.LastShownSecond = second;

            if (_ui.IsUiOpen(uid, IS14LaunchConsoleUiKey.Key))
                UpdateUi(ent);
        }
    }

    /// <summary>
    /// Walks the three moments of a launch: shutters get the lead time to open, the driver fires on
    /// the dot, and the late signal goes out once the same lead time has passed.
    /// </summary>
    private void RunSequence(Entity<IS14LaunchConsoleComponent> ent, TimeSpan launchAt)
    {
        var now = _timing.CurTime;
        var console = ent.Comp;

        if (!console.PreFired && now >= launchAt - console.LeadTime)
        {
            console.PreFired = true;
            _link.InvokePort(ent.Owner, console.PreLaunchPort);
        }

        if (!console.LaunchFired && now >= launchAt)
        {
            console.LaunchFired = true;
            _link.InvokePort(ent.Owner, console.LaunchPort);
        }

        if (now < launchAt + console.LeadTime)
            return;

        _link.InvokePort(ent.Owner, console.PostLaunchPort);

        console.LaunchAt = null;
        console.PreFired = false;
        console.LaunchFired = false;
    }

    private void OnStart(Entity<IS14LaunchConsoleComponent> ent, ref IS14LaunchConsoleStartMessage args)
    {
        if (!this.IsPowered(ent.Owner, EntityManager) || ent.Comp.LaunchAt != null)
            return;

        ent.Comp.LaunchAt = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Countdown);
        ent.Comp.PreFired = false;
        ent.Comp.LaunchFired = false;

        _audio.PlayPvs(ent.Comp.AlarmSound, ent.Owner);
        UpdateUi(ent);
    }

    private void OnAbort(Entity<IS14LaunchConsoleComponent> ent, ref IS14LaunchConsoleAbortMessage args)
    {
        if (ent.Comp.LaunchAt == null)
            return;

        // Aborting after the shutters opened still closes them: the late signal is the only thing
        // that seals the chamber, so it has to go out even when the launch never happened.
        if (ent.Comp.PreFired)
            _link.InvokePort(ent.Owner, ent.Comp.PostLaunchPort);

        ent.Comp.LaunchAt = null;
        ent.Comp.PreFired = false;
        ent.Comp.LaunchFired = false;

        UpdateUi(ent);
    }

    private void OnSetCountdown(Entity<IS14LaunchConsoleComponent> ent, ref IS14LaunchConsoleSetCountdownMessage args)
    {
        if (ent.Comp.LaunchAt != null)
            return;

        ent.Comp.Countdown = Math.Clamp(args.Seconds, ent.Comp.MinCountdown, ent.Comp.MaxCountdown);
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<IS14LaunchConsoleComponent> ent)
    {
        var state = new IS14LaunchConsoleUiState
        {
            Countdown = ent.Comp.Countdown,
            LeadSeconds = (int) ent.Comp.LeadTime.TotalSeconds,
            PreLinks = CountLinks(ent, ent.Comp.PreLaunchPort),
            LaunchLinks = CountLinks(ent, ent.Comp.LaunchPort),
            PostLinks = CountLinks(ent, ent.Comp.PostLaunchPort),
        };

        if (ent.Comp.LaunchAt is { } launchAt)
            state.SecondsLeft = Math.Max(0, (int) Math.Ceiling((launchAt - _timing.CurTime).TotalSeconds));

        _ui.SetUiState(ent.Owner, IS14LaunchConsoleUiKey.Key, state);
    }

    /// <summary>How many devices are wired to one of the console's outputs.</summary>
    private int CountLinks(Entity<IS14LaunchConsoleComponent> ent, string port)
    {
        if (!TryComp<DeviceLinkSourceComponent>(ent.Owner, out var source))
            return 0;

        var count = 0;

        foreach (var (_, links) in source.LinkedPorts)
        {
            foreach (var (sourcePort, _) in links)
            {
                if (sourcePort == port)
                    count++;
            }
        }

        return count;
    }
}
