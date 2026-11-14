#nullable enable annotations
using Content.Server._IS14.Economy.EconomyMonitor;
using Content.Server._IS14.Economy.Payroll;
using Content.Server._IS14.Economy.Treasury;
using Content.Server.Construction.Components;
using Content.Shared._IS14.Economy.EconomyMonitor;
using Content.Shared._IS14.Economy.Payroll;
using Content.Shared._IS14.Economy.Treasury;
using Robust.Shared.Containers;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class EconomyConsoleConstructionCompatibilityTest
{
    [TestCase("IS14EconomyMonitorConsole", null)]
    [TestCase("IS14TreasuryConsole", null)]
    [TestCase("IS14PayrollConsoleEngineering", "StationEngineering")]
    [TestCase("IS14PayrollConsoleMedical", "StationMedical")]
    [TestCase("IS14PayrollConsoleSecurity", "StationSecurity")]
    [TestCase("IS14PayrollConsoleCargo", "StationCargo")]
    [TestCase("IS14PayrollConsoleResearch", "StationResearch")]
    [TestCase("IS14PayrollConsoleService", "StationService")]
    [TestCase("IS14PayrollConsoleCommand", "StationTreasury")]
    public async Task MapInitializationKeepsTheLocalConsoleWithoutSpawningAFakeBoard(string prototype, string? account)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var console = em.SpawnEntity(prototype, map.GridCoords);
            Assert.That(em.GetComponent<ComputerComponent>(console).BoardPrototype, Is.Null);
            Assert.That(server.System<SharedContainerSystem>().GetContainer(console, "board").ContainedEntities, Is.Empty);
            var ui = server.System<SharedUserInterfaceSystem>();
            if (account != null)
            {
                Assert.That(em.GetComponent<PayrollConsoleComponent>(console).ManagedAccount.Id, Is.EqualTo(account));
                Assert.That(ui.HasUi(console, PayrollConsoleUiKey.Key), Is.True);
            }
            else if (prototype == "IS14TreasuryConsole")
            {
                Assert.That(em.HasComponent<TreasuryConsoleComponent>(console), Is.True);
                Assert.That(ui.HasUi(console, TreasuryConsoleUiKey.Key), Is.True);
            }
            else
            {
                Assert.That(em.GetComponent<EconomyMonitorConsoleComponent>(console).NetworkId, Is.EqualTo("station"));
                Assert.That(ui.HasUi(console, EconomyMonitorUiKey.Key), Is.True);
            }
            em.DeleteEntity(console);
        });
        await pair.CleanReturnAsync();
    }
}
