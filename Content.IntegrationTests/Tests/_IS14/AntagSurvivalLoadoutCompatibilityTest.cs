using System.Linq;
using Content.Shared.Inventory;
using Content.Shared.Preferences;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Station.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class AntagSurvivalLoadoutCompatibilityTest
{
    private static readonly ProtoId<RoleLoadoutPrototype> CompatibilityRoleSurvivalExtendedAntagRoleLoadoutId = "RoleSurvivalExtendedAntag";

    [TestCase("Human", "MobHuman", "EmergencyOxygenExtendedAntag", "BoxSurvivalDeluxeExtended", "ExtendedEmergencyOxygenTankFilled")]
    [TestCase("Vox", "MobVox", "EmergencyNitrogenExtendedAntag", "BoxSurvivalDeluxeExtendedNitrogen", "ExtendedEmergencyNitrogenTankFilled")]
    public async Task DefaultSurvivalGearMatchesTheSpeciesAndContainsSupplies(string species, string mob,
        string expectedLoadout, string expectedBox, string expectedTank)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var profile = HumanoidCharacterProfile.DefaultWithSpecies(species);
            var loadout = new RoleLoadout("RoleSurvivalExtendedAntag");
            loadout.SetDefault(profile, null, prototypes);
            var selected = loadout.SelectedLoadouts["SurvivalExtendedAntag"];
            Assert.That(selected.Any(l => l.Prototype.Id == expectedLoadout), Is.True);
            Assert.That(selected.Count(l => l.Prototype.Id.StartsWith("Emergency")), Is.EqualTo(1),
                "The default must not include both oxygen and nitrogen survival boxes.");
            var human = em.SpawnEntity(mob, map.GridCoords);
            var backpack = em.SpawnEntity("ClothingBackpack", map.GridCoords);
            Assert.That(server.System<InventorySystem>().TryEquip(human, backpack, "back", silent: true), Is.True);
            server.System<StationSpawningSystem>().EquipRoleLoadout(human, loadout,
                prototypes.Index<RoleLoadoutPrototype>(CompatibilityRoleSurvivalExtendedAntagRoleLoadoutId));
            var box = em.EntityQuery<MetaDataComponent>().Single(m => m.EntityPrototype?.ID == expectedBox).Owner;
            var containers = server.System<SharedContainerSystem>();
            Assert.That(containers.TryGetContainingContainer((box, null, null), out var parent), Is.True);
            Assert.That(parent!.Owner, Is.EqualTo(backpack));
            Assert.That(containers.TryGetContainer(box, "storagebase", out var supplies), Is.True);
            var contents = supplies!.ContainedEntities.Select(e => em.GetComponent<MetaDataComponent>(e).EntityPrototype!.ID).ToArray();
            Assert.That(contents, Does.Contain(expectedTank));
            Assert.That(contents, Does.Contain("FoodSnackNutribrick"));
            Assert.That(contents, Does.Contain("DrinkWaterBottleFull"));
            Assert.That(contents, Does.Contain("ClothingMaskBreath"));
        });
        await server.WaitPost(() => server.System<SharedMapSystem>().DeleteMap(map.MapId));
        await pair.RunTicksSync(3);
        await pair.CleanReturnAsync();
    }
}
