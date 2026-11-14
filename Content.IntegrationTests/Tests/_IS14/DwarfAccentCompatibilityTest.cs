using System.Globalization;
using Content.Shared.Decals;
using Content.Shared.Speech.Components;
using Content.Shared.Speech.EntitySystems;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class DwarfAccentCompatibilityTest
{
    private static readonly ProtoId<DecalPrototype> CompatibilitydwarfDecalId = "dwarf";

    [TestCase("ru-RU", "девочка!", "дэвочшка!")]
    [TestCase("en-US", "girl!", "lassie!")]
    public async Task RetainedDwarfAccentUsesTheRulesOfTheActiveLanguage(string culture, string input, string expected)
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
                Assert.That(localization.GetString("accent-dwarf-words-1"),
                    Is.EqualTo(culture == "ru-RU" ? "девочка" : "girl"));
                var dwarf = server.EntMan.SpawnEntity("MobDwarf", map.GridCoords);
                var accent = server.EntMan.GetComponent<ReplacementAccentComponent>(dwarf);
                Assert.That(accent.Accent.Id, Is.EqualTo("dwarf"));
                var system = server.System<ReplacementAccentSystem>();
                var otherIsRussian = culture == "en-US";
                localization.SetCulture(CultureInfo.GetCultureInfo(otherIsRussian ? "ru-RU" : "en-US"));
                Assert.That(system.Accentuate(otherIsRussian ? "девочка!" : "girl!", (dwarf, accent)),
                    Is.EqualTo(otherIsRussian ? "дэвочшка!" : "lassie!"));
                localization.SetCulture(CultureInfo.GetCultureInfo(culture));
                Assert.That(system.Accentuate(input, (dwarf, accent)), Is.EqualTo(expected));
                Assert.That(server.ResolveDependency<IPrototypeManager>().HasIndex<DecalPrototype>(CompatibilitydwarfDecalId), Is.True);
                server.EntMan.DeleteEntity(dwarf);
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
