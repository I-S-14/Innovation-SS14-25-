using System.Linq;
using Content.Server._IS14.Economy;
using Content.Shared._IS14.Economy;
using Content.Shared._IS14.Economy.VendingMachine;
using Content.Shared.Access.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.VendingMachines;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Log;
using Robust.Shared.Maths;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class VendingDispenseAnimationTest
{
    [TestCase("IS14VendingMachineAtmosClothing", true)]
    [TestCase("IS14VendingMachineMed", true)]
    [TestCase("IS14VendingMachinekofee", false)]
    [TestCase("IS14VendingMachineCigs", false)]
    public async Task PurchaseRestartsDispensingAndReturnsToIdle(string prototype, bool animated)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true, NoLoadTestPrototypes = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        ISawmill resourceLog = null!;
        LogLevel? oldResourceLevel = null;
        await pair.Client.WaitPost(() =>
        {
            // The existing vending UI references two raw RSI PNGs. Keep its
            // unrelated resource warnings out of this animation regression;
            // resource errors and all other logging categories remain strict.
            resourceLog = pair.Client.ResolveDependency<ILogManager>().GetSawmill("res");
            oldResourceLevel = resourceLog.Level;
            resourceLog.Level = LogLevel.Error;
        });
        EntityUid machine = default, buyer = default;
        EntityUid? previous = null;
        ICommonSession session = null!;
        IS14VendingMachineComponent vending = null!;
        BankAccount account = null!;
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            session = server.PlayerMan.Sessions.Single();
            previous = session.AttachedEntity;
            buyer = em.SpawnEntity("MobHuman", map.GridCoords);
            server.PlayerMan.SetAttachedEntity(session, buyer);
            em.EnsureComponent<IdCardComponent>(buyer);
            account = server.System<BankManagerSystem>().CreateAccount(1000);
            em.EnsureComponent<BankAccountHolderComponent>(buyer).AccountNumber = account.AccountNumber;
            machine = em.SpawnEntity(prototype, map.GridCoords);
            Assert.That(server.System<SharedTransformSystem>().AnchorEntity((machine, null, null), map.Grid, Vector2i.Zero), Is.True);
            server.System<SharedPowerReceiverSystem>().SetNeedsPower(machine, false);
            vending = em.GetComponent<IS14VendingMachineComponent>(machine);
            Assert.That(vending.EjectState != null, Is.EqualTo(animated));
            vending.Tabs[0].Access.Clear();
            vending.Tabs[0].Inventory[0].Price = 1;
            vending.Tabs[0].Inventory[0].Stock = 3;
            Assert.That(server.System<SharedUserInterfaceSystem>().TryOpenUi(machine, IS14VendingMachineUiKey.Key, buyer), Is.True);
        });
        await pair.RunTicksSync(10);
        var local = pair.ToClientUid(machine);
        await Buy();
        await AssertVisual(animated ? IS14VendingMachineVisualState.Eject : IS14VendingMachineVisualState.Normal);
        await server.WaitAssertion(() => Assert.That(vending.Tabs[0].Inventory[0].Stock, Is.EqualTo(2)));
        if (animated)
        {
            await pair.Client.WaitPost(() =>
            {
                var sprites = pair.Client.System<SpriteSystem>();
                var sprite = pair.Client.EntMan.GetComponent<SpriteComponent>(local);
                Assert.That(sprites.LayerMapTryGet((local, sprite), VendingMachineVisualLayers.BaseUnshaded, out var layer, false), Is.True);
                sprites.LayerSetAnimationTime((local, sprite), layer, 0.5f);
            });
        }
        await Buy();
        await AssertVisual(animated ? IS14VendingMachineVisualState.Eject : IS14VendingMachineVisualState.Normal);
        if (animated)
        {
            await pair.Client.WaitAssertion(() =>
            {
                var em = pair.Client.EntMan;
                Assert.That(em.GetComponent<IS14VendingMachineComponent>(local).LastEjectSequence, Is.EqualTo(2));
                var sprite = em.GetComponent<SpriteComponent>(local);
                pair.Client.System<SpriteSystem>().LayerMapTryGet((local, sprite), VendingMachineVisualLayers.BaseUnshaded, out var layer, false);
                Assert.That(sprite.LayerData[layer].AnimationTime, Is.LessThan(0.4f), "A consecutive purchase must restart at frame zero");
            });
        }
        await pair.RunSeconds((float)vending.EjectDelay.TotalSeconds + 0.2f);
        await AssertVisual(IS14VendingMachineVisualState.Normal);
        await Buy();
        await server.WaitPost(() => server.System<SharedPowerReceiverSystem>().SetNeedsPower(machine, true));
        await pair.RunTicksSync(6);
        await AssertVisual(IS14VendingMachineVisualState.Off);
        await server.WaitPost(() => server.System<SharedPowerReceiverSystem>().SetNeedsPower(machine, false));
        await pair.RunTicksSync(6);
        await AssertVisual(IS14VendingMachineVisualState.Normal);
        await server.WaitPost(() => server.System<SharedUserInterfaceSystem>().TryOpenUi(machine, IS14VendingMachineUiKey.Key, buyer));
        await pair.RunTicksSync(6);
        await Buy();
        await AssertVisual(IS14VendingMachineVisualState.Deny);
        await server.WaitAssertion(() =>
        {
            Assert.That(vending.Tabs[0].Inventory[0].Stock, Is.Zero);
            Assert.That(account.Balance, Is.EqualTo(997));
            Assert.That(vending.EjectSequence, Is.EqualTo(animated ? 3u : 0u));
            Assert.That(vending.EjectEnd, Is.EqualTo(TimeSpan.Zero));
            server.PlayerMan.SetAttachedEntity(session, previous);
            server.EntMan.DeleteEntity(buyer);
            server.EntMan.DeleteEntity(machine);
            server.System<BankManagerSystem>().RemoveAccount(account.AccountNumber);
            server.System<SharedMapSystem>().DeleteMap(map.MapId);
        });
        await pair.RunTicksSync(3);
        await pair.Client.WaitPost(() => resourceLog.Level = oldResourceLevel);
        await pair.CleanReturnAsync();

        async Task Buy()
        {
            await pair.Client.WaitPost(() => pair.Client.System<SharedUserInterfaceSystem>()
                .ClientSendUiMessage(local, IS14VendingMachineUiKey.Key, new IS14VendingMachineBuyMessage(0, 0)));
            // Give the BUI request and subsequent appearance snapshot time to
            // cross both directions, while staying within the shortest eject.
            await pair.RunTicksSync(8);
        }

        async Task AssertVisual(IS14VendingMachineVisualState expected)
        {
            await server.WaitAssertion(() =>
            {
                Assert.That(server.System<SharedAppearanceSystem>().TryGetData<IS14VendingMachineVisualState>(machine, IS14VendingMachineVisuals.VisualState, out var visual), Is.True);
                Assert.That(visual, Is.EqualTo(expected));
            });
            await pair.Client.WaitAssertion(() =>
            {
                Assert.That(pair.Client.System<SharedAppearanceSystem>().TryGetData<IS14VendingMachineVisualState>(local,
                    IS14VendingMachineVisuals.VisualState, out var visual), Is.True);
                Assert.That(visual, Is.EqualTo(expected), "Replicated visual state");
                var sprite = pair.Client.EntMan.GetComponent<SpriteComponent>(local);
                var sprites = pair.Client.System<SpriteSystem>();
                Assert.That(sprites.LayerMapTryGet((local, sprite), VendingMachineVisualLayers.BaseUnshaded, out var layer, false), Is.True);
                Assert.That(sprite[layer].Visible, Is.EqualTo(expected != IS14VendingMachineVisualState.Off));
                if (expected != IS14VendingMachineVisualState.Off)
                    Assert.That(sprite[layer].RsiState.ToString(), Is.EqualTo(expected switch
                    {
                        IS14VendingMachineVisualState.Eject => "eject-unshaded",
                        IS14VendingMachineVisualState.Deny => "deny-unshaded",
                        _ => "normal-unshaded",
                    }));
            });
        }
    }
}
