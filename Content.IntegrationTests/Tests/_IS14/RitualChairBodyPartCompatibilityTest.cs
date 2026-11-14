using Robust.Shared.Prototypes;
using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Construction;
using Content.Shared.Atmos;
using Content.Shared.Body.Part;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Tag;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RitualChairBodyPartCompatibilityTest
{
    private static readonly ProtoId<TagPrototype> CompatibilityHeadTagId = "Head";

    [Test]
    public async Task RitualChairConsumesAGoobHeadAndReturnsAGoobSkull()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid human = default, head = default, cursed = default;
        EntityCoordinates coords = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var moles = new float[Atmospherics.AdjustedNumberOfGases];
            moles[(int) Gas.Oxygen] = 21.824779f;
            moles[(int) Gas.Nitrogen] = 82.10312f;
            server.System<AtmosphereSystem>().SetMapAtmosphere(map.MapUid, false,
                new GasMixture(moles, Atmospherics.T20C));
            for (var i = 1; i <= 3; i++)
                server.System<SharedMapSystem>().SetTile(map.Grid.Owner, map.Grid.Comp,
                    map.GridCoords.Offset(new Vector2(i, 0)), map.Tile.Tile);
            var userCoords = map.GridCoords.Offset(new Vector2(1.5f, 0.5f));
            coords = userCoords.Offset(Vector2.UnitX);
            human = em.SpawnEntity("MobHuman", userCoords);
            head = em.SpawnEntity("HeadHuman", userCoords);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(human, head), Is.True);
            var chair = em.SpawnEntity("ChairRitual", coords);
            Assert.That(server.System<ConstructionSystem>().SetPathfindingTarget(chair, "chairCursed"), Is.True);
            em.EventBus.RaiseLocalEvent(chair, new InteractUsingEvent(human, head, chair, coords));
        });
        await pair.RunSeconds(3);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.EntityExists(head), Is.False, "The human head must be consumed by construction.");
            var chairs = em.EntityQuery<MetaDataComponent>().Where(c => c.EntityPrototype?.ID == "ChairCursed").ToArray();
            Assert.That(chairs, Has.Length.EqualTo(1));
            cursed = chairs[0].Owner;
            var crowbar = em.SpawnEntity("Crowbar", coords);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(human, crowbar), Is.True);
            Assert.That(server.System<ConstructionSystem>().SetPathfindingTarget(cursed, "chairRitual"), Is.True);
            em.EventBus.RaiseLocalEvent(cursed, new InteractUsingEvent(human, crowbar, cursed, coords));
        });
        await pair.RunSeconds(3);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.EntityExists(cursed), Is.False);
            var skulls = em.EntityQuery<MetaDataComponent>().Where(c => c.EntityPrototype?.ID == "HeadSkeleton").ToArray();
            Assert.That(skulls, Has.Length.EqualTo(1));
            Assert.That(em.GetComponent<BodyPartComponent>(skulls[0].Owner).PartType, Is.EqualTo(BodyPartType.Head));
            Assert.That(server.System<TagSystem>().HasTag(skulls[0].Owner, CompatibilityHeadTagId), Is.True);
            Assert.That(em.EntityQuery<MetaDataComponent>().Count(c => c.EntityPrototype?.ID == "ChairRitual"), Is.EqualTo(1));
            server.System<SharedMapSystem>().DeleteMap(map.MapId);
        });
        await pair.CleanReturnAsync();
    }
}
