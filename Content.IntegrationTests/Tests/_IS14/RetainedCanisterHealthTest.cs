#nullable enable annotations
using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Atmos.Piping.Unary.Components;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedCanisterHealthTest
{
    [TestCase("OxygenCanister", 140, null)]
    [TestCase("BZCanister", 300, "BZCanisterBroken")]
    [TestCase("HealiumCanister", 300, "HealiumCanisterBroken")]
    [TestCase("NitriumCanister", 300, "NitriumCanisterBroken")]
    [TestCase("PluoxiumCanister", 300, "PluoxiumCanisterBroken")]
    public async Task CanisterKeepsGasUntilItsOwnBreakThreshold(string prototype, int threshold, string? broken)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid canister = default;
        float moles = 0;
        await server.WaitAssertion(() =>
        {
            canister = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            moles = server.EntMan.GetComponent<GasCanisterComponent>(canister).Air.TotalMoles;
            server.System<DamageableSystem>().TryChangeDamage(canister,
                new DamageSpecifier { DamageDict = { ["Blunt"] = threshold - 1 } }, true);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.EntityExists(canister), Is.True, "A parent's earlier threshold must not break the retained canister.");
            Assert.That(server.System<DamageableSystem>().GetTotalDamage(canister), Is.EqualTo(FixedPoint2.New(threshold - 1)));
            Assert.That(em.GetComponent<GasCanisterComponent>(canister).Air.TotalMoles, Is.EqualTo(moles).Within(0.005f),
                "A parent's earlier DumpCanisterBehavior must not vent the gas.");
            server.System<DamageableSystem>().TryChangeDamage(canister,
                new DamageSpecifier { DamageDict = { ["Blunt"] = 1 } }, true);
        });
        await pair.RunTicksSync(3);
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            Assert.That(em.EntityExists(canister), Is.False);
            if (broken != null)
                Assert.That(em.EntityQuery<MetaDataComponent>().Count(c => c.EntityPrototype?.ID == broken), Is.EqualTo(1));
            server.System<SharedMapSystem>().DeleteMap(map.MapId);
        });
        await pair.CleanReturnAsync();
    }
}
