#nullable enable annotations
using Robust.Shared.Prototypes;
using System.Linq;
using Content.Goobstation.Maths.FixedPoint;
using Content.Server.PDA.Ringer;
using Content.Server.Traitor.Uplink;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Implants.Components;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Store;
using Content.Shared.Store.Components;
using Content.Shared.StoreDiscount.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class UplinkDeviceCompatibilityTest
{
    private static readonly ProtoId<ListingPrototype> CompatibilityUplinkUplinkImplanterListingId = "UplinkUplinkImplanter";

    [TestCase("PassengerPDA")]
    [TestCase("IS14_PdaAssistant")]
    [TestCase("implant")]
    [TestCase("remote-implant")]
    public async Task GrantedUplinkHasItsCatalogueCurrencyDiscountsAndCanOpen(string device)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var isImplant = device is "implant" or "remote-implant";
        EntityUid human = default, mind = default, storeUid = default;
        EntityUid? remoteStoreUid = null;
        Entity<ActionComponent> implantAction = default;
        ICommonSession session = null!;
        EntityUid? previous = null;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            human = em.SpawnEntity("MobHuman", map.GridCoords);
            mind = server.System<SharedMindSystem>().CreateMind(null).Owner;
            server.System<SharedMindSystem>().TransferTo(mind, human);
            session = server.PlayerMan.Sessions.Single();
            previous = session.AttachedEntity;
            server.PlayerMan.SetAttachedEntity(session, human);
            EntityUid? pda = null;
            StoreComponent? originalStore = null;
            if (!isImplant)
            {
                // The production ID slot requires an equipped jumpsuit.
                var uniform = em.SpawnEntity("ClothingUniformJumpsuitColorGrey", map.GridCoords);
                Assert.That(server.System<InventorySystem>().TryEquip(human, uniform, "jumpsuit", silent: true), Is.True);
                pda = em.SpawnEntity(device, map.GridCoords);
                Assert.That(server.System<InventorySystem>().TryEquip(human, pda.Value, "id", silent: true), Is.True);
                em.TryGetComponent(pda.Value, out originalStore);
            }
            var budget = FixedPoint2.New(1000);
            Assert.That(server.System<UplinkSystem>().AddUplink(human, budget, null, out _, out _, pda, true), Is.True);
            storeUid = pda ?? em.EntityQuery<MetaDataComponent>().Single(c => c.EntityPrototype?.ID == "UplinkImplant").Owner;
            var store = em.GetComponent<StoreComponent>(storeUid);
            if (originalStore != null)
                Assert.That(store, Is.SameAs(originalStore), "Configured IS14 PDA stores must not be replaced.");
            Assert.That(store.AccountOwner, Is.EqualTo(mind));
            Assert.That(store.Categories.Any(c => c.Id == "UplinkWeaponry"), Is.True);
            Assert.That(store.CurrencyWhitelist.Contains(UplinkSystem.TelecrystalCurrencyPrototype), Is.True);
            var expected = isImplant
                ? budget - server.ProtoMan.Index<ListingPrototype>(CompatibilityUplinkUplinkImplanterListingId).Cost[UplinkSystem.TelecrystalCurrencyPrototype]
                : budget;
            Assert.That(store.Balance[UplinkSystem.TelecrystalCurrencyPrototype], Is.EqualTo(expected));
            var stores = server.System<SharedStoreSystem>();
            Assert.That(stores.GetAvailableListings(mind, storeUid, store).Any(), Is.True);
            Assert.That(em.GetComponent<StoreDiscountComponent>(storeUid).Discounts, Has.Count.EqualTo(8));
            Assert.That(stores.GetStore(storeUid)!.Value.Owner, Is.EqualTo(storeUid));

            if (isImplant)
            {
                StoreComponent? remote = null;
                if (device == "remote-implant")
                {
                    var remoteUid = em.SpawnEntity("StorePresetRemoteUplink", map.GridCoords);
                    remoteStoreUid = remoteUid;
                    remote = em.GetComponent<StoreComponent>(remoteUid);
                    remote.AccountOwner = mind;
                    remote.Balance[UplinkSystem.TelecrystalCurrencyPrototype] = FixedPoint2.New(777);
                    remote.LastAvailableListings.Clear();
                    store.LastAvailableListings.Clear();
                    stores.SetRemoteStore(storeUid, remoteUid);
                }
                var actions = server.System<SharedActionsSystem>();
                var action = actions.GetActions(human).Single(a =>
                    em.GetComponent<MetaDataComponent>(a.Owner).EntityPrototype?.ID == "ActionOpenUplinkImplant");
                implantAction = action;
                Assert.That(actions.GetEvent(action), Is.TypeOf<OpenUplinkImplantEvent>());
                actions.PerformAction((human, em.GetComponent<ActionsComponent>(human)), action);
                if (remote != null)
                {
                    Assert.That(remote.LastAvailableListings, Is.Not.Empty, "An explicit remote wallet must remain the UI's source.");
                    Assert.That(store.LastAvailableListings, Is.Empty, "The local fallback must not override an explicit remote wallet.");
                }
            }
            else
            {
                var code = em.GetComponent<RingerAccessUplinkComponent>(storeUid).Code;
                Assert.That(code, Is.Not.Null);
                Assert.That(server.System<RingerSystem>().TryToggleUplink(storeUid, code!, human), Is.True);
                stores.ToggleUi(human, storeUid, store);
            }
            Assert.That(server.System<SharedUserInterfaceSystem>().IsUiOpen(storeUid, StoreUiKey.Key, human), Is.True);
        });
        await pair.RunTicksSync(5);
        await server.WaitAssertion(() =>
        {
            Assert.That(server.System<SharedUserInterfaceSystem>().IsUiOpen(storeUid, StoreUiKey.Key, human), Is.True);
            if (remoteStoreUid is { } remoteUid)
            {
                server.System<SharedUserInterfaceSystem>().CloseUi(storeUid, StoreUiKey.Key);
                server.EntMan.DeleteEntity(remoteUid);
                server.System<SharedStoreSystem>().SetRemoteStore(storeUid, remoteUid);
                server.System<SharedActionsSystem>().PerformAction(
                    (human, server.EntMan.GetComponent<ActionsComponent>(human)), implantAction);
                Assert.That(server.System<SharedUserInterfaceSystem>().IsUiOpen(storeUid, StoreUiKey.Key, human), Is.False,
                    "A deleted explicitly linked remote store must not expose the unrelated local fallback wallet.");
            }
            server.System<SharedUserInterfaceSystem>().CloseUi(storeUid, StoreUiKey.Key);
            server.PlayerMan.SetAttachedEntity(session, previous);
            server.System<SharedMapSystem>().DeleteMap(map.MapId);
            server.EntMan.DeleteEntity(mind);
        });
        await pair.RunTicksSync(3);
        await pair.CleanReturnAsync();
    }
}
