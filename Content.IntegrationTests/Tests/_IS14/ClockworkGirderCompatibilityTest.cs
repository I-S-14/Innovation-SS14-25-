using System.Linq;
using System.Numerics;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Construction;
using Content.Shared.Atmos;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Stacks;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class ClockworkGirderCompatibilityTest
{
    [Test]
    public async Task BrassBuildsAGirderThatRetainsItsGoobDestructionAndSalvage()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid human = default, material = default, girder = default;
        Task<bool> construction = null!;
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
            var coords = map.GridCoords.Offset(new Vector2(1.5f, 0.5f));
            human = em.SpawnEntity("MobHuman", coords);
            material = em.SpawnEntity("SheetBrass", coords);
            server.System<SharedStackSystem>().SetCount(material, 3);
            Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(human, material), Is.True);
            construction = server.System<ConstructionSystem>().TryStartStructureConstruction(human,
                "ClockworkGirder", coords.Offset(Vector2.UnitX), Angle.Zero);
            Assert.That(construction.IsCompleted, Is.False);
        });
        await pair.RunSeconds(2);
        await server.WaitAssertion(() => Assert.That(construction.IsCompletedSuccessfully, Is.True));
        Assert.That(await construction, Is.True);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.GetComponent<StackComponent>(material).Count, Is.EqualTo(1));
            var girders = em.EntityQuery<MetaDataComponent>().Where(c => c.EntityPrototype?.ID == "ClockworkGirder").ToArray();
            Assert.That(girders, Has.Length.EqualTo(1));
            girder = girders[0].Owner;
            var damage = server.System<DamageableSystem>();
            damage.TryChangeDamage(girder, new DamageSpecifier { DamageDict = { ["Blunt"] = 49 } }, true);
            Assert.That(em.EntityExists(girder), Is.True);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.EntityExists(girder), Is.True, "An inherited earlier threshold must not destroy the gear.");
            Assert.That(server.EntMan.GetComponent<MetaDataComponent>(girder).EntityPrototype!.ID, Is.EqualTo("ClockworkGirder"));
            var damage = server.System<DamageableSystem>();
            Assert.That(damage.GetTotalDamage(girder), Is.EqualTo(FixedPoint2.New(49)));
            damage.TryChangeDamage(girder, new DamageSpecifier { DamageDict = { ["Blunt"] = 1 } }, true);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.EntityQuery<MetaDataComponent>().Any(c => c.EntityPrototype?.ID == "ClockworkGirder"), Is.False);
            foreach (var prototype in new[] { "SheetBrass1", "PartRodMetal1" })
            {
                var count = em.EntityQuery<StackComponent>().Where(c =>
                    em.GetComponent<MetaDataComponent>(c.Owner).EntityPrototype?.ID == prototype).Sum(c => c.Count);
                Assert.That(count, Is.InRange(1, 2), prototype);
            }
            server.System<SharedMapSystem>().DeleteMap(map.MapId);
        });
        await pair.CleanReturnAsync();
    }
}
