using Robust.Shared.Prototypes;
using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Goobstation.Shared.Changeling.Components;
using Content.IntegrationTests.Fixtures;
using Content.Server.Antag;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Antag.Components;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Preferences;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class GoobChangelingRoundCompatibilityTest : GameTest
{
    private static readonly EntProtoId CompatibilityChangelingEntityId = "Changeling";

    public override PoolSettings PoolSettings => new()
    {
        InLobby = true,
        DummyTicker = false,
        Connected = true,
        Dirty = true
    };

    [Test]
    public async Task RetainedChangelingGetsEvolutionCurrencyAbilitiesAndCanOpenItsStore()
    {
        var ticker = Server.System<ServerGameTicker>();
        await Server.WaitPost(() =>
        {
            Server.CfgMan.SetCVar(CCVars.GameMap, "Saltern");
            ticker.SetGamePreset("Sandbox");
            Server.ResolveDependency<IGameMapManager>().SelectMap("Saltern");
        });
        await Pair.SetJobPriorities(("Passenger", JobPriority.High));
        await Pair.SetAntagPreference("Changeling", true);
        await Server.WaitPost(() =>
        {
            ticker.ToggleReadyAll(true);
            ticker.StartRound();
        });
        await Pair.RunTicksSync(5);
        EntityUid changeling = default;
        await Server.WaitAssertion(() =>
        {
            Assert.That(ticker.RunLevel, Is.EqualTo(GameRunLevel.InRound));
            var em = Server.EntMan;
            var session = Server.PlayerMan.Sessions.Single();
            Assert.That(session.AttachedEntity, Is.Not.Null);
            changeling = session.AttachedEntity!.Value;
            ticker.StartGameRule(CompatibilityChangelingEntityId, out var rule);
            Assert.That(rule, Is.Not.Null);
            var selection = em.GetComponent<AntagSelectionComponent>(rule!.Value.Owner);
            var definition = selection.Antags.Single().Proto;
            Assert.That(Server.System<ServerAntagSelectionSystem>()
                .TryMakeAntag((rule.Value.Owner, selection), definition, session), Is.True);
            Assert.That(em.HasComponent<ChangelingComponent>(changeling), Is.True);
            Assert.That(em.HasComponent<ChangelingIdentityComponent>(changeling), Is.True);
            Assert.That(em.HasComponent<ChangelingChemicalComponent>(changeling), Is.True);
            var store = em.GetComponent<StoreComponent>(changeling);
            Assert.That(store.CurrencyWhitelist.Any(c => c.Id == "EvolutionPoint"), Is.True);
            Assert.That(store.Balance["EvolutionPoint"], Is.EqualTo(FixedPoint2.New(12)));
            Assert.That(store.Categories, Is.Not.Empty);
            var actions = Server.System<SharedActionsSystem>();
            var granted = actions.GetActions(changeling).ToArray();
            var ids = granted.Select(a => em.GetComponent<MetaDataComponent>(a.Owner).EntityPrototype?.ID).ToArray();
            Assert.That(ids, Does.Contain("ActionAbsorbDNA"));
            Assert.That(ids, Does.Contain("ActionChangelingTransform"));
            var menu = granted.Single(a => em.GetComponent<MetaDataComponent>(a.Owner).EntityPrototype?.ID == "ActionEvolutionMenu");
            actions.PerformAction((changeling, em.GetComponent<ActionsComponent>(changeling)), menu);
            Assert.That(Server.System<SharedUserInterfaceSystem>().IsUiOpen(changeling, StoreUiKey.Key, changeling), Is.True);
        });
        await Pair.RunTicksSync(3);
        await Server.WaitAssertion(() =>
            Assert.That(Server.System<SharedUserInterfaceSystem>().IsUiOpen(changeling, StoreUiKey.Key, changeling), Is.True));
    }
}
