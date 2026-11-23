using System.Linq;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.GameRules;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Antag.Components;
using Content.Shared.GameTicking;
using Content.Shared.Xenoborgs.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class XenoborgEntryCompatibilityTest : AntagTest
{
    private static readonly EntProtoId XenoborgRule = "Xenoborgs";

    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        Map = PoolManager.TestStation
    };

    [Test]
    [RunOnSide(Side.Server)]
    public void XenoborgRuleLoadsMothershipAndSpawnsPlayableIS14Roles()
    {
        Assert.That(STicker.StartGameRule(XenoborgRule, out var rule), Is.True);
        var grids = SEntMan.GetComponent<RuleGridsComponent>(rule!.Value.Owner);
        Assert.That(grids.MapGrids, Is.Not.Empty);

        var spawners = SEntMan.EntityQuery<GhostRoleAntagSpawnerComponent>()
            .Where(c => c.Rule == rule.Value.Owner).ToArray();
        Assert.That(spawners.Length, Is.EqualTo(5));
        Assert.That(spawners.Count(c => c.Definition?.Id == "IS14MothershipCoreAntag"), Is.EqualTo(1));
        Assert.That(spawners.Count(c => c.Definition?.Id == "IS14XenoborgsAntag"), Is.EqualTo(4));

        foreach (var spawner in spawners)
        {
            var ghostRole = SEntMan.GetComponent<GhostRoleComponent>(spawner.Owner);
            Assert.That(Server.System<GhostRoleSystem>().Takeover(ServerSession!, ghostRole.Identifier), Is.True);
            var body = ServerSession!.AttachedEntity!.Value;
            Assert.That(grids.MapGrids, Does.Contain(SEntMan.GetComponent<TransformComponent>(body).GridUid));
            if (spawner.Definition?.Id == "IS14MothershipCoreAntag")
                Assert.That(SEntMan.HasComponent<MothershipCoreComponent>(body), Is.True);
            else
            {
                Assert.That(SEntMan.HasComponent<CyborgComponent>(body), Is.True);
                Assert.That(SEntMan.GetComponent<MetaDataComponent>(body).EntityPrototype!.ID,
                    Does.StartWith("IS14Xenoborg"));
            }
        }

        STicker.ClearGameRules();
    }
}
