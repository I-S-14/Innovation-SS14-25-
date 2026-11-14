using Content.Shared.EntityConditions;
using Content.Shared.EntityConditions.Conditions.Body;
using System.Globalization;
using System.Linq;
using Content.Shared.Body.Prototypes;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Metabolizer;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class MetabolizerTypeCompatibilityTest
{
    [TestCase("OrganHumanLungs", "Human", true)]
    [TestCase("OrganHumanLungs", "Vox", false)]
    [TestCase("OrganVoxLungs", "Human", false)]
    [TestCase("OrganVoxLungs", "Vox", true)]
    public async Task ReagentConditionsUseTheActualOrganType(string prototype, string type, bool expected)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var organ = server.EntMan.SpawnEntity(prototype, map.GridCoords);
            var conditions = server.System<SharedEntityConditionsSystem>();
            var condition = new MetabolizerTypeCondition { Type = [type] };
            Assert.That(conditions.TryCondition(organ, condition), Is.EqualTo(expected));
            condition.Inverted = true;
            Assert.That(conditions.TryCondition(organ, condition), Is.EqualTo(!expected));
            server.EntMan.DeleteEntity(organ);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase("ru-RU")]
    [TestCase("en-US")]
    public async Task BioKnifeTargetMenuUsesLocalizedMetabolizers(string culture)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var entities = server.EntMan;
            var localization = server.ResolveDependency<ILocalizationManager>();
            var previous = localization.DefaultCulture;
            var knife = entities.SpawnEntity("XenoborgBioKnife", map.GridCoords);
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            try
            {
                localization.SetCulture(CultureInfo.GetCultureInfo(culture));
                var component = entities.GetComponent<BonusDamageOnMetabolismComponent>(knife);
                var category = localization.GetString("verb-categories-select-target");
                var choices = server.System<SharedVerbSystem>()
                    .GetLocalVerbs(knife, user, typeof(Verb), force: true)
                    .Where(verb => verb.Category?.Text == category).ToArray();
                var prototypes = server.ResolveDependency<IPrototypeManager>()
                    .EnumeratePrototypes<MetabolizerTypePrototype>()
                    .Where(prototype => !component.ExcludedMetabolizers.Contains(prototype.ID)).ToArray();
                Assert.That(choices.Select(verb => verb.Text),
                    Is.EquivalentTo(prototypes.Select(prototype => prototype.LocalizedName)));
                var selected = choices.First(verb => !verb.Disabled);
                Assert.That(selected.Act, Is.Not.Null);
                selected.Act!.Invoke();
                Assert.That(component.SelectedMetabolizer?.Id,
                    Is.EqualTo(prototypes.Single(prototype => prototype.LocalizedName == selected.Text).ID));
            }
            finally
            {
                entities.DeleteEntity(knife);
                entities.DeleteEntity(user);
                if (previous != null)
                    localization.SetCulture(previous);
            }
        });
        await pair.CleanReturnAsync();
    }
}
