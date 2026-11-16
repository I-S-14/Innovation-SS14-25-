// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Linq;
using Content.Shared._IS14.Cyborgs;
using Content.Shared._IS14.Pinpointer;
using Content.Shared.Lathe;
using Content.Shared.Mind;
using Content.Shared.Research.Prototypes;
using Content.Shared.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class MothershipTerminalTest
{
    [Test]
    public async Task FirstIntrinsicProductionOpenSendsRecipesAndNetworkTerminalsOpen()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, Dirty = true });
        var map = await pair.CreateTestMap();
        var server = pair.Server;
        EntityUid core = default, pointer = default;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            core = em.SpawnEntity("MothershipCore", map.GridCoords);
            em.SpawnEntity("IS14XenoborgScout", map.GridCoords);
            pointer = em.SpawnEntity("PinpointerMothership", map.GridCoords);
            server.System<SharedMindSystem>().ControlMob(server.PlayerMan.Sessions.Single().UserId, core);
        });
        await pair.RunTicksSync(10);
        await server.WaitAssertion(() =>
        {
            // Exactly the path used by the production action; no ActivateInWorld or manual lathe sync.
            Assert.That(server.System<IntrinsicUISystem>().InteractUI(core, LatheUiKey.Key), Is.True);
            var ui = server.System<SharedUserInterfaceSystem>();
            Assert.That(ui.TryGetUiState<LatheUpdateState>(core, LatheUiKey.Key, out var state), Is.True);
            Assert.That(state!.Recipes, Does.Contain(new ProtoId<LatheRecipePrototype>("IS14MothershipMMI")));
            Assert.That(state.Recipes, Does.Contain(new ProtoId<LatheRecipePrototype>("XenoborgEngiRecipe")));
        });
        await pair.RunTicksSync(10);
        await pair.Client.WaitAssertion(() =>
        {
            var ui = pair.Client.System<SharedUserInterfaceSystem>();
            Assert.That(ui.TryGetUiState<LatheUpdateState>(pair.ToClientUid(core), LatheUiKey.Key, out var state), Is.True);
            Assert.That(state!.Recipes, Is.Not.Empty, "the first production window must receive the recipe list");
        });
        await server.WaitAssertion(() =>
        {
            var ui = server.System<SharedUserInterfaceSystem>();
            Assert.That(server.System<IntrinsicUISystem>().InteractUI(core, MothershipEyeUiKey.Key), Is.True);
            Assert.That(ui.TryOpenUi(pointer, MothershipPinpointerUiKey.Key, core), Is.True);
        });
        await pair.RunTicksSync(10);
        await pair.Client.WaitAssertion(() =>
        {
            var ui = pair.Client.System<SharedUserInterfaceSystem>();
            Assert.That(ui.TryGetUiState<MothershipEyeState>(pair.ToClientUid(core), MothershipEyeUiKey.Key, out var eye), Is.True);
            Assert.That(eye!.Borgs, Has.Count.EqualTo(1));
            Assert.That(ui.TryGetUiState<MothershipPinpointerState>(pair.ToClientUid(pointer), MothershipPinpointerUiKey.Key, out var targets), Is.True);
            Assert.That(targets!.Targets, Has.Count.EqualTo(2));
        });
        await pair.CleanReturnAsync();
    }
}
