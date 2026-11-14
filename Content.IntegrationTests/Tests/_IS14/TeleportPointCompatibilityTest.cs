using Robust.Shared.Prototypes;
using System.Collections.Generic;
using System.Linq;
using Content.Shared.Tag;
using Content.Shared.Teleportation.Components;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class TeleportPointCompatibilityTest
{
    private static readonly ProtoId<TagPrototype> CompatibilityGhostOnlyWarpTagId = "GhostOnlyWarp";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: IS14TestTeleportAllowed
  components:
  - type: WarpPoint
    location: is14-upstream-warp-monumentcosmiccultbase
  - type: Tag
    tags: [TeleportScrollWarp]

- type: entity
  parent: IS14TestTeleportAllowed
  id: IS14TestTeleportLegacyExcluded
  components:
  - type: WarpPoint
    blacklist:
      tags: [GhostOnlyWarp]
  - type: Tag
    tags: [TeleportScrollWarp, GhostOnlyWarp]

- type: entity
  parent: IS14TestTeleportAllowed
  id: IS14TestTeleportModernExcluded
  components:
  - type: Tag
    tags: [TeleportScrollWarp, TeleportScrollWarpBlacklist]

- type: entity
  parent: IS14TestTeleportAllowed
  id: IS14TestTeleportMissingWhitelist
  components:
  - type: Tag
    tags: []

- type: entity
  parent: IS14TestTeleportAllowed
  id: IS14TestTeleportNonmatchingExclusion
  components:
  - type: WarpPoint
    blacklist:
      tags: [GhostOnlyWarp]

- type: entity
  parent: WizardTeleportScroll
  id: IS14TestTeleportUnfilteredScroll
  components:
  - type: TeleportLocations
    whitelist: null
    blacklist: null
";

    [TestCase(true)]
    [TestCase(false)]
    public async Task ScrollCombinesPointExclusionsWithItsOwnFilters(bool modernFilters)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var ids = new[]
            {
                "IS14TestTeleportAllowed", "IS14TestTeleportLegacyExcluded", "IS14TestTeleportModernExcluded",
                "IS14TestTeleportMissingWhitelist", "IS14TestTeleportNonmatchingExclusion",
            };
            var markers = ids.ToDictionary(id => id, id => em.SpawnEntity(id, map.GridCoords));
            var actor = em.SpawnEntity("MobHuman", map.GridCoords);
            var scroll = em.SpawnEntity(modernFilters ? "WizardTeleportScroll" : "IS14TestTeleportUnfilteredScroll", map.GridCoords);
            var teleporter = em.GetComponent<TeleportLocationsComponent>(scroll);
            var expected = new HashSet<NetEntity>
            {
                em.GetNetEntity(markers["IS14TestTeleportAllowed"]),
                em.GetNetEntity(markers["IS14TestTeleportNonmatchingExclusion"]),
            };
            if (!modernFilters)
            {
                expected.Add(em.GetNetEntity(markers["IS14TestTeleportModernExcluded"]));
                expected.Add(em.GetNetEntity(markers["IS14TestTeleportMissingWhitelist"]));
            }
            Assert.That(teleporter.AvailableWarps.Select(p => p.TelePoint), Is.EquivalentTo(expected));

            // Refreshing the real menu must re-evaluate each point's own exclusion.
            server.System<TagSystem>().RemoveTag(markers["IS14TestTeleportLegacyExcluded"], CompatibilityGhostOnlyWarpTagId);
            em.EventBus.RaiseLocalEvent(scroll, new BeforeActivatableUIOpenEvent(actor));
            expected.Add(em.GetNetEntity(markers["IS14TestTeleportLegacyExcluded"]));
            Assert.That(teleporter.AvailableWarps.Select(p => p.TelePoint), Is.EquivalentTo(expected));

            em.DeleteEntity(scroll);
            em.DeleteEntity(actor);
            foreach (var marker in markers.Values)
                em.DeleteEntity(marker);
        });
        await pair.CleanReturnAsync();
    }
}
