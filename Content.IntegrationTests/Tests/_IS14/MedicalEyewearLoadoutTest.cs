using Content.Shared.Inventory;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Station.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class MedicalEyewearLoadoutTest
{
    private readonly ProtoId<RoleLoadoutPrototype> CompatibilityIS14TestMedicalEyewearRoleLoadoutId = "IS14TestMedicalEyewear";

    [TestPrototypes]
    private const string Prototypes = @"
- type: roleLoadout
  id: IS14TestMedicalEyewear
  groups: [MedicalEyewear]
";

    [TestCase("MedicalHud", "ClothingEyesHudMedical")]
    [TestCase("MedicalEyePatchHud", "ClothingEyesEyepatchHudMedical")]
    public async Task ApplyingTheSelectedLoadoutEquipsTheMedicalHud(string selected, string expected)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid human = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            human = em.SpawnEntity("MobHuman", map.GridCoords);
            var loadout = new RoleLoadout("IS14TestMedicalEyewear");
            loadout.SelectedLoadouts["MedicalEyewear"] = [new Loadout { Prototype = selected }];
            var role = server.ResolveDependency<IPrototypeManager>().Index<RoleLoadoutPrototype>(CompatibilityIS14TestMedicalEyewearRoleLoadoutId);
            server.System<StationSpawningSystem>().EquipRoleLoadout(human, loadout, role);
            Assert.That(server.System<InventorySystem>().TryGetSlotEntity(human, "eyes", out var eyewear), Is.True);
            Assert.That(em.GetComponent<MetaDataComponent>(eyewear!.Value).EntityPrototype!.ID, Is.EqualTo(expected));
        });
        await pair.RunTicksSync(3);
        await server.WaitPost(() => server.EntMan.DeleteEntity(human));
        await pair.CleanReturnAsync();
    }
}
