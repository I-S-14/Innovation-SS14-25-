using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Doors.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedMaterialDoorTest
{
    [TestCase("MetalDoor")]
    [TestCase("BananiumDoor")]
    public async Task RestoredDoorOpensAndRetainsItsDestructionThreshold(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid door = default;
        await server.WaitAssertion(() =>
        {
            door = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            Assert.That(server.System<SharedDoorSystem>().TryOpen(door), Is.True);
            server.System<DamageableSystem>().TryChangeDamage(door,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 149 } }, true);
            Assert.That(server.EntMan.EntityExists(door), Is.True);
        });
        await pair.RunSeconds(2);
        await server.WaitAssertion(() => server.System<DamageableSystem>().TryChangeDamage(door,
            new DamageSpecifier { DamageDict = { ["Blunt"] = 1 } }, true));
        await PoolManager.WaitUntil(server, () => !server.EntMan.EntityExists(door));
        await pair.CleanReturnAsync();
    }
}
