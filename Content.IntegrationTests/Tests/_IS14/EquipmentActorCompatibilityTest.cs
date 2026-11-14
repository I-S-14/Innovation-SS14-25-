using System.Linq;
using Content.Goobstation.Shared.Blob.Components;
using Content.IntegrationTests.Fixtures;
using Content.Server._White.Xenomorphs.FaceHugger;
using Content.Shared._Goobstation.Heretic.Components;
using Content.Shared._Goobstation.Wizard;
using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modsuit.Systems;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Content.Shared.Heretic;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Containers;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class EquipmentActorCompatibilityTest : GameTest
{
    [TestCase(true)]
    [TestCase(false)]
    public async Task AnUninfectedHelperCanRemoveALivingFacehugger(bool self)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var entities = Server.EntMan;
            var wearer = entities.SpawnEntity("MobHuman", map.GridCoords);
            var actor = self ? wearer : entities.SpawnEntity("MobHuman", map.GridCoords);
            var hugger = entities.SpawnEntity(null, map.GridCoords);
            var component = entities.AddComponent<FaceHuggerComponent>(hugger);
            component.Active = false;
            component.RestIn = TimeSpan.MaxValue;
            Assert.That(Server.System<InventorySystem>().TryGetSlotContainer(wearer, "mask", out _, out var slot), Is.True);
            var attempt = new BeingUnequippedAttemptEvent(actor, wearer, hugger, slot!);
            entities.EventBus.RaiseLocalEvent(hugger, attempt);
            Assert.That(attempt.Cancelled, Is.EqualTo(self));
        });
    }

    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task MadnessMaskChecksWhoRemovesIt(bool actorEligible, bool wearerEligible)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var entities = Server.EntMan;
            var wearer = entities.SpawnEntity("MobHuman", map.GridCoords);
            var actor = entities.SpawnEntity("MobHuman", map.GridCoords);
            if (wearerEligible)
                entities.AddComponent<GhoulComponent>(wearer);
            if (actorEligible)
                entities.AddComponent<GhoulComponent>(actor);
            var mask = entities.SpawnEntity("ClothingMaskMadHeretic", map.GridCoords);
            Assert.That(Server.System<InventorySystem>().TryGetSlotContainer(wearer, "mask", out _, out var slot), Is.True);
            var attempt = new BeingUnequippedAttemptEvent(actor, wearer, mask, slot!);
            entities.EventBus.RaiseLocalEvent(mask, attempt);
            Assert.That(attempt.Cancelled, Is.EqualTo(!actorEligible));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task BlobPodDistinguishesWearerFromAnotherPerson(bool self)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var entities = Server.EntMan;
            var wearer = entities.SpawnEntity("MobHuman", map.GridCoords);
            var actor = self ? wearer : entities.SpawnEntity("MobHuman", map.GridCoords);
            var pod = entities.SpawnEntity(null, map.GridCoords);
            entities.AddComponent<BlobPodComponent>(pod);
            var attempt = new BeingUnequippedAttemptEvent(actor, wearer, pod, new SlotDefinition());
            entities.EventBus.RaiseLocalEvent(pod, attempt);
            Assert.That(attempt.Cancelled, Is.EqualTo(self));
        });
    }

    [TestCase(true, false, true)]
    [TestCase(true, true, false)]
    [TestCase(false, true, true)]
    [TestCase(false, false, true)]
    public async Task HereticClothingChecksBothWearerAndActor(bool wearerEligible, bool actorEligible, bool cancelled)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var entities = Server.EntMan;
            var wearer = entities.SpawnEntity("MobHuman", map.GridCoords);
            var actor = entities.SpawnEntity("MobHuman", map.GridCoords);
            var clothing = entities.SpawnEntity(null, map.GridCoords);
            entities.AddComponent<HereticClothingComponent>(clothing);
            if (wearerEligible)
                entities.AddComponent<WizardComponent>(wearer);
            if (actorEligible)
                entities.AddComponent<WizardComponent>(actor);

            var attempt = new BeingEquippedAttemptEvent(actor, wearer, clothing, new SlotDefinition());
            entities.EventBus.RaiseLocalEvent(clothing, attempt);
            Assert.That(attempt.Cancelled, Is.EqualTo(cancelled));
        });
    }

    [Test]
    public async Task AnotherPersonCanRemoveAnUnsealedModsuitPartFromARestrainedWearer()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var entities = Server.EntMan;
            var wearer = entities.SpawnEntity("MobHuman", map.GridCoords);
            var actor = entities.SpawnEntity("MobHuman", map.GridCoords);
            var suit = entities.SpawnEntity("IS14ModsuitEngineeringFilled", map.GridCoords);
            var inventory = Server.System<InventorySystem>();
            var modsuit = Server.System<SharedModsuitSystem>();
            var control = entities.GetComponent<ModsuitControlComponent>(suit);
            Assert.That(inventory.TryEquip(wearer, suit, "back", force: true), Is.True);
            modsuit.DeployAll((suit, control), silent: true);
            Assert.That(entities.GetComponent<ModsuitPartComponent>(control.Parts["head"]).Deployed, Is.True);

            Assert.That(inventory.CanUnequip(wearer, wearer, "head", out _), Is.False);
            Assert.That(inventory.CanUnequip(actor, wearer, "head", out _), Is.False);
            var cuffs = entities.SpawnEntity("Handcuffs", map.GridCoords);
            Assert.That(Server.System<SharedContainerSystem>().Insert(cuffs,
                entities.GetComponent<CuffableComponent>(wearer).Container), Is.True);
            Assert.That(modsuit.IsSubdued(wearer), Is.True);
            Assert.That(inventory.CanUnequip(actor, wearer, "head", out _), Is.True);
            Assert.That(inventory.CanUnequip(wearer, wearer, "head", out _), Is.False);
        });
    }

    [Test]
    public async Task RemovingAnotherPersonsAttachedHoodStartsTheActorsDoAfter()
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var entities = Server.EntMan;
            var wearer = entities.SpawnEntity("MobHuman", map.GridCoords);
            var actor = entities.SpawnEntity("MobHuman", map.GridCoords);
            var coat = entities.SpawnEntity("ClothingOuterWinterHoS", map.GridCoords);
            var inventory = Server.System<InventorySystem>();
            var toggleable = entities.GetComponent<ToggleableClothingComponent>(coat);
            Assert.That(inventory.TryEquip(wearer, coat, "outerClothing", force: true), Is.True);
            var hood = toggleable.ClothingUids.Single(pair => pair.Value == "head").Key;
            Assert.That(Server.System<ToggleableClothingSystem>()
                .EquipClothing(wearer, (coat, toggleable), hood, "head", silent: true), Is.True);

            var ownAttempt = new BeingUnequippedAttemptEvent(wearer, wearer, hood, new SlotDefinition());
            entities.EventBus.RaiseLocalEvent(hood, ownAttempt);
            Assert.That(entities.GetComponent<DoAfterComponent>(wearer).DoAfters, Is.Empty);
            var otherAttempt = new BeingUnequippedAttemptEvent(actor, wearer, hood, new SlotDefinition());
            entities.EventBus.RaiseLocalEvent(hood, otherAttempt);
            Assert.That(otherAttempt.Cancelled, Is.True);
            Assert.That(entities.GetComponent<DoAfterComponent>(actor).DoAfters.Values
                .Any(after => after.Args.Event is AttachClothingDoAfterEvent), Is.True);
            Assert.That(entities.GetComponent<DoAfterComponent>(wearer).DoAfters, Is.Empty);
        });
    }
}
