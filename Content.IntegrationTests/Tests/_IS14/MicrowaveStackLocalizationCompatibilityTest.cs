using System.Globalization;
using Content.Client.Guidebook.Controls;
using Content.IntegrationTests.Fixtures;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class MicrowaveStackLocalizationCompatibilityTest : GameTest
{
    [TestCase("ru-RU", "RecipeEmpoweredBurger", "листа плазмы")]
    [TestCase("ru-RU", "RecipeMedicatedSuture", "рулон ткани")]
    [TestCase("en-US", "RecipeEmpoweredBurger", "plasma")]
    [TestCase("en-US", "RecipeMedicatedSuture", "cloth")]
    public async Task RecipeEmbedResolvesStackNames(string culture, string recipe, string expected)
    {
        var client = Pair.Client;
        await client.WaitAssertion(() =>
        {
            var localization = client.ResolveDependency<ILocalizationManager>();
            var previous = localization.DefaultCulture;
            try
            {
                localization.SetCulture(CultureInfo.GetCultureInfo(culture));
                using var embed = new GuideMicrowaveEmbed(recipe);
                Assert.That(embed.CheckMatchesSearch(expected), Is.True,
                    $"Recipe {recipe} should display its localized stack ingredient in {culture}.");
            }
            finally
            {
                if (previous != null)
                    localization.SetCulture(previous);
            }
        });
    }
}
