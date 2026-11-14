#nullable enable annotations
using Content.Server.Humanoid;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Content.Shared.MagicMirror;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class MagicMirrorCompatibilityTest
{
    [TestCase("Mirror", false)]
    [TestCase("MirrorModern", false)]
    [TestCase("BarberScissors", true)]
    public async Task HairChangesUseTheRetainedEditorAndCorrectDelay(string prototype, bool delayed)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid human = default;
        EntityUid mirror = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            human = em.SpawnEntity("MobHuman", map.GridCoords);
            mirror = em.SpawnEntity(prototype, map.GridCoords);
            var appearance = em.GetComponent<HumanoidAppearanceComponent>(human);
            var humanoids = server.System<HumanoidAppearanceSystem>();
            if (!appearance.MarkingSet.TryGetCategory(MarkingCategories.Hair, out var hair) || hair.Count == 0)
                humanoids.AddMarking(human, "HumanHairLong", forced: true);
            else
                humanoids.SetMarkingId(human, MarkingCategories.Hair, 0, "HumanHairLong");
            Assert.That(Hair(em, human), Is.EqualTo("HumanHairLong"));
            if (prototype == "BarberScissors")
                Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(human, mirror), Is.True);

            // The UI has selected this humanoid. Dispatch its normal authenticated
            // message payload through the same BUI event handler as the game.
            em.GetComponent<MagicMirrorComponent>(mirror).Target = human;
            var message = new MagicMirrorSelectMessage(MagicMirrorCategory.Hair, "HumanHairFrenchBraid", 0)
            {
                Actor = human,
                UiKey = MagicMirrorUiKey.Key,
            };
            em.EventBus.RaiseLocalEvent(mirror, message);
        });
        await server.WaitRunTicks(1);
        await server.WaitAssertion(() =>
            Assert.That(Hair(server.EntMan, human),
                Is.EqualTo(delayed ? "HumanHairLong" : "HumanHairFrenchBraid")));
        await PoolManager.WaitUntil(server, () => Hair(server.EntMan, human) == "HumanHairFrenchBraid");
        await server.WaitAssertion(() =>
        {
            server.EntMan.DeleteEntity(mirror);
            server.EntMan.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }

    private static string? Hair(IEntityManager entities, EntityUid human)
    {
        return entities.GetComponent<HumanoidAppearanceComponent>(human).MarkingSet
            .TryGetCategory(MarkingCategories.Hair, out var hair) && hair.Count > 0 ? hair[0].MarkingId : null;
    }
}
