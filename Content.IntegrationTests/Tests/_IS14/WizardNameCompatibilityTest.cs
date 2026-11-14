using System.Globalization;
using System.Linq;
using Content.Shared.Dataset;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class WizardNameCompatibilityTest
{
    private static readonly ProtoId<LocalizedDatasetPrototype> CompatibilityNamesWizardFirstLocalizedDatasetId = "NamesWizardFirst";
    private static readonly ProtoId<LocalizedDatasetPrototype> CompatibilityNamesWizardLastLocalizedDatasetId = "NamesWizardLast";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: TestIS14LocalizedWizard
  parent: MobHuman
  components:
  - type: RandomMetadata
    nameSegments: [NamesWizardFirst, NamesWizardLast]
    nameFormat: name-format-wizard
";

    [TestCase("ru-RU")]
    [TestCase("en-US")]
    public async Task SpawnGeneratesAWizardNameFromTheActiveLanguage(string culture)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var localization = server.ResolveDependency<ILocalizationManager>();
            var previous = localization.DefaultCulture;
            try
            {
                localization.SetCulture(CultureInfo.GetCultureInfo(culture));
                var prototypes = server.ResolveDependency<IPrototypeManager>();
                var first = prototypes.Index<LocalizedDatasetPrototype>(CompatibilityNamesWizardFirstLocalizedDatasetId).Values;
                var last = prototypes.Index<LocalizedDatasetPrototype>(CompatibilityNamesWizardLastLocalizedDatasetId).Values;
                Assert.That(first, Has.Count.EqualTo(44));
                Assert.That(last, Has.Count.EqualTo(69));
                foreach (var key in first.Concat(last))
                    Assert.That(localization.TryGetString(key, out _), Is.True, $"{culture}: {key}");
                var validNames = first.SelectMany(a => last.Select(b => localization.GetString("name-format-wizard",
                    ("part0", localization.GetString(a)), ("part1", localization.GetString(b))))).ToHashSet();
                var wizard = server.EntMan.SpawnEntity("TestIS14LocalizedWizard", map.GridCoords);
                Assert.That(validNames, Does.Contain(server.EntMan.GetComponent<MetaDataComponent>(wizard).EntityName));
                server.EntMan.DeleteEntity(wizard);
            }
            finally
            {
                if (previous != null)
                    localization.SetCulture(previous);
            }
        });
        await pair.CleanReturnAsync();
    }
}
