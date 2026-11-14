using Robust.Shared.Prototypes;
using System.Linq;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.GameRules;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._Goobstation.Wizard;
using Content.Shared.Antag;
using Content.Shared.Antag.Components;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class WizardEntryCompatibilityTest : AntagTest
{
    private static readonly ProtoId<AntagSpecifierPrototype> CompatibilityWizardAntagSpecifierId = "Wizard";
    private readonly EntProtoId CompatibilityIS14TestIncomingWizardRuleEntityId = "IS14TestIncomingWizardRule";

    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        Map = PoolManager.TestStation
    };

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: IS14TestIncomingWizardRule
  parent: WizardMidround
  components:
  - type: AntagSelection
    antags:
    - !type:LinearAntagCount
      proto: Wizard
      range: { min: 1, max: 1 }
      playerRatio: 20
";

    [Test]
    [RunOnSide(Side.Server)]
    public void IncomingWizardEntryRetainsGoobAbilitiesAndEquipment()
    {
        STicker.StartGameRule(CompatibilityIS14TestIncomingWizardRuleEntityId, out var rule);
        Assert.That(rule, Is.Not.Null);
        var spawner = SEntMan.EntityQuery<GhostRoleAntagSpawnerComponent>()
            .Single(c => c.Rule == rule!.Value.Owner);
        Assert.That(spawner.Definition?.Id, Is.EqualTo("Wizard"));
        var ghostRole = SEntMan.GetComponent<GhostRoleComponent>(spawner.Owner);
        Assert.That(Server.System<GhostRoleSystem>().Takeover(ServerSession!, ghostRole.Identifier), Is.True);
        SAssertAntagInitialized(SProtoMan.Index<AntagSpecifierPrototype>(CompatibilityWizardAntagSpecifierId), ServerSession!);
        var wizard = ServerSession!.AttachedEntity!.Value;
        Assert.That(SEntMan.HasComponent<WizardComponent>(wizard), Is.True);
        foreach (var (slot, expected) in new[]
                 {
                     ("head", "ClothingHeadHatWizardReal"),
                     ("outerClothing", "ClothingOuterWizardReal"),
                     ("pocket2", "WizardsGrimoire")
                 })
        {
            Assert.That(Server.System<InventorySystem>().TryGetSlotEntity(wizard, slot, out var item), Is.True, slot);
            Assert.That(SEntMan.GetComponent<MetaDataComponent>(item!.Value).EntityPrototype!.ID, Is.EqualTo(expected));
        }
        STicker.ClearGameRules();
    }
}
