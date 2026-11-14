using System.Collections.Generic;
using System.Linq;
using Content.Shared.Inventory;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Preferences.Loadouts;
using Content.Shared.Preferences.Loadouts.Effects;
using Content.Shared.Roles;
using Content.Shared.Station.Systems;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class ColoredTowelLoadoutCompatibilityTest
{
    private readonly ProtoId<RoleLoadoutPrototype> CompatibilityIS14TestColoredTowelRoleLoadoutId = "IS14TestColoredTowel";

    [TestPrototypes]
    private const string Prototypes = @"
- type: roleLoadout
  id: IS14TestColoredTowel
  groups: [Trinkets]
";

    [TestCase("Gray")]
    [TestCase("Black")]
    [TestCase("Red")]
    [TestCase("Orange")]
    [TestCase("LightBrown")]
    [TestCase("Yellow")]
    [TestCase("DarkGreen")]
    [TestCase("Green")]
    [TestCase("LightBlue")]
    [TestCase("DarkBlue")]
    [TestCase("Purple")]
    [TestCase("Maroon")]
    [TestCase("Mime")]
    [TestCase("Silver")]
    [TestCase("Gold")]
    public async Task EarnedTowelKeepsItsTimeGateAndIsPlacedInTheBackpack(string color)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var prototypes = server.ResolveDependency<IPrototypeManager>();
            var id = "TowelColor" + color;
            var loadout = prototypes.Index<LoadoutPrototype>(id);
            var requirement = loadout.Effects.OfType<JobRequirementLoadoutEffect>().Single().Requirement;
            var tracker = requirement switch
            {
                RoleTimeRequirement role => role.Role.Id,
                DepartmentTimeRequirement department => prototypes.Index(prototypes.Index(department.Department).Roles.First()).PlayTimeTracker.Id,
                OverallPlaytimeRequirement => PlayTimeTrackingShared.TrackerOverall.Id,
                _ => throw new AssertionException("Unexpected retained towel time requirement.")
            };
            var requiredTime = color switch
            {
                "Silver" => TimeSpan.FromHours(500),
                "Gold" => TimeSpan.FromHours(1000),
                _ => TimeSpan.FromHours(100)
            };
            var playtimes = new Dictionary<string, TimeSpan> { [tracker] = requiredTime - TimeSpan.FromSeconds(1) };
            Assert.That(requirement.Check(em, prototypes, null, playtimes, out _), Is.False);
            playtimes[tracker] = requiredTime;
            Assert.That(requirement.Check(em, prototypes, null, playtimes, out _), Is.True);

            var human = em.SpawnEntity("MobHuman", map.GridCoords);
            var backpack = em.SpawnEntity("ClothingBackpack", map.GridCoords);
            Assert.That(server.System<InventorySystem>().TryEquip(human, backpack, "back", silent: true), Is.True);
            var selection = new RoleLoadout("IS14TestColoredTowel");
            selection.SelectedLoadouts["Trinkets"] = [new Loadout { Prototype = id }];
            server.System<StationSpawningSystem>().EquipRoleLoadout(human, selection,
                prototypes.Index<RoleLoadoutPrototype>(CompatibilityIS14TestColoredTowelRoleLoadoutId));
            var towel = em.EntityQuery<MetaDataComponent>().Single(c => c.EntityPrototype?.ID == id).Owner;
            Assert.That(server.System<SharedContainerSystem>().TryGetContainingContainer((towel, null, null), out var container), Is.True);
            Assert.That(container!.Owner, Is.EqualTo(backpack));
        });
        await server.WaitPost(() => server.System<SharedMapSystem>().DeleteMap(map.MapId));
        await pair.RunTicksSync(3);
        await pair.CleanReturnAsync();
    }
}
