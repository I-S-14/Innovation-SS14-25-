using System.Collections.Generic;
using System.Linq;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class RestoredCargoCrateTest
{
    [TestCase("CrateEmergencyInternals", "ClothingMaskGas:3,ClothingMaskBreath:3,OxygenTankFilled:3,NitrogenTankFilled:3,ClothingOuterSuitEmergency:3", false)]
    [TestCase("CrateNitrogenInternals", "ClothingMaskGas:2,ClothingMaskBreath:2,NitrogenTankFilled:4", false)]
    [TestCase("CrateEngineeringShuttle", "WallmountSubstationElectronics:1,WallmountGeneratorAPUElectronics:1,HandheldGPSBasic:1,InflatableDoorStack1:1", false)]
    [TestCase("CrateMaterialTextiles", "MaterialCloth:2,MaterialDurathread:1", false)]
    [TestCase("CrateFoodBarSupply", "DrinkGlass:5,DrinkShotGlass:5,DrinkShaker:2,DrinkOrangeJuice:2,DrinkLimeJuice:2,DrinkTomatoJuice:2,DrinkWaterBottleFull:2,DrinkSodaWaterCan:2,DrinkCreamCarton:2", false)]
    [TestCase("CrateServiceJanitorialSupplies", "MopItem:1,MopBucketCubeWrapped:1,Bucket:3,WetFloorSign:2,SprayBottleSpaceCleaner:2,TrashBag:2,Plunger:2,BoxCleanerGrenades:1,WireBrush:2", true)]
    public async Task CreatingTheCratePopulatesItsRealStorage(string prototype, string expectedCsv, bool randomSoap)
    {
        var expected = expectedCsv.Split(',').Select(p => p.Split(':'))
            .ToDictionary(p => p[0], p => int.Parse(p[1]));
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var crate = em.SpawnEntity(prototype, map.GridCoords);
            var items = server.System<SharedContainerSystem>().GetContainer(crate, "entity_storage")
                .ContainedEntities.ToArray();
            var actual = items.GroupBy(uid => em.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID)
                .ToDictionary(g => g.Key, g => g.Count());
            foreach (var (id, count) in expected)
                Assert.That(actual.GetValueOrDefault(id), Is.EqualTo(count), prototype + ": " + id);
            Assert.That(items, Has.Length.EqualTo(expected.Values.Sum() + (randomSoap ? 1 : 0)));
            if (randomSoap)
            {
                string[] soaps = ["SoapNT", "Soap", "SoapHomemade", "SoapDeluxe", "SoapSyndie", "SlipocalypseClusterSoap", "SoapOmega"];
                Assert.That(actual.Keys.Except(expected.Keys), Is.SubsetOf(soaps));
            }
            if (prototype == "CrateMaterialTextiles")
            {
                Assert.That(items.Where(uid => em.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID == "MaterialCloth")
                    .Sum(uid => em.GetComponent<StackComponent>(uid).Count), Is.EqualTo(60));
                Assert.That(items.Where(uid => em.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID == "MaterialDurathread")
                    .Sum(uid => em.GetComponent<StackComponent>(uid).Count), Is.EqualTo(30));
            }
            foreach (var item in items)
                em.DeleteEntity(item);
            em.DeleteEntity(crate);
        });
        await pair.CleanReturnAsync();
    }
}
