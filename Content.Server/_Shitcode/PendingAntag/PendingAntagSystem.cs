// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Antag;
using Content.Server.Antag.Components;
using Content.Shared.GameTicking;
using Content.Shared.Roles;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Content.Shared.Antag;
using Content.Shared.Antag.Components;

namespace Content.Server._Goobstation.PendingAntag;

public sealed class PendingAntagSystem : EntitySystem
{
    [Dependency] private readonly AntagSelectionSystem _selection = default!;

    public Dictionary<NetUserId, (AntagSpecifierPrototype, Entity<AntagSelectionComponent>)> PendingAntags = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (ev.LateJoin)
            return;

        if (ev.JobId is not { } job || !PendingAntags.TryGetValue(ev.Player.UserId, out var pendingAntag))
            return;

        var specifier = pendingAntag.Item1;
        if (specifier.JobWhitelist is { } whitelist && !whitelist.Contains(job)
            || specifier.JobBlacklist is { } blacklist && blacklist.Contains(job))
            return;

        PendingAntags.Remove(ev.Player.UserId);
        _selection.TryMakeAntag(pendingAntag.Item2, pendingAntag.Item1, ev.Player, true);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        PendingAntags.Clear();
    }
}
