using System.Linq;
using Content.Goobstation.Shared.Enchanting.Components;
using Content.Goobstation.Shared.Wraith.Curses;
using Content.Shared._CorvaxGoob.Skills;
using Content.Shared.Implants.Components;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RetainedErtJobsTest
{
    [TestCase("ERTLeader")]
    [TestCase("ERTChaplain")]
    [TestCase("ERTEngineer")]
    [TestCase("ERTSecurity")]
    [TestCase("ERTMedical")]
    [TestCase("ERTJanitor")]
    public async Task JobSpecialsInstallRetainedGoobImplants(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var job = server.ResolveDependency<IPrototypeManager>().Index<JobPrototype>(prototype);
            Assert.That(job.CanBeAntag, Is.False);
            if (prototype == "ERTLeader")
                Assert.That(job.Skills, Does.Contain(Skills.All));
            Assert.That(job.Access.Select(id => id.Id), Is.SupersetOf(new[]
            {
                "CentralCommand", "NanotrasenRepresentative", "BlueshieldOfficer",
            }));
            var human = em.SpawnEntity("MobHuman", map.GridCoords);
            foreach (var special in job.Special)
                special.AfterEquip(human);
            if (prototype == "ERTChaplain")
            {
                Assert.That(em.HasComponent<CurseImmuneComponent>(human), Is.True);
                Assert.That(em.HasComponent<CanEnchantComponent>(human), Is.True);
            }
            var implants = em.GetComponent<ImplantedComponent>(human).ImplantContainer.ContainedEntities;
            Assert.That(implants.Select(uid => em.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID),
                Is.EquivalentTo(new[]
                {
                    "MindShieldImplant", "FreedomImplant", "BluespaceLifelineImplant",
                    "RadioImplantCentcomm", "NutrimentPumpImplant", "StypticStimulatorImplant",
                    "DeathRattleImplantCentcomm",
                }));
            em.DeleteEntity(human);
        });
        await pair.CleanReturnAsync();
    }
}
