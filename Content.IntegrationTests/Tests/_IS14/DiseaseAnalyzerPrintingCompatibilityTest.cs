#nullable enable annotations
using System.Globalization;
using System.Collections.Generic;
using Content.Goobstation.Shared.Disease.Components;
using Content.Goobstation.Shared.Disease;
using Content.Goobstation.Shared.Virology;
using Content.Server.Power.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Paper;
using Robust.Shared.Localization;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class DiseaseAnalyzerPrintingCompatibilityTest
{
    [TestCase("ru-RU")]
    [TestCase("en-US")]
    public async Task InsertedSampleProducesALocalizedReport(string culture)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entities = server.EntMan;
        var created = new List<EntityUid>();
        CultureInfo? previous = null;
        EntityUid analyzer = default;
        EntityUid disease = default;
        EntityUid swab = default;
        try
        {
            await server.WaitAssertion(() =>
            {
                var localization = server.ResolveDependency<ILocalizationManager>();
                previous = localization.DefaultCulture;
                localization.SetCulture(CultureInfo.GetCultureInfo(culture));
                analyzer = entities.SpawnEntity("DiseaseDiagnoser", map.GridCoords);
                disease = entities.SpawnEntity("DiseaseFlu", map.GridCoords);
                swab = entities.SpawnEntity("DiseaseSwab", map.GridCoords);
                created.AddRange([analyzer, disease, swab]);
                // Supply the isolated test machine without a station power network.
                entities.RemoveComponent<ApcPowerReceiverComponent>(analyzer);
                entities.GetComponent<DiseaseSwabComponent>(swab).DiseaseUid = disease;
                Assert.That(server.System<ItemSlotsSystem>().TryInsert(
                    analyzer, VirologyMachineComponent.SwabSlotId, swab, null), Is.True);
                Assert.That(entities.HasComponent<ActiveVirologyMachineComponent>(analyzer), Is.True);
            });

            await pair.RunTicksSync(360);

            await server.WaitAssertion(() =>
            {
                var reports = new List<EntityUid>();
                var query = entities.AllEntityQueryEnumerator<PaperComponent, MetaDataComponent>();
                while (query.MoveNext(out var uid, out _, out var metadata))
                    if (metadata.EntityPrototype?.ID == "DiagnosisReportPaper")
                        reports.Add(uid);
                created.AddRange(reports);
                Assert.That(reports, Has.Count.EqualTo(1));
                var content = entities.GetComponent<PaperComponent>(reports[0]).Content;
                var sample = entities.GetComponent<DiseaseComponent>(disease);
                var prototypes = server.ResolveDependency<IPrototypeManager>();
                Assert.That(content, Does.Contain(prototypes.Index<DiseaseTypePrototype>(sample.DiseaseType).LocalizedName));
                Assert.That(content, Does.Contain(sample.Genotype.ToString()));
                Assert.That(sample.Effects.ContainedEntities, Is.Not.Empty);
                foreach (var effect in sample.Effects.ContainedEntities)
                {
                    var prototype = entities.GetComponent<MetaDataComponent>(effect).EntityPrototype!;
                    Assert.That(content, Does.Contain(prototype.Name));
                    Assert.That(content, Does.Contain(prototype.Description));
                }
                Assert.That(entities.HasComponent<ActiveVirologyMachineComponent>(analyzer), Is.False);
                Assert.That(server.System<ItemSlotsSystem>().TryGetSlot(
                    analyzer, VirologyMachineComponent.SwabSlotId, out var slot), Is.True);
                Assert.That(slot!.Item, Is.Null);
            });
        }
        finally
        {
            await server.WaitPost(() =>
            {
                foreach (var uid in created)
                    if (entities.EntityExists(uid))
                        entities.DeleteEntity(uid);
                if (previous != null)
                    server.ResolveDependency<ILocalizationManager>().SetCulture(previous);
            });
        }
        await pair.CleanReturnAsync();
    }
}
