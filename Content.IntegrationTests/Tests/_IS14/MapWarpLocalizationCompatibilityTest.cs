using System.Globalization;
using System.Text.RegularExpressions;
using Robust.Shared.ContentPack;
using Robust.Shared.Localization;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class MapWarpLocalizationCompatibilityTest
{
    private static readonly Regex WarpKeys = new(@"^(is14-map-warp-[a-f0-9]{12}) =", RegexOptions.Multiline);
    [TestCase("en-US")]
    [TestCase("ru-RU")]
    public async Task RetainedMapWarpLabelsResolveInBothCultures(string culture)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var resources = server.ResolveDependency<IResourceManager>();
            var source = resources.ContentFileReadAllText("/Locale/en-US/_IS14/Upstream/retained-map-warp-labels.ftl");
            var keys = WarpKeys.Matches(source);
            Assert.That(keys.Count, Is.GreaterThan(0));
            var localization = server.ResolveDependency<ILocalizationManager>();
            var previous = localization.DefaultCulture;
            try
            {
                localization.SetCulture(CultureInfo.GetCultureInfo(culture));
                foreach (Match match in keys)
                {
                    var key = match.Groups[1].Value;
                    Assert.That(localization.TryGetString(key, out var value), Is.True, key);
                    Assert.That(value, Is.Not.Null.And.Not.Empty.And.Not.EqualTo(key), key);
                }
                Assert.That(localization.GetString("is14-map-warp-11fb682be0a0"), Is.EqualTo(culture == "ru-RU" ? "ИИ" : "AI"));
                Assert.That(localization.GetString("is14-map-warp-95d64cacce0f"), Is.EqualTo(culture == "ru-RU" ? "Бар" : "Bar"));
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
