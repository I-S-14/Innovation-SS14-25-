using System.Linq;
using System.Numerics;
using Content.Server.Body.Systems;
using Content.Shared._Shitmed.Medical.Surgery;
using Content.Shared._Shitmed.Medical.Surgery.Steps.Parts;
using Content.Shared.Body.Part;
using Content.Shared.Buckle;
using Content.Shared.Hands.EntitySystems;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class SurgeryIncisionCompatibilityTest
{
    [Test]
    public async Task HumanArmCanBeOpenedRetractedAndCauterizedWithRealTools()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        EntityUid map = default, patient = default, surgeon = default, arm = default;
        EntityCoordinates coordinates = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            map = server.System<SharedMapSystem>().CreateMap(out var mapId);
            Assert.That(server.System<MapLoaderSystem>().TryLoadGrid(mapId,
                new ResPath("Maps/Test/Breathing/3by3-20oxy-80nit.yml"), out var grid), Is.True);
            coordinates = new EntityCoordinates(grid!.Value.Owner, new Vector2(0.5f, 0.5f));
            var table = em.SpawnEntity("OperatingTable", coordinates);
            patient = em.SpawnEntity("MobHuman", coordinates);
            surgeon = em.SpawnEntity("MobHuman", new EntityCoordinates(coordinates.EntityId, new Vector2(1.5f, 0.5f)));
            Assert.That(server.System<SharedBuckleSystem>().TryBuckle(patient, patient, table, popup: false), Is.True);
            arm = server.System<BodySystem>().GetBodyChildrenOfType(patient, BodyPartType.Arm).First().Id;
            Assert.That(em.HasComponent<IncisionOpenComponent>(arm), Is.False);
            Assert.That(em.HasComponent<SkinRetractedComponent>(arm), Is.False);
        });
        await pair.RunSeconds(1);

        async Task Perform(string toolPrototype, string surgery, string step)
        {
            EntityUid tool = default;
            await server.WaitAssertion(() =>
            {
                tool = server.EntMan.SpawnEntity(toolPrototype, coordinates);
                Assert.That(server.System<SharedHandsSystem>().TryPickupAnyHand(surgeon, tool), Is.True);
                Assert.That(server.System<SharedSurgerySystem>().TryDoSurgeryStep(patient, arm, surgeon,
                    surgery, step, out var error), Is.True, $"{step}: {error}");
            });
            // Unskilled surgeons use the retained 0.25 speed multiplier; do not bypass the do-after.
            await pair.RunSeconds(10);
            await server.WaitAssertion(() => server.EntMan.DeleteEntity(tool));
        }

        await Perform("Scalpel", "SurgeryOpenIncision", "SurgeryStepOpenIncisionScalpel");
        await server.WaitAssertion(() => Assert.That(server.EntMan.HasComponent<IncisionOpenComponent>(arm), Is.True));
        await Perform("Retractor", "SurgeryOpenIncision", "SurgeryStepRetractSkin");
        await server.WaitAssertion(() => Assert.That(server.EntMan.HasComponent<SkinRetractedComponent>(arm), Is.True));
        await Perform("Cautery", "SurgeryCloseIncision", "SurgeryStepCloseIncision");
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.HasComponent<IncisionOpenComponent>(arm), Is.False);
            Assert.That(server.EntMan.HasComponent<SkinRetractedComponent>(arm), Is.False);
            Assert.That(server.System<BodySystem>().GetBodyChildrenOfType(patient, BodyPartType.Arm).Select(p => p.Id),
                Does.Contain(arm));
            server.EntMan.DeleteEntity(map);
        });
        await pair.CleanReturnAsync();
    }
}
