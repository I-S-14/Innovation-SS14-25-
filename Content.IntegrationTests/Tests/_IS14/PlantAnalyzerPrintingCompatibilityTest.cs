using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Content.Shared._CorvaxGoob.Botany.Components;
using Content.Shared._CorvaxGoob.Botany.PlantAnalyzer;
using Content.Shared.Botany.Components;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Paper;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class PlantAnalyzerPrintingCompatibilityTest
{
    [TestCase("ru-RU")]
    [TestCase("en-US")]
    public async Task ScannerPrintsRetainedPlantDataAndEnforcesCooldown(string culture)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var localization = server.ResolveDependency<ILocalizationManager>();
            var previous = localization.DefaultCulture;
            var created = new List<EntityUid>();
            try
            {
                localization.SetCulture(CultureInfo.GetCultureInfo(culture));
                var human = em.SpawnEntity("MobHuman", map.GridCoords);
                var analyzer = em.SpawnEntity("PlantAnalyzer", map.GridCoords);
                var seed = em.SpawnEntity("BluespaceTomatoSeeds", map.GridCoords);
                created.AddRange([human, analyzer, seed]);
                var component = em.GetComponent<CorvaxPlantAnalyzerComponent>(analyzer);
                component.ScannedEntity = seed;
                var message = new CorvaxPlantAnalyzerPrintMessage
                {
                    Actor = human,
                    UiKey = CorvaxPlantAnalyzerUiKey.Key,
                };
                var before = Reports(em).ToHashSet();
                em.EventBus.RaiseLocalEvent(analyzer, message);
                var reports = Reports(em).Where(uid => !before.Contains(uid)).ToArray();
                created.AddRange(reports);
                Assert.That(reports, Has.Length.EqualTo(1));
                var content = em.GetComponent<PaperComponent>(reports[0]).Content;
                var plantId = em.GetComponent<SeedComponent>(seed).PlantProtoId;
                var plant = server.ResolveDependency<IPrototypeManager>().Index(plantId);
                Assert.That(plant.TryGetComponent<PlantDataComponent>(out var species), Is.True);
                Assert.That(content, Does.Contain(localization.GetString(species!.Name)));
                Assert.That(content, Does.Contain(culture == "ru-RU" ? "Отчёт анализатора растений" : "Plant Analyzer Report"));
                Assert.That(content, Does.Not.Contain("plant-analyzer-printout"));
                Assert.That(component.PrintReadyAt, Is.GreaterThan(server.ResolveDependency<IGameTiming>().CurTime));
                var readyAt = component.PrintReadyAt;
                em.EventBus.RaiseLocalEvent(analyzer, message);
                Assert.That(Reports(em).Count(uid => !before.Contains(uid)), Is.EqualTo(1));
                Assert.That(component.PrintReadyAt, Is.EqualTo(readyAt));
            }
            finally
            {
                foreach (var uid in created)
                    if (em.EntityExists(uid))
                        em.DeleteEntity(uid);
                if (previous != null)
                    localization.SetCulture(previous);
            }
        });
        await pair.CleanReturnAsync();
    }

    private static List<EntityUid> Reports(IEntityManager em)
    {
        var reports = new List<EntityUid>();
        var query = em.AllEntityQueryEnumerator<PaperComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out var metadata))
            if (metadata.EntityPrototype?.ID == "PlantAnalyzerReportPaper")
                reports.Add(uid);
        return reports;
    }
}
