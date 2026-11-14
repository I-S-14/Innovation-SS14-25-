using Robust.Shared.Prototypes;
using System.Collections.Generic;
using Content.Server.GameTicking;
using Content.Shared.Antag.Components;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Random;

namespace Content.IntegrationTests.Tests._IS14;

/// <summary>Fallback selection must fill remaining slots after the actual primary candidate pool is exhausted.</summary>
[TestFixture]
public sealed class AntagFallbackPreferenceTest
{
    private readonly EntProtoId CompatibilityIS14TestFallbackRuleEntityId = "IS14TestFallbackRule";

    [TestPrototypes]
    private const string Prototypes = @"
- type: antagSpecifier
  id: IS14TestFallbackSpecifier
  prefRoles: [NukeopsCommander]
  fallbackRoles: [Traitor]

- type: entity
  parent: BaseGameRule
  id: IS14TestFallbackRule
  components:
  - type: AntagSelection
    selectionTime: PrePlayerSpawn
    antags:
    - !type:FixedAntagCount
      proto: IS14TestFallbackSpecifier
      count: 2
";

    [TestCase(false)]
    [TestCase(true)]
    public async Task FallbackUsesOnlyRemainingCandidates(bool includePrimary)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            DummyTicker = false,
            Connected = true,
            InLobby = true,
        });
        var server = pair.Server;
        var ticker = server.System<ServerGameTicker>();
        var primary = (await server.AddDummySessions(1))[0];
        await pair.RunTicksSync(5);
        await pair.SetAntagPreference("Traitor", true);
        await pair.SetAntagPreference("NukeopsCommander", true, primary.UserId);

        await server.WaitPost(() =>
        {
            var rule = ticker.AddGameRule(CompatibilityIS14TestFallbackRuleEntityId, force: true);
            Assert.That(rule, Is.Not.Null);
            var uid = rule!.Value.Owner;
            var selection = server.EntMan.GetComponent<AntagSelectionComponent>(uid);
            var pool = new List<ICommonSession> { pair.Player! };
            if (includePrimary)
                pool.Add(primary);

            var profiles = new Dictionary<Robust.Shared.Network.NetUserId, HumanoidCharacterProfile>();
            foreach (var session in pool)
                profiles.Add(session.UserId, new HumanoidCharacterProfile());

            // The fallback candidate is visited first with this seed. It must be
            // retried after the primary is selected, rather than leaving a slot empty.
            server.ResolveDependency<IRobustRandom>().SetSeed(1);
            server.EntMan.EventBus.RaiseEvent(EventSource.Local, new RulePlayerSpawningEvent(pool, profiles, false));

            var selections = selection.PreSelectedSessions;
            Assert.That(selections.TryGetValue("IS14TestFallbackSpecifier", out var selected), Is.True);
            Assert.That(selected, Does.Contain(pair.Player));
            Assert.That(selected, Has.Count.EqualTo(includePrimary ? 2 : 1),
                "A connected primary outside this candidate pool must not suppress fallback selection.");
            server.EntMan.DeleteEntity(uid);
        });
        await pair.CleanReturnAsync();
    }
}
