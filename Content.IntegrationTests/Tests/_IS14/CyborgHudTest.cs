// Licensed under IS14's EULA, see EULA.txt for more information.

#nullable enable
using Content.Client._IS14.Cyborgs;
using Content.Client._IS14.Cyborgs.UI;
using System.Linq;
using Content.Server.Mind;
using Content.Shared._IS14.Cyborgs;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared._IS14.Cyborgs.Prototypes;
using Content.Shared._IS14.Cyborgs.Systems;
using Robust.Client.UserInterface;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._IS14;

/// <summary>
///     The cyborg HUD replaces the hands bar for a player in a cyborg and drives the tool slots.
/// </summary>
[TestFixture]
[TestOf(typeof(CyborgHudUIController))]
public sealed class CyborgHudTest
{
    [Test]
    public async Task HudShowsSlotsAndDeploysTools()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var session = playerMan.GetSessionById(client.Session!.UserId);

        EntityUid cyborg = default;
        var toolCount = 0;
        await server.WaitPost(() =>
        {
            cyborg = sEntMan.SpawnEntity("CyborgNTEngineering", map.GridCoords);
            var mindSys = sEntMan.System<MindSystem>();
            var mind = mindSys.CreateMind(session.UserId, "CyborgHudTest");
            mindSys.TransferTo(mind, cyborg, mind: mind.Comp);
            playerMan.SetAttachedEntity(session, cyborg);
            toolCount = sEntMan.GetComponent<CyborgComponent>(cyborg).ToolContainer.ContainedEntities.Count;
        });

        await pair.RunTicksSync(30);

        var controller = client.ResolveDependency<IUserInterfaceManager>().GetUIController<CyborgHudUIController>();
        EntityUid? firstTool = null;

        await client.WaitAssertion(() =>
        {
            var hud = controller.Hud;
            Assert.That(hud, Is.Not.Null, "the cyborg HUD should replace the hands bar");
            Assert.Multiple(() =>
            {
                Assert.That(hud!.SlotCount, Is.EqualTo(3));
                Assert.That(hud.ToolCount, Is.EqualTo(toolCount), "every stored tool should be in the grid");
                Assert.That(hud.GetSlotEntity(0), Is.Null, "slots start empty");
            });

            var cEntMan = client.ResolveDependency<IEntityManager>();
            var local = client.Session!.AttachedEntity!.Value;
            var comp = cEntMan.GetComponent<CyborgComponent>(local);
            firstTool = comp.ToolContainer.ContainedEntities[0];
            cEntMan.System<CyborgSystem>().RequestToggleTool(firstTool.Value);
        });

        await pair.RunTicksSync(30);

        await client.WaitAssertion(() =>
        {
            var hud = controller.Hud!;
            Assert.That(hud.GetSlotEntity(0), Is.EqualTo(firstTool), "clicking a tool should put it in the first slot");
        });

        await server.WaitAssertion(() =>
        {
            var cyborgSys = sEntMan.System<SharedCyborgSystem>();
            var comp = sEntMan.GetComponent<CyborgComponent>(cyborg);
            Assert.That(comp.ToolContainer.ContainedEntities, Has.Count.EqualTo(toolCount - 1));
            Assert.That(comp.Active, "a cyborg with a player and a cell is active");
            Assert.That(cyborgSys.EnumerateTools((cyborg, comp)), Has.Exactly(toolCount).Items);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A cyborg without a model gets the model window, and one message sets both the model and the skin.
    /// </summary>
    [Test]
    public async Task ModelWindowPicksModelAndSkin()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            DummyTicker = false,
            Connected = true,
            Dirty = true,
        });
        var server = pair.Server;
        var client = pair.Client;
        var map = await pair.CreateTestMap();

        var sEntMan = server.ResolveDependency<IEntityManager>();
        var playerMan = server.ResolveDependency<IPlayerManager>();
        var session = playerMan.GetSessionById(client.Session!.UserId);
        var model = server.ResolveDependency<IPrototypeManager>().Index<CyborgModelPrototype>("CyborgModelMedical");
        var skin = model.Skins[^1];

        EntityUid cyborg = default;
        await server.WaitPost(() =>
        {
            cyborg = sEntMan.SpawnEntity("CyborgNTBattery", map.GridCoords);
            var mindSys = sEntMan.System<MindSystem>();
            var mind = mindSys.CreateMind(session.UserId, "CyborgModelWindowTest");
            mindSys.TransferTo(mind, cyborg, mind: mind.Comp);
            playerMan.SetAttachedEntity(session, cyborg);
        });

        await pair.RunTicksSync(30);

        var uiMan = client.ResolveDependency<IUserInterfaceManager>();
        await client.WaitAssertion(() =>
        {
            Assert.That(uiMan.WindowRoot.Children.OfType<CyborgModelWindow>().Count(), Is.EqualTo(1),
                "a cyborg without a model should get the model window");

            var cEntMan = client.ResolveDependency<IEntityManager>();
            var local = client.Session!.AttachedEntity!.Value;
            cEntMan.System<SharedUserInterfaceSystem>()
                .ClientSendUiMessage(local, CyborgUiKey.Model, new CyborgSelectModelMessage(model.ID, skin));
        });

        await pair.RunTicksSync(30);

        await server.WaitAssertion(() =>
        {
            var comp = sEntMan.GetComponent<CyborgComponent>(cyborg);
            var ui = sEntMan.System<SharedUserInterfaceSystem>();
            Assert.Multiple(() =>
            {
                Assert.That(comp.Model, Is.EqualTo(new ProtoId<CyborgModelPrototype>(model.ID)));
                Assert.That(comp.Skin, Is.EqualTo(skin), "the skin from the window should be applied");
                Assert.That(comp.SkinChosen);
                Assert.That(ui.IsUiOpen(cyborg, CyborgUiKey.Model), Is.False);
                Assert.That(ui.IsUiOpen(cyborg, CyborgUiKey.Skin), Is.False, "no second menu after the window");
            });
        });

        await client.WaitAssertion(() =>
        {
            Assert.That(uiMan.WindowRoot.Children.OfType<CyborgModelWindow>(), Is.Empty);
        });

        await pair.CleanReturnAsync();
    }
}
