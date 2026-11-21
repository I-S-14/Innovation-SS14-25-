// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Linq;
using Content.Shared.Examine;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Localization;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class CyborgUpgradeExamineTest
{
    [TestCase("IS14CyborgUpgradeXenoborgDoorControl", "XenoborgModuleDoorControl")]
    [TestCase("IS14CyborgUpgradeXenoborgHeavyLaser", "XenoborgModuleHeavyLaser")]
    [TestCase("IS14CyborgUpgradeXenoborgEnergySword", "XenoborgModuleEnergySword")]
    [TestCase("IS14CyborgUpgradeXenoborgSuperCloak", "XenoborgModuleSuperCloakDevice")]
    public async Task UpgradeShowsTheSameLocalizedCompatibilityHintAsItsVanillaModule(string upgrade, string vanilla)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var examiner = em.SpawnEntity(null, map.GridCoords);
            var board = em.SpawnEntity(upgrade, map.GridCoords);
            var original = em.SpawnEntity(vanilla, map.GridCoords);
            var localization = server.ResolveDependency<ILocalizationManager>();
            var fit = em.GetComponent<BorgModuleComponent>(original).BorgFitTypes!.Single();
            var expected = localization.GetString("borg-module-fit", ("types", localization.GetString(fit)));
            var examined = new ExaminedEvent(new FormattedMessage(), board, examiner, false, false);
            em.EventBus.RaiseLocalEvent(board, examined);
            Assert.That(examined.GetTotalMessage().ToString(),
                Does.Contain(FormattedMessage.FromMarkupOrThrow(expected).ToString()));
            em.DeleteEntity(board);
            em.DeleteEntity(original);
            em.DeleteEntity(examiner);
        });
        await pair.CleanReturnAsync();
    }
}
