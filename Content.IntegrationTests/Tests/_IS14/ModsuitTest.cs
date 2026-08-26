// Licensed under IS14's EULA, see EULA.txt for more information.

#nullable enable
using System.Linq;
using Content.Shared._IS14.Modsuit;
using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modsuit.Systems;
using Content.Shared._IS14.Modular;
using Content.Shared._IS14.Modular.Components;
using Content.Shared._IS14.Modular.Behaviours;
using Content.Shared._IS14.Modular.Systems;
using Content.Shared._Shitmed.Targeting;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Alert;
using Content.Shared.Clothing;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Damage;
using Content.Shared.Emp;
using Content.Shared.Damage.Prototypes;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.IntegrationTests.Tests._IS14;

/// <summary>
///     End-to-end checks on the MOD suit: the three states, the complexity budget,
///     and the slot requirements that gate modules.
/// </summary>
[TestFixture]
[TestOf(typeof(ModsuitControlComponent))]
public sealed class ModsuitTest
{
    private const string SuitProto = "IS14ModsuitEngineering";
    private const string DebugSuitProto = "IS14ModsuitDebug";
    private static readonly ProtoId<AlertPrototype> MagbootsAlert = "Magboots";

    /// <summary>
    ///     A fresh suit should come up with all four parts stowed, a core installed,
    ///     and its integrated module in place.
    /// </summary>
    [Test]
    public async Task SuitSpawnsAssembled()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var suit = entMan.SpawnEntity(SuitProto, MapCoordinates.Nullspace);

            var control = entMan.GetComponent<ModsuitControlComponent>(suit);
            var chassis = entMan.GetComponent<ModularChassisComponent>(suit);

            Assert.Multiple(() =>
            {
                Assert.That(control.Parts, Has.Count.EqualTo(4), "suit should spawn with four parts");
                Assert.That(control.Parts.Keys,
                    Is.EquivalentTo(new[] { "head", "outerClothing", "gloves", "shoes" }));

                // Integrated modules are free and unremovable, so they must not eat the budget.
                Assert.That(chassis.ModuleContainer.ContainedEntities, Has.Count.EqualTo(1));
                Assert.That(chassis.UsedComplexity, Is.Zero);

                foreach (var part in control.Parts.Values)
                {
                    var comp = entMan.GetComponent<ModsuitPartComponent>(part);
                    Assert.That(comp.Deployed, Is.False, "parts start stowed");
                    Assert.That(comp.Sealed, Is.False, "parts start unsealed");
                }
            });

            entMan.DeleteEntity(suit);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The state machine: stowed to deployed to sealed and back, with the suit
    ///     only switching on once something is actually sealed.
    /// </summary>
    [Test]
    public async Task DeployAndSealCycle()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var modsuit = entMan.System<SharedModsuitSystem>();
            var invSystem = entMan.System<InventorySystem>();

            var human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var suit = entMan.SpawnEntity(SuitProto, MapCoordinates.Nullspace);
            var control = entMan.GetComponent<ModsuitControlComponent>(suit);
            var chassis = entMan.GetComponent<ModularChassisComponent>(suit);
            var ent = new Entity<ModsuitControlComponent>(suit, control);

            Assert.That(invSystem.TryEquip(human, suit, "back", force: true), Is.True, "suit should go on the back");
            Assert.That(control.Wearer, Is.EqualTo(human));

            // Deploy.
            modsuit.DeployAll(ent, silent: true);
            Assert.Multiple(() =>
            {
                Assert.That(modsuit.AllPartsDeployed(ent), Is.True);
                Assert.That(chassis.Active, Is.False, "deploying alone must not power the suit");
                Assert.That(modsuit.IsAnyPartSealed(ent), Is.False);
            });

            // Seal one part directly, bypassing the DoAfter queue.
            var helmet = control.Parts["head"];
            var helmetComp = entMan.GetComponent<ModsuitPartComponent>(helmet);

            modsuit.SetPartSealed(ent, (helmet, helmetComp), true);

            Assert.Multiple(() =>
            {
                Assert.That(helmetComp.Sealed, Is.True);
                Assert.That(modsuit.IsSealed(ent), Is.False, "one sealed part is not a sealed suit");
                Assert.That(modsuit.IsAnyPartSealed(ent), Is.True);
            });

            // Retracting a sealed part has to unseal it first.
            modsuit.TryRetractPart(ent, helmet, silent: true);
            Assert.Multiple(() =>
            {
                Assert.That(helmetComp.Sealed, Is.False, "retracting must unseal");
                Assert.That(helmetComp.Deployed, Is.False);
            });

            modsuit.RetractAll(ent, silent: true);
            Assert.That(modsuit.AnyPartDeployed(ent), Is.False);

            entMan.DeleteEntity(suit);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Deploying over clothing the wearer already has on must stash it and hand it
    ///     back afterwards. Without this a pair of gloves stops the suit closing, which
    ///     is exactly the friction the suit is supposed to remove.
    /// </summary>
    [Test]
    public async Task DeployOverExistingClothing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var modsuit = entMan.System<SharedModsuitSystem>();
            var invSystem = entMan.System<InventorySystem>();

            var human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var gloves = entMan.SpawnEntity("ClothingHandsGlovesColorYellow", MapCoordinates.Nullspace);

            Assert.That(invSystem.TryEquip(human, gloves, "gloves", force: true), Is.True);

            var suit = entMan.SpawnEntity(SuitProto, MapCoordinates.Nullspace);
            var control = entMan.GetComponent<ModsuitControlComponent>(suit);
            var ent = new Entity<ModsuitControlComponent>(suit, control);

            invSystem.TryEquip(human, suit, "back", force: true);
            modsuit.DeployAll(ent, silent: true);

            var gauntlets = control.Parts["gloves"];

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<ModsuitPartComponent>(gauntlets).Deployed, Is.True,
                    "gauntlets should deploy over the existing gloves");
                Assert.That(invSystem.TryGetSlotEntity(human, "gloves", out var worn) && worn == gauntlets,
                    Is.True, "the suit part should now occupy the slot");
                Assert.That(entMan.GetComponent<TransformComponent>(gloves).ParentUid, Is.EqualTo(gauntlets),
                    "the displaced gloves should be stashed inside the part");
            });

            modsuit.RetractAll(ent, silent: true);

            Assert.That(invSystem.TryGetSlotEntity(human, "gloves", out var restored) && restored == gloves,
                Is.True, "retracting must hand the original gloves back");

            entMan.DeleteEntity(suit);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Modules are gated on the parts they declare. A visor needs a sealed helmet,
    ///     so unsealing the helmet has to switch it back off.
    /// </summary>
    [Test]
    public async Task ModulesFollowSealedParts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var modsuit = entMan.System<SharedModsuitSystem>();
            var chassisSys = entMan.System<SharedModularChassisSystem>();
            var invSystem = entMan.System<InventorySystem>();

            var human = entMan.SpawnEntity("MobHuman", MapCoordinates.Nullspace);
            var suit = entMan.SpawnEntity(DebugSuitProto, MapCoordinates.Nullspace);
            var control = entMan.GetComponent<ModsuitControlComponent>(suit);
            var chassis = entMan.GetComponent<ModularChassisComponent>(suit);
            var ent = new Entity<ModsuitControlComponent>(suit, control);

            invSystem.TryEquip(human, suit, "back", force: true);
            modsuit.DeployAll(ent, silent: true);

            // Find the helmet-gated visor module.
            var visor = chassis.ModuleContainer.ContainedEntities
                .Select(m => (uid: m, comp: entMan.GetComponent<ChassisModuleComponent>(m)))
                .First(m => m.comp.RequiredSlots.Contains(SlotFlags.HEAD));

            Assert.That(visor.comp.Enabled, Is.False, "an unsealed suit must not run modules");

            // Seal every part and switch the chassis on, as the seal sequence would.
            foreach (var part in control.Parts.Values)
            {
                var comp = entMan.GetComponent<ModsuitPartComponent>(part);
                modsuit.SetPartSealed(ent, (part, comp), true);
            }

            chassisSys.SetActive((suit, chassis), true);

            Assert.That(visor.comp.Enabled, Is.True, "a sealed, powered suit should run its modules");

            // Pop the helmet: the visor loses its required slot and must stand down.
            var helmet = control.Parts["head"];
            var helmetComp = entMan.GetComponent<ModsuitPartComponent>(helmet);
            modsuit.SetPartSealed(ent, (helmet, helmetComp), false);

            Assert.That(visor.comp.Enabled, Is.False, "unsealing the helmet must disable helmet modules");

            entMan.DeleteEntity(suit);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Selecting an Active module has to actually put its device in the wearer's
    ///     hands. The device is built during MapInit, so this also guards against the
    ///     module being spawned somewhere MapInit never runs.
    /// </summary>
    [Test]
    public async Task ActiveModuleExtendsDevice()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        // Hands are built during map init, so this one needs a real map rather than
        // nullspace — otherwise there is nowhere for the device to go.
        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var modsuit = entMan.System<SharedModsuitSystem>();
            var chassisSys = entMan.System<SharedModularChassisSystem>();
            var moduleSys = entMan.System<SharedChassisModuleSystem>();
            var invSystem = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();

            var human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var suit = entMan.SpawnEntity(DebugSuitProto, map.GridCoords);
            var control = entMan.GetComponent<ModsuitControlComponent>(suit);
            var chassis = entMan.GetComponent<ModularChassisComponent>(suit);
            var ent = new Entity<ModsuitControlComponent>(suit, control);

            invSystem.TryEquip(human, suit, "back", force: true);
            modsuit.DeployAll(ent, silent: true);

            foreach (var part in control.Parts.Values)
            {
                var comp = entMan.GetComponent<ModsuitPartComponent>(part);
                modsuit.SetPartSealed(ent, (part, comp), true);
            }

            chassisSys.SetActive((suit, chassis), true);

            var analyzer = chassis.ModuleContainer!.ContainedEntities
                .Select(m => (uid: m, comp: entMan.GetComponent<ChassisModuleComponent>(m)))
                .First(m => m.comp.Kind == ModuleKind.Active);

            var item = entMan.GetComponent<ModuleItemComponent>(analyzer.uid);

            Assert.That(item.Device, Is.Not.Null,
                "the module should have built its device during map init");
            Assert.That(hands.EnumerateHands(human).Any(), Is.True,
                "test precondition: the mob needs hands to hold anything");

            Assert.That(moduleSys.TrySelect((analyzer.uid, analyzer.comp), human), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(analyzer.comp.Active, Is.True);
                Assert.That(item.HeldBy, Is.EqualTo(human),
                    "the module should have claimed the device for the wearer");
                Assert.That(hands.IsHolding(human, item.Device), Is.True,
                    "selecting an active module must place its device in hand");
            });

            // Standing it down puts the device back rather than dropping it.
            moduleSys.Deactivate((analyzer.uid, analyzer.comp), suit, human);

            Assert.That(hands.IsHolding(human, item.Device), Is.False,
                "deselecting must stow the device again");

            entMan.DeleteEntity(suit);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The complexity budget and the conflict tags are what make module choice a
    ///     decision rather than a checklist, so both need to actually bite.
    /// </summary>
    [Test]
    public async Task ComplexityBudgetAndConflicts()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var chassisSys = entMan.System<SharedModularChassisSystem>();

            var suit = entMan.SpawnEntity(SuitProto, MapCoordinates.Nullspace);
            var chassis = entMan.GetComponent<ModularChassisComponent>(suit);
            var chassisEnt = new Entity<ModularChassisComponent>(suit, chassis);

            // Installing is a hardware job: the panel has to be open.
            var storage = entMan.SpawnEntity("IS14ModuleStorage", MapCoordinates.Nullspace);
            var storageComp = entMan.GetComponent<ChassisModuleComponent>(storage);

            Assert.That(chassisSys.CanInstall(chassisEnt, (storage, storageComp), out _), Is.False,
                "a closed panel should refuse installation");

            chassisSys.SetPanelOpen(chassisEnt, true);

            // An engaged ID lock has to bite even with the panel open, otherwise the
            // lock is decoration.
            var lockSys = entMan.System<SharedModsuitLockSystem>();
            var lockComp = entMan.GetComponent<ModsuitLockComponent>(suit);

            lockSys.SetLocked((suit, lockComp), true);
            Assert.That(chassisSys.TryInstall(chassisEnt, (storage, storageComp)), Is.False,
                "a locked suit must refuse hardware");

            lockSys.SetLocked((suit, lockComp), false);
            Assert.That(chassisSys.TryInstall(chassisEnt, (storage, storageComp)), Is.True);
            Assert.That(chassis.UsedComplexity, Is.EqualTo(3));

            // Two storage modules share a conflict tag, so the second must be refused.
            var storage2 = entMan.SpawnEntity("IS14ModuleStorageLarge", MapCoordinates.Nullspace);
            var storage2Comp = entMan.GetComponent<ChassisModuleComponent>(storage2);

            Assert.That(chassisSys.CanInstall(chassisEnt, (storage2, storage2Comp), out _), Is.False,
                "conflicting modules must not stack");

            // Removing gives the budget back.
            Assert.That(chassisSys.TryUninstall(chassisEnt, (storage, storageComp)), Is.True);
            Assert.That(chassis.UsedComplexity, Is.Zero);

            entMan.DeleteEntity(suit);
            entMan.DeleteEntity(storage);
            entMan.DeleteEntity(storage2);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Suit hardware is not loot: dropping an extended device has to fail and fold the
    ///     module away instead of leaving a free tool on the floor.
    /// </summary>
    [Test]
    public async Task ExtendedDeviceCannotBeDropped()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        var map = await pair.CreateTestMap();

        EntityUid human = default;
        EntityUid suit = default;
        EntityUid moduleUid = default;
        EntityUid device = default;

        await server.WaitAssertion(() =>
        {
            var modsuit = entMan.System<SharedModsuitSystem>();
            var chassisSys = entMan.System<SharedModularChassisSystem>();
            var moduleSys = entMan.System<SharedChassisModuleSystem>();
            var invSystem = entMan.System<InventorySystem>();
            var hands = entMan.System<SharedHandsSystem>();

            human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            suit = entMan.SpawnEntity(DebugSuitProto, map.GridCoords);

            var control = entMan.GetComponent<ModsuitControlComponent>(suit);
            var chassis = entMan.GetComponent<ModularChassisComponent>(suit);
            var ent = new Entity<ModsuitControlComponent>(suit, control);

            invSystem.TryEquip(human, suit, "back", force: true);
            modsuit.DeployAll(ent, silent: true);

            foreach (var part in control.Parts.Values)
            {
                var comp = entMan.GetComponent<ModsuitPartComponent>(part);
                modsuit.SetPartSealed(ent, (part, comp), true);
            }

            chassisSys.SetActive((suit, chassis), true);

            var analyzer = chassis.ModuleContainer!.ContainedEntities
                .Select(m => (uid: m, comp: entMan.GetComponent<ChassisModuleComponent>(m)))
                .First(m => m.comp.Kind == ModuleKind.Active);

            moduleUid = analyzer.uid;

            Assert.That(moduleSys.TrySelect((analyzer.uid, analyzer.comp), human), Is.True);

            var item = entMan.GetComponent<ModuleItemComponent>(moduleUid);
            device = item.Device!.Value;

            Assert.That(entMan.HasComponent<ChassisDeviceComponent>(device), Is.True,
                "an extended device must be stamped as suit property");

            // The drop is refused outright — the device never touches the floor.
            Assert.That(hands.TryDrop(human, device, checkActionBlocker: false), Is.False,
                "suit hardware must not be droppable");
            Assert.That(hands.IsHolding(human, device), Is.True,
                "a refused drop must leave the device in hand");
        });

        // Reeling in is deferred by a tick, because it happens from inside the very
        // removal check that just said no.
        await server.WaitRunTicks(2);

        await server.WaitAssertion(() =>
        {
            var hands = entMan.System<SharedHandsSystem>();
            var module = entMan.GetComponent<ChassisModuleComponent>(moduleUid);
            var item = entMan.GetComponent<ModuleItemComponent>(moduleUid);

            Assert.Multiple(() =>
            {
                Assert.That(module.Active, Is.False, "a refused drop must switch the module off");
                Assert.That(hands.IsHolding(human, device), Is.False, "the device must leave the hand");
                Assert.That(item.Device, Is.EqualTo(device), "the module must keep its hardware");
                Assert.That(item.Container!.ContainedEntity, Is.EqualTo(device),
                    "the device belongs back in the module");
            });

            entMan.DeleteEntity(suit);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Switching the magnetic module on has to give the wearer the same magboot state a
    ///     real pair would, alert included — without it the player gets no sign it is running.
    /// </summary>
    [Test]
    public async Task MagbootsModuleClampsWearer()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var modsuit = entMan.System<SharedModsuitSystem>();
            var chassisSys = entMan.System<SharedModularChassisSystem>();
            var moduleSys = entMan.System<SharedChassisModuleSystem>();
            var invSystem = entMan.System<InventorySystem>();
            var alerts = entMan.System<AlertsSystem>();

            var human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var suit = entMan.SpawnEntity(DebugSuitProto, map.GridCoords);

            var control = entMan.GetComponent<ModsuitControlComponent>(suit);
            var chassis = entMan.GetComponent<ModularChassisComponent>(suit);
            var ent = new Entity<ModsuitControlComponent>(suit, control);

            invSystem.TryEquip(human, suit, "back", force: true);
            modsuit.DeployAll(ent, silent: true);

            foreach (var part in control.Parts.Values)
            {
                var comp = entMan.GetComponent<ModsuitPartComponent>(part);
                modsuit.SetPartSealed(ent, (part, comp), true);
            }

            chassisSys.SetActive((suit, chassis), true);

            var boots = chassis.ModuleContainer!.ContainedEntities
                .Select(m => (uid: m, comp: entMan.GetComponent<ChassisModuleComponent>(m)))
                .First(m => entMan.HasComponent<ModuleMagbootsComponent>(m.uid));

            Assert.That(entMan.HasComponent<MagbootsComponent>(human), Is.False,
                "an idle module must not clamp anyone");

            Assert.That(moduleSys.TrySelect((boots.uid, boots.comp), human), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<MagbootsComponent>(human), Is.True,
                    "the module must hand the wearer real magboot behaviour");
                Assert.That(alerts.IsShowingAlert(human, MagbootsAlert), Is.True,
                    "the wearer needs a status icon while the magnets are live");
            });

            moduleSys.Deactivate((boots.uid, boots.comp), suit, human);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<MagbootsComponent>(human), Is.False,
                    "switching off must release the clamp");
                Assert.That(alerts.IsShowingAlert(human, MagbootsAlert), Is.False,
                    "the status icon must go away with it");
            });

            entMan.DeleteEntity(suit);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The storage module has to hand over a usable pocket the moment it is installed —
    ///     not once the suit is sealed — and take it away again when it is pulled.
    /// </summary>
    [Test]
    public async Task StorageModuleGivesAPocket()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var chassisSys = entMan.System<SharedModularChassisSystem>();

            var suit = entMan.SpawnEntity(SuitProto, MapCoordinates.Nullspace);
            var chassis = entMan.GetComponent<ModularChassisComponent>(suit);
            var chassisEnt = new Entity<ModularChassisComponent>(suit, chassis);

            Assert.That(entMan.HasComponent<StorageComponent>(suit), Is.False,
                "a bare suit has nowhere to put anything");

            var module = entMan.SpawnEntity("IS14ModuleStorage", MapCoordinates.Nullspace);
            var moduleComp = entMan.GetComponent<ChassisModuleComponent>(module);

            chassisSys.SetPanelOpen(chassisEnt, true);
            Assert.That(chassisSys.TryInstall(chassisEnt, (module, moduleComp)), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<StorageComponent>(suit), Is.True,
                    "installing the module must open the compartments up");

                Assert.That(moduleComp.Enabled, Is.True,
                    "the pocket has to work on an unsealed suit, or it is useless on the walk to the airlock");

                var storage = entMan.GetComponent<StorageComponent>(suit);

                Assert.That(storage.Grid, Is.Not.Empty);
                Assert.That(storage.OpenOnActivate, Is.True,
                    "E on a back item reaches for its pockets; the panel is on alt-interact");
            });

            Assert.That(chassisSys.TryUninstall(chassisEnt, (module, moduleComp)), Is.True);

            Assert.That(entMan.HasComponent<StorageComponent>(suit), Is.False,
                "pulling the module must take the pocket with it");

            entMan.DeleteEntity(suit);
            entMan.DeleteEntity(module);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A suit whose parts are all folded away is a suit that is off. Left running it
    ///     would keep billing the core for a costume back in its case.
    /// </summary>
    [Test]
    public async Task FoldingAwayShutsTheSuitDown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var modsuit = entMan.System<SharedModsuitSystem>();
            var invSystem = entMan.System<InventorySystem>();

            var human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var suit = entMan.SpawnEntity(DebugSuitProto, map.GridCoords);

            var control = entMan.GetComponent<ModsuitControlComponent>(suit);
            var chassis = entMan.GetComponent<ModularChassisComponent>(suit);
            var ent = new Entity<ModsuitControlComponent>(suit, control);

            invSystem.TryEquip(human, suit, "back", force: true);
            modsuit.DeployAll(ent, silent: true);

            foreach (var part in control.Parts.Values)
            {
                var comp = entMan.GetComponent<ModsuitPartComponent>(part);
                modsuit.SetPartSealed(ent, (part, comp), true);
            }

            Assert.That(chassis.Active, Is.True,
                "sealing a part should bring the suit up on its own");

            modsuit.RetractAll(ent, silent: true);

            Assert.Multiple(() =>
            {
                Assert.That(modsuit.AnyPartDeployed(ent), Is.False);
                Assert.That(chassis.Active, Is.False,
                    "a folded suit must stop drawing power");
            });

            entMan.DeleteEntity(suit);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Hits that land on the body underneath a piece of plating wear that piece down,
    ///     and only that piece — a club to the head must not scuff the boots.
    /// </summary>
    [Test]
    public async Task PlatingWearsFromHitsToTheBodyUnderIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var modsuit = entMan.System<SharedModsuitSystem>();
            var invSystem = entMan.System<InventorySystem>();
            var damageable = entMan.System<DamageableSystem>();

            var human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var suit = entMan.SpawnEntity(DebugSuitProto, map.GridCoords);

            var control = entMan.GetComponent<ModsuitControlComponent>(suit);
            var ent = new Entity<ModsuitControlComponent>(suit, control);

            invSystem.TryEquip(human, suit, "back", force: true);
            modsuit.DeployAll(ent, silent: true);

            var helmet = entMan.GetComponent<ModsuitPartComponent>(control.Parts["head"]);
            var boots = entMan.GetComponent<ModsuitPartComponent>(control.Parts["shoes"]);

            Assert.That(helmet.Integrity, Is.EqualTo(helmet.MaxIntegrity),
                "a fresh piece comes up intact");

            var blunt = protoMan.Index<DamageTypePrototype>("Blunt");

            damageable.TryChangeDamage(
                human,
                new DamageSpecifier(blunt, FixedPoint2.New(40)),
                targetPart: TargetBodyPart.Head,
                canMiss: false);

            Assert.Multiple(() =>
            {
                Assert.That(helmet.Integrity, Is.LessThan(helmet.MaxIntegrity),
                    "a hit to the head has to show up on the helmet");
                Assert.That(boots.Integrity, Is.EqualTo(boots.MaxIntegrity),
                    "and nowhere else");
            });

            entMan.DeleteEntity(suit);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Ion is what you point at a powered suit, so the plating takes it harder than
    ///     the same magnitude of anything mechanical.
    /// </summary>
    [Test]
    public async Task IonWearsThePlatingHarder()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var modsuit = entMan.System<SharedModsuitSystem>();
            var invSystem = entMan.System<InventorySystem>();
            var damageable = entMan.System<DamageableSystem>();

            var human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var suit = entMan.SpawnEntity(DebugSuitProto, map.GridCoords);

            var control = entMan.GetComponent<ModsuitControlComponent>(suit);
            var ent = new Entity<ModsuitControlComponent>(suit, control);

            invSystem.TryEquip(human, suit, "back", force: true);
            modsuit.DeployAll(ent, silent: true);

            var helmetUid = control.Parts["head"];
            var helmet = entMan.GetComponent<ModsuitPartComponent>(helmetUid);
            var part = new Entity<ModsuitPartComponent>(helmetUid, helmet);

            float Wear(string type)
            {
                modsuit.SetIntegrity(part, helmet.MaxIntegrity);

                damageable.TryChangeDamage(
                    human,
                    new DamageSpecifier(protoMan.Index<DamageTypePrototype>(type), FixedPoint2.New(30)),
                    targetPart: TargetBodyPart.Head,
                    canMiss: false);

                return helmet.MaxIntegrity - helmet.Integrity;
            }

            var blunt = Wear("Blunt");
            var ion = Wear("Ion");

            Assert.That(blunt, Is.GreaterThan(0f), "the control case has to land at all");

            // Deliberately loose: the mob's own resistances sit between the weapon and this
            // number, so the test asserts the multiplier is there, not its exact arithmetic.
            Assert.That(ion, Is.GreaterThan(blunt * 1.5f),
                "ion should cost the plating roughly twice what an equal hit does");

            entMan.DeleteEntity(suit);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Plating beaten past its break threshold keeps sealing but stops carrying the
    ///     modules bolted to it — the helmet still holds air, the light goes dark.
    /// </summary>
    [Test]
    public async Task BrokenPlatingDropsItsModules()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var modsuit = entMan.System<SharedModsuitSystem>();
            var chassisSys = entMan.System<SharedModularChassisSystem>();
            var invSystem = entMan.System<InventorySystem>();

            var human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var suit = entMan.SpawnEntity(DebugSuitProto, map.GridCoords);

            var control = entMan.GetComponent<ModsuitControlComponent>(suit);
            var chassis = entMan.GetComponent<ModularChassisComponent>(suit);
            var ent = new Entity<ModsuitControlComponent>(suit, control);
            var chassisEnt = new Entity<ModularChassisComponent>(suit, chassis);

            invSystem.TryEquip(human, suit, "back", force: true);
            modsuit.DeployAll(ent, silent: true);

            foreach (var piece in control.Parts.Values)
            {
                var comp = entMan.GetComponent<ModsuitPartComponent>(piece);
                modsuit.SetPartSealed(ent, (piece, comp), true);
            }

            var module = entMan.SpawnEntity("IS14ModuleFlashlight", map.GridCoords);
            var moduleComp = entMan.GetComponent<ChassisModuleComponent>(module);

            chassisSys.SetPanelOpen(chassisEnt, true);
            Assert.That(chassisSys.TryInstall(chassisEnt, (module, moduleComp)), Is.True);

            Assert.That(moduleComp.Enabled, Is.True,
                "a helmet module on a sealed helmet should be live");

            var helmetUid = control.Parts["head"];
            var helmet = entMan.GetComponent<ModsuitPartComponent>(helmetUid);

            // Just past the threshold, not destroyed.
            modsuit.SetIntegrity((helmetUid, helmet),
                helmet.MaxIntegrity * helmet.ModuleThreshold - 1f);

            Assert.Multiple(() =>
            {
                Assert.That(helmet.Sealed, Is.True,
                    "battered plating still holds pressure");
                Assert.That(moduleComp.Enabled, Is.False,
                    "but it stops offering the hardpoint the module needs");
            });

            entMan.DeleteEntity(suit);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A crowbar in the open panel takes the core out and leaves the modules where
    ///     they are — those come out through the interface now.
    /// </summary>
    [Test]
    public async Task PryingTakesTheCoreAndLeavesTheModules()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var chassisSys = entMan.System<SharedModularChassisSystem>();
            var itemSlots = entMan.System<ItemSlotsSystem>();

            var human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var suit = entMan.SpawnEntity(SuitProto, map.GridCoords);

            var chassis = entMan.GetComponent<ModularChassisComponent>(suit);
            var coreSlot = entMan.GetComponent<ModCoreSlotComponent>(suit);

            chassisSys.SetPanelOpen((suit, chassis), true);

            Assert.That(itemSlots.TryGetSlot(suit, coreSlot.SlotId, out var slot), Is.True);
            Assert.That(slot!.Item, Is.Not.Null, "the suit ships with a core in it");

            var modulesBefore = chassis.ModuleContainer!.ContainedEntities.Count;

            // Every ordinary route is shut: the context menu's eject verb, the slot
            // button and smart equip all come through here.
            Assert.Multiple(() =>
            {
                Assert.That(itemSlots.TryEject(suit, slot, human, out _), Is.False,
                    "nothing but the crowbar takes the core out");
                Assert.That(slot.Item, Is.Not.Null);
            });

            var pry = new ChassisPryEvent(human, false, false);
            entMan.EventBus.RaiseLocalEvent(suit, ref pry);

            Assert.Multiple(() =>
            {
                Assert.That(pry.Handled, Is.True, "something has to answer the crowbar");
                Assert.That(slot.Item, Is.Null, "and what it answers with is the core");
                Assert.That(chassis.ModuleContainer!.ContainedEntities, Has.Count.EqualTo(modulesBefore),
                    "modules are pulled from the interface, never levered out");
            });

            entMan.DeleteEntity(suit);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The second line: plating this far gone cannot hold pressure. It blows its own
    ///     seal and refuses to close again until somebody works on it.
    /// </summary>
    [Test]
    public async Task RupturedPlatingBlowsItsSeal()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var modsuit = entMan.System<SharedModsuitSystem>();
            var invSystem = entMan.System<InventorySystem>();

            var human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var suit = entMan.SpawnEntity(DebugSuitProto, map.GridCoords);

            var control = entMan.GetComponent<ModsuitControlComponent>(suit);
            var ent = new Entity<ModsuitControlComponent>(suit, control);

            invSystem.TryEquip(human, suit, "back", force: true);
            modsuit.DeployAll(ent, silent: true);

            foreach (var piece in control.Parts.Values)
            {
                var comp = entMan.GetComponent<ModsuitPartComponent>(piece);
                modsuit.SetPartSealed(ent, (piece, comp), true);
            }

            var helmetUid = control.Parts["head"];
            var helmet = entMan.GetComponent<ModsuitPartComponent>(helmetUid);
            var part = new Entity<ModsuitPartComponent>(helmetUid, helmet);

            var boots = entMan.GetComponent<ModsuitPartComponent>(control.Parts["shoes"]);

            modsuit.SetIntegrity(part, helmet.MaxIntegrity * helmet.UnsealThreshold - 1f);

            Assert.Multiple(() =>
            {
                Assert.That(helmet.Sealed, Is.False, "a split piece cannot stay closed");
                Assert.That(boots.Sealed, Is.True, "and the rest of the suit stays shut around it");
            });

            Assert.That(modsuit.TrySealPart(ent, helmetUid, true), Is.False,
                "and it must refuse to close again until it is repaired");

            entMan.DeleteEntity(suit);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A welder works dented plating back out; the fault kind follows what actually
    ///     did the damage, so brute-damaged plating asks for the welder and not for cable.
    /// </summary>
    [Test]
    public async Task WeldingPutsDentedPlatingBack()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var modsuit = entMan.System<SharedModsuitSystem>();
            var invSystem = entMan.System<InventorySystem>();
            var damageable = entMan.System<DamageableSystem>();

            var human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var suit = entMan.SpawnEntity(DebugSuitProto, map.GridCoords);

            var control = entMan.GetComponent<ModsuitControlComponent>(suit);
            var ent = new Entity<ModsuitControlComponent>(suit, control);

            invSystem.TryEquip(human, suit, "back", force: true);
            modsuit.DeployAll(ent, silent: true);

            var helmetUid = control.Parts["head"];
            var helmet = entMan.GetComponent<ModsuitPartComponent>(helmetUid);
            var part = new Entity<ModsuitPartComponent>(helmetUid, helmet);

            damageable.TryChangeDamage(
                human,
                new DamageSpecifier(protoMan.Index<DamageTypePrototype>("Blunt"), FixedPoint2.New(60)),
                targetPart: TargetBodyPart.Head,
                canMiss: false);

            var hurt = helmet.Integrity;

            Assert.Multiple(() =>
            {
                Assert.That(hurt, Is.LessThan(helmet.MaxIntegrity));
                Assert.That(modsuit.GetFault(part), Is.EqualTo(ChassisPartFault.Structural),
                    "a beating is plate work, not wiring");
            });

            // The repair itself, without the tool ceremony the interaction adds on top.
            modsuit.ChangeIntegrity(part, helmet.MaxIntegrity * helmet.RepairFraction);

            Assert.That(helmet.Integrity, Is.GreaterThan(hurt), "work has to put condition back");

            modsuit.SetIntegrity(part, helmet.MaxIntegrity);

            Assert.That(modsuit.GetFault(part), Is.EqualTo(ChassisPartFault.None),
                "a piece back at full condition has nothing left to ask for");

            entMan.DeleteEntity(suit);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Ion cooks wiring rather than bending plate, so the same piece asks for cable
    ///     instead of a welder.
    /// </summary>
    [Test]
    public async Task IonDamageAsksForCable()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var modsuit = entMan.System<SharedModsuitSystem>();
            var invSystem = entMan.System<InventorySystem>();
            var damageable = entMan.System<DamageableSystem>();

            var human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var suit = entMan.SpawnEntity(DebugSuitProto, map.GridCoords);

            var control = entMan.GetComponent<ModsuitControlComponent>(suit);
            var ent = new Entity<ModsuitControlComponent>(suit, control);

            invSystem.TryEquip(human, suit, "back", force: true);
            modsuit.DeployAll(ent, silent: true);

            var helmetUid = control.Parts["head"];
            var helmet = entMan.GetComponent<ModsuitPartComponent>(helmetUid);
            var part = new Entity<ModsuitPartComponent>(helmetUid, helmet);

            damageable.TryChangeDamage(
                human,
                new DamageSpecifier(protoMan.Index<DamageTypePrototype>("Ion"), FixedPoint2.New(40)),
                targetPart: TargetBodyPart.Head,
                canMiss: false);

            Assert.That(modsuit.GetFault(part), Is.EqualTo(ChassisPartFault.Electrical),
                "ion goes through the loom, so that is what has to be re-run");

            entMan.DeleteEntity(suit);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The pocket has to actually take things. The grid is handed over after the
    ///     storage component exists, and the occupancy mask is built once at init — miss
    ///     the rebuild and every insert is refused for want of space in a grid the
    ///     storage does not know it has.
    /// </summary>
    [Test]
    public async Task GrantedPocketAcceptsItems()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var modsuit = entMan.System<SharedModsuitSystem>();
            var invSystem = entMan.System<InventorySystem>();
            var storageSys = entMan.System<SharedStorageSystem>();

            var human = entMan.SpawnEntity("MobHuman", map.GridCoords);

            // The debug suit carries a storage module from map init, which is the path
            // a player actually meets.
            var suit = entMan.SpawnEntity(DebugSuitProto, map.GridCoords);
            var control = entMan.GetComponent<ModsuitControlComponent>(suit);

            invSystem.TryEquip(human, suit, "back", force: true);
            modsuit.DeployAll((suit, control), silent: true);

            var storage = entMan.GetComponent<StorageComponent>(suit);
            Assert.That(storage.Grid, Is.Not.Empty, "the module should have handed over a grid");

            var thing = entMan.SpawnEntity("Wrench", map.GridCoords);

            Assert.That(storageSys.CanInsert(suit, thing, out var reason, storage), Is.True,
                $"the pocket refused an ordinary item: {reason}");

            Assert.That(storageSys.Insert(suit, thing, out _, out _, storageComp: storage, user: human), Is.True);
            Assert.That(storage.Container.ContainedEntities, Does.Contain(thing));

            entMan.DeleteEntity(suit);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Every theme is five prototypes that differ by one sprite sheet, which is exactly
    ///     the shape of thing that ships with a typo nobody notices until a player spawns it.
    ///     This walks all of them: four parts, a core in the cradle, and integrated modules
    ///     that cost the wearer nothing.
    /// </summary>
    [Test]
    [TestCase("IS14ModsuitStandard")]
    [TestCase("IS14ModsuitEngineering")]
    [TestCase("IS14ModsuitMedical")]
    [TestCase("IS14ModsuitSecurity")]
    [TestCase("IS14ModsuitMining")]
    [TestCase("IS14ModsuitAtmospheric")]
    [TestCase("IS14ModsuitResearch")]
    [TestCase("IS14ModsuitSyndicate")]
    public async Task EveryThemeSpawnsWhole(string proto)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var itemSlots = entMan.System<ItemSlotsSystem>();

            var suit = entMan.SpawnEntity(proto, MapCoordinates.Nullspace);

            var control = entMan.GetComponent<ModsuitControlComponent>(suit);
            var chassis = entMan.GetComponent<ModularChassisComponent>(suit);
            var coreSlot = entMan.GetComponent<ModCoreSlotComponent>(suit);

            Assert.Multiple(() =>
            {
                Assert.That(control.Parts, Has.Count.EqualTo(4), $"{proto} should carry four parts");
                Assert.That(control.Parts.Keys,
                    Is.EquivalentTo(new[] { "head", "outerClothing", "gloves", "shoes" }));

                Assert.That(itemSlots.TryGetSlot(suit, coreSlot.SlotId, out var slot), Is.True);
                Assert.That(slot!.Item, Is.Not.Null, $"{proto} should ship with a core");

                // Built-in gear is part of the suit, not part of the player's budget.
                Assert.That(chassis.UsedComplexity, Is.Zero, $"{proto} spends budget on its own gear");
            });

            foreach (var (slotName, part) in control.Parts)
            {
                var comp = entMan.GetComponent<ModsuitPartComponent>(part);

                Assert.Multiple(() =>
                {
                    Assert.That(comp.Slot, Is.EqualTo(slotName));
                    Assert.That(comp.MaxIntegrity, Is.GreaterThan(0f), $"{proto} {slotName} has no plating");
                    Assert.That(comp.Integrity, Is.EqualTo(comp.MaxIntegrity));
                    Assert.That(comp.CoveredParts, Is.Not.Empty,
                        $"{proto} {slotName} covers nothing, so nothing can ever wear it down");
                });
            }

            foreach (var module in chassis.ModuleContainer!.ContainedEntities)
            {
                var comp = entMan.GetComponent<ChassisModuleComponent>(module);

                Assert.Multiple(() =>
                {
                    Assert.That(comp.Removable, Is.False, "integrated gear is not pulled by hand");
                    Assert.That(comp.Complexity, Is.Zero);
                });
            }

            entMan.DeleteEntity(suit);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The wire panel's status keys ride inside the interface state, so the client has
    ///     to be able to name their type. A key defined server-side compiles, lints and
    ///     starts up perfectly — and then kills the server mid-tick the first time somebody
    ///     opens the panel, because PVS cannot serialise a type the client has never heard of.
    /// </summary>
    [Test]
    public async Task WireKeysAreNetSerialisable()
    {
        await using var pair = await PoolManager.GetServerClient();
        var serializer = pair.Server.ResolveDependency<IRobustSerializer>();

        Assert.That(serializer.CanSerialize(typeof(ModsuitWireKey)), Is.True,
            "the panel's status keys have to live in a shared assembly");

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     An EMP is applied to whatever holds the charge, and that is the cell — nested
    ///     inside the core, inside the suit. Resistance sitting on the chassis is read by
    ///     nobody, which is how the shield module managed to do nothing at all.
    /// </summary>
    [Test]
    public async Task EmpShieldCoversTheCell()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var chassisSys = entMan.System<SharedModularChassisSystem>();
            var itemSlots = entMan.System<ItemSlotsSystem>();
            var core = entMan.System<ModCoreSystem>();

            var suit = entMan.SpawnEntity(SuitProto, map.GridCoords);
            var chassis = entMan.GetComponent<ModularChassisComponent>(suit);
            var chassisEnt = new Entity<ModularChassisComponent>(suit, chassis);
            var coreSlot = entMan.GetComponent<ModCoreSlotComponent>(suit);

            Assert.That(core.GetCore((suit, coreSlot)), Is.Not.Null);
            var coreUid = core.GetCore((suit, coreSlot))!.Value.Owner;

            Assert.That(itemSlots.TryGetSlot(coreUid, "cell_slot", out var slot), Is.True);
            Assert.That(slot!.Item, Is.Not.Null, "the standard core runs off a swappable cell");
            var cell = slot.Item!.Value;

            // Two containers deep and unprotected.
            var before = new EmpAttemptEvent();
            entMan.EventBus.RaiseLocalEvent(cell, ref before);
            Assert.That(before.Cancelled, Is.False, "nothing shields the cell yet");

            var module = entMan.SpawnEntity("IS14ModuleEmpShield", map.GridCoords);
            var moduleComp = entMan.GetComponent<ChassisModuleComponent>(module);

            chassisSys.SetPanelOpen(chassisEnt, true);
            Assert.That(chassisSys.TryInstall(chassisEnt, (module, moduleComp)), Is.True);

            var after = new EmpAttemptEvent();
            entMan.EventBus.RaiseLocalEvent(cell, ref after);
            Assert.That(after.Cancelled, Is.True,
                "the screen has to answer for everything nested inside the suit");

            Assert.That(chassisSys.TryUninstall(chassisEnt, (module, moduleComp)), Is.True);

            var pulled = new EmpAttemptEvent();
            entMan.EventBus.RaiseLocalEvent(cell, ref pulled);
            Assert.That(pulled.Cancelled, Is.False, "and stop answering once it is pulled");

            entMan.DeleteEntity(suit);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A flat suit sealing up would be a coffin: the seal draws power to hold, and the
    ///     wearer would be shut into a shell that cannot open itself.
    /// </summary>
    [Test]
    public async Task FlatSuitRefusesToSeal()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.ResolveDependency<IEntityManager>();

        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var modsuit = entMan.System<SharedModsuitSystem>();
            var invSystem = entMan.System<InventorySystem>();
            var power = entMan.System<ChassisPowerSystem>();

            var human = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var suit = entMan.SpawnEntity(SuitProto, map.GridCoords);

            var control = entMan.GetComponent<ModsuitControlComponent>(suit);
            var ent = new Entity<ModsuitControlComponent>(suit, control);

            invSystem.TryEquip(human, suit, "back", force: true);
            modsuit.DeployAll(ent, silent: true);

            var helmet = control.Parts["head"];

            Assert.That(modsuit.TrySealPart(ent, helmet, true), Is.True, "a charged suit seals");
            modsuit.SetPartSealed(ent, (helmet, entMan.GetComponent<ModsuitPartComponent>(helmet)), false);

            // Empty the core down to nothing.
            var charge = power.GetCharge(suit).Current;
            Assert.That(charge, Is.GreaterThan(0f));
            Assert.That(power.TryUseCharge(suit, charge), Is.True);
            Assert.That(power.GetCharge(suit).Current, Is.EqualTo(0f).Within(0.01f));

            Assert.That(modsuit.TrySealPart(ent, helmet, true), Is.False,
                "a flat suit must not close up");

            entMan.DeleteEntity(suit);
            entMan.DeleteEntity(human);
        });

        await pair.CleanReturnAsync();
    }
}
