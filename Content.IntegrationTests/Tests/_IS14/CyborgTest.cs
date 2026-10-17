// Licensed under IS14's EULA, see EULA.txt for more information.

#nullable enable
using System.Linq;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared._IS14.Cyborgs.Prototypes;
using Content.Shared._IS14.Cyborgs.Systems;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Radio.Components;
using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Fluids;
using Content.Shared.Fluids.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Item.ItemToggle;
using Content.Shared.PowerCell;
using Content.Shared.Storage.EntitySystems;
using Content.Server.Power.Components;
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Content.Shared.Lock;
using Content.Shared.Wires;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Systems;
using Robust.Shared.GameObjects;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._IS14;

/// <summary>
///     Checks the core of the tgstation-style cyborgs: models, tool storage and tool slots.
/// </summary>
[TestFixture]
[TestOf(typeof(SharedCyborgSystem))]
public sealed class CyborgTest
{
    private const string NoModelProto = "IS14CyborgNT";
    private const string EngineeringProto = "IS14CyborgNTEngineering";
    private static readonly ProtoId<CyborgModelPrototype> EngineeringModel = "IS14CyborgModelEngineering";

    /// <summary>
    ///     Every model must point at existing skins and tools, and every tool must be a cyborg tool.
    /// </summary>
    [Test]
    public async Task ModelsAreValid()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var model in protoMan.EnumeratePrototypes<CyborgModelPrototype>())
                {
                    Assert.That(model.Skins, Is.Not.Empty, $"{model.ID} has no skins");

                    foreach (var skin in model.Skins)
                    {
                        Assert.That(protoMan.HasIndex(skin), $"{model.ID} uses unknown skin {skin}");
                    }

                    foreach (var tool in model.BasicTools.Concat(model.EmagTools))
                    {
                        Assert.That(protoMan.TryIndex(tool, out var toolProto), $"{model.ID} uses unknown tool {tool}");
                        Assert.That(toolProto!.TryGetComponent<CyborgToolComponent>(out _,
                            server.ResolveDependency<IComponentFactory>()),
                            $"{tool} in {model.ID} is missing the CyborgTool component");
                    }
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Every state a skin names must exist in its RSI, otherwise the cyborg turns invisible in game.
    /// </summary>
    [Test]
    public async Task SkinsHaveSprites()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var resMan = server.ResolveDependency<IResourceManager>();

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                foreach (var skin in protoMan.EnumeratePrototypes<CyborgSkinPrototype>())
                {
                    var states = new[]
                    {
                        skin.BodyState, skin.MovingBodyState, skin.EyesState, skin.NoMindEyesState, skin.LightState,
                    };

                    foreach (var state in states)
                    {
                        if (state == null)
                            continue;

                        var path = new ResPath("/Textures") / skin.Sprite / $"{state}.png";
                        Assert.That(resMan.ContentFileExists(path), $"{skin.ID} is missing {path}");
                    }
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A cyborg spawned with a model gets its tools, channels and traits; resetting takes them away.
    /// </summary>
    [Test]
    public async Task ModelAppliesAndResets()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var cyborgSys = entMan.System<SharedCyborgSystem>();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity(EngineeringProto, map.GridCoords);
            var cyborg = entMan.GetComponent<CyborgComponent>(uid);
            var model = protoMan.Index(EngineeringModel);

            Assert.Multiple(() =>
            {
                Assert.That(cyborg.Model, Is.EqualTo(EngineeringModel));
                Assert.That(cyborg.Skin, Is.EqualTo(model.Skins[0]));
                Assert.That(cyborg.OsEntity is { } os &&
                    entMan.HasComponent<Content.Shared._IS14.OS.Components.IS14OsDeviceComponent>(os),
                    "every cyborg gets its built-in tablet");
                Assert.That(cyborg.ToolContainer.ContainedEntities,
                    Has.Count.EqualTo(model.BasicTools.Count + model.EmagTools.Count));
                Assert.That(entMan.HasComponent<MovementIgnoreGravityComponent>(uid), "model trait missing");

                var radio = entMan.GetComponent<IntrinsicRadioTransmitterComponent>(uid);
                Assert.That(radio.Channels.Select(c => c.Id), Does.Contain("Engineering"));
                Assert.That(radio.Channels.Select(c => c.Id), Does.Contain("Common"));
            });

            cyborgSys.ResetModel((uid, cyborg));

            Assert.Multiple(() =>
            {
                Assert.That(cyborg.Model, Is.Null);
                Assert.That(cyborg.ToolContainer.ContainedEntities, Is.Empty);
                Assert.That(entMan.HasComponent<MovementIgnoreGravityComponent>(uid), Is.False, "trait not removed");

                var radio = entMan.GetComponent<IntrinsicRadioTransmitterComponent>(uid);
                Assert.That(radio.Channels.Select(c => c.Id), Does.Not.Contain("Engineering"));
            });

            entMan.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Every model can be applied: all of its tools spawn and fit into the storage.
    /// </summary>
    [Test]
    public async Task AllModelsApply()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();
        var cyborgSys = entMan.System<SharedCyborgSystem>();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity(NoModelProto, map.GridCoords);
            var cyborg = entMan.GetComponent<CyborgComponent>(uid);

            Assert.Multiple(() =>
            {
                foreach (var model in protoMan.EnumeratePrototypes<CyborgModelPrototype>())
                {
                    cyborgSys.ApplyModel((uid, cyborg), model.ID);
                    Assert.That(cyborg.Model, Is.EqualTo(new ProtoId<CyborgModelPrototype>(model.ID)));
                    Assert.That(cyborg.ToolContainer.ContainedEntities,
                        Has.Count.EqualTo(model.BasicTools.Count + model.EmagTools.Count),
                        $"{model.ID} lost tools on spawn");
                }
            });

            entMan.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Tools go from storage into the slots and back, the slots fill up,
    ///     and powering down puts everything away.
    /// </summary>
    [Test]
    public async Task ToolsDeployAndStore()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var cyborgSys = entMan.System<SharedCyborgSystem>();
        var handsSys = entMan.System<SharedHandsSystem>();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity(EngineeringProto, map.GridCoords);
            var cyborg = entMan.GetComponent<CyborgComponent>(uid);
            var ent = (uid, cyborg);

            Assert.That(handsSys.EnumerateHands(uid).Count(), Is.EqualTo(cyborg.ToolSlots));

            var tools = cyborg.ToolContainer.ContainedEntities.ToList();

            // Without a player the cyborg is inactive and can't take tools out.
            Assert.That(cyborgSys.TryDeployTool(ent, (tools[0], entMan.GetComponent<CyborgToolComponent>(tools[0]))),
                Is.False);

            cyborgSys.SetActive(ent, true);

            for (var i = 0; i < cyborg.ToolSlots; i++)
            {
                var tool = tools[i];
                Assert.That(cyborgSys.TryDeployTool(ent, (tool, entMan.GetComponent<CyborgToolComponent>(tool))),
                    $"tool {i} should fit");
                Assert.That(cyborgSys.IsToolHeld(ent, tool, out _));
                Assert.That(entMan.HasComponent<UnremoveableComponent>(tool), "held tool must not be droppable");
            }

            var extra = tools[cyborg.ToolSlots];
            Assert.That(cyborgSys.TryDeployTool(ent, (extra, entMan.GetComponent<CyborgToolComponent>(extra))),
                Is.False, "slots should be full");

            cyborgSys.StoreTool(ent, tools[0]);
            Assert.Multiple(() =>
            {
                Assert.That(cyborgSys.IsToolHeld(ent, tools[0], out _), Is.False);
                Assert.That(cyborg.ToolContainer.Contains(tools[0]));
                Assert.That(entMan.HasComponent<UnremoveableComponent>(tools[0]), Is.False);
            });

            cyborgSys.SetActive(ent, false);
            Assert.That(cyborg.ToolContainer.ContainedEntities, Has.Count.EqualTo(tools.Count),
                "powering down should store every tool");

            entMan.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A synthesized stack used up to zero stays in the kit with one unit instead of despawning.
    /// </summary>
    [Test]
    public async Task StackSynthesizerSurvivesEmpty()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var stackSys = entMan.System<SharedStackSystem>();

        EntityUid sheets = default;
        await server.WaitAssertion(() =>
        {
            // The engineering cyborg carries real sheets now, the saboteur still synthesizes them.
            var uid = entMan.SpawnEntity("IS14CyborgSyndicateSaboteur", map.GridCoords);
            var cyborg = entMan.GetComponent<CyborgComponent>(uid);

            sheets = cyborg.ToolContainer.ContainedEntities
                .Single(e => entMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID == "IS14CyborgToolSheetSteel");
            var stack = entMan.GetComponent<StackComponent>(sheets);

            Assert.That(stack.Count, Is.EqualTo(stackSys.GetMaxCount(stack)), "synthesizer should spawn full");
            Assert.That(stackSys.TryUse((sheets, stack), stack.Count), "using the whole stack should work");
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.EntityExists(sheets), "an emptied synthesizer must not despawn");
            Assert.That(entMan.GetComponent<StackComponent>(sheets).Count, Is.EqualTo(1));
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Upgrade boards: model checks, duplicates, emag unlock, and what survives a model change.
    /// </summary>
    [Test]
    public async Task UpgradesInstallAndSurviveReset()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var cyborgSys = entMan.System<SharedCyborgSystem>();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity(EngineeringProto, map.GridCoords);
            var cyborg = entMan.GetComponent<CyborgComponent>(uid);
            var ent = (uid, cyborg);
            var user = entMan.SpawnEntity(null, map.GridCoords);
            var baseTools = cyborg.ToolContainer.ContainedEntities.Count;

            Entity<CyborgUpgradeComponent> Board(string proto)
            {
                var board = entMan.SpawnEntity(proto, map.GridCoords);
                return (board, entMan.GetComponent<CyborgUpgradeComponent>(board));
            }

            // Closed cover refuses everything.
            Assert.That(cyborgSys.TryInstallUpgrade(ent, Board("IS14CyborgUpgradeIonThrusters"), user), Is.False);

            entMan.System<LockSystem>().Unlock(uid, null);
            Assert.That(entMan.System<SharedWiresSystem>()
                .TogglePanel(uid, entMan.GetComponent<WiresPanelComponent>(uid), true), "cover should open");

            Assert.Multiple(() =>
            {
                Assert.That(cyborgSys.TryInstallUpgrade(ent, Board("IS14CyborgUpgradeDiamondDrill"), user), Is.False,
                    "miner board must not fit an engineering cyborg");
                Assert.That(cyborgSys.TryInstallUpgrade(ent, Board("IS14CyborgUpgradeAdvancedTools"), user));
                Assert.That(cyborg.ToolContainer.ContainedEntities, Has.Count.EqualTo(baseTools + 3 - 1),
                    "three new tools, the experimental welder replaces the basic one");
                Assert.That(cyborgSys.TryInstallUpgrade(ent, Board("IS14CyborgUpgradeAdvancedTools"), user), Is.False,
                    "duplicate board");
                Assert.That(cyborgSys.TryInstallUpgrade(ent, Board("IS14CyborgUpgradeSelfRepair"), user));
                Assert.That(entMan.HasComponent<PassiveDamageComponent>(uid));
                Assert.That(cyborgSys.TryInstallUpgrade(ent, Board("IS14CyborgUpgradeIllegal"), user));
                Assert.That(cyborg.EmagToolsUnlocked);
            });

            Assert.That(cyborgSys.TryInstallUpgrade(ent, Board("IS14CyborgUpgradeReclassification"), user));

            Assert.Multiple(() =>
            {
                Assert.That(cyborg.Model, Is.Null);
                Assert.That(cyborg.ToolContainer.ContainedEntities, Is.Empty, "model-specific board tools must go");
                Assert.That(cyborg.UpgradeContainer.ContainedEntities, Has.Count.EqualTo(2),
                    "generic boards stay, the engineering one is lost");
                Assert.That(entMan.HasComponent<PassiveDamageComponent>(uid), "generic board effect must stay");
            });

            entMan.DeleteEntity(uid);
            entMan.DeleteEntity(user);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Pulling a board out takes its tools and traits, but keeps a trait the model also gives.
    /// </summary>
    [Test]
    public async Task UpgradeRemovalTakesItsToolsOnly()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var cyborgSys = entMan.System<SharedCyborgSystem>();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity(EngineeringProto, map.GridCoords);
            var cyborg = entMan.GetComponent<CyborgComponent>(uid);
            var ent = (uid, cyborg);
            var user = entMan.SpawnEntity(null, map.GridCoords);
            var baseTools = cyborg.ToolContainer.ContainedEntities.Count;

            entMan.System<LockSystem>().Unlock(uid, null);
            entMan.System<SharedWiresSystem>().TogglePanel(uid, entMan.GetComponent<WiresPanelComponent>(uid), true);

            var tools = entMan.SpawnEntity("IS14CyborgUpgradeAdvancedTools", map.GridCoords);
            var thrusters = entMan.SpawnEntity("IS14CyborgUpgradeIonThrusters", map.GridCoords);
            Assert.That(cyborgSys.TryInstallUpgrade(ent, (tools, entMan.GetComponent<CyborgUpgradeComponent>(tools)),
                user));
            Assert.That(cyborgSys.TryInstallUpgrade(ent,
                (thrusters, entMan.GetComponent<CyborgUpgradeComponent>(thrusters)), user));
            Assert.That(CountTools(entMan, cyborg, "IS14CyborgToolWelder"), Is.Zero,
                "the board replaces the basic welder");

            Assert.That(cyborgSys.TryRemoveUpgrade(ent, tools, user));
            Assert.That(cyborgSys.TryRemoveUpgrade(ent, thrusters, user));

            Assert.Multiple(() =>
            {
                Assert.That(cyborg.ToolContainer.ContainedEntities, Has.Count.EqualTo(baseTools),
                    "board tools must go");
                Assert.That(CountTools(entMan, cyborg, "IS14CyborgToolWelder"), Is.EqualTo(1),
                    "the replaced welder comes back");
                Assert.That(cyborg.UpgradeContainer.ContainedEntities, Is.Empty);
                Assert.That(entMan.HasComponent<MovementIgnoreGravityComponent>(uid),
                    "the engineering model also gives gravity negation, removing thrusters must keep it");
                Assert.That(entMan.EntityExists(tools), "a removed board is handed back, not deleted");
            });

            entMan.DeleteEntity(uid);
            entMan.DeleteEntity(user);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The clown model picker switches the model, the rename board needs a name and renames the cyborg.
    ///     Both are used up.
    /// </summary>
    [Test]
    public async Task ModelPickerAndRenameBoards()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var cyborgSys = entMan.System<SharedCyborgSystem>();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity(EngineeringProto, map.GridCoords);
            var cyborg = entMan.GetComponent<CyborgComponent>(uid);
            var ent = (uid, cyborg);
            var user = entMan.SpawnEntity(null, map.GridCoords);

            entMan.System<LockSystem>().Unlock(uid, null);
            entMan.System<SharedWiresSystem>().TogglePanel(uid, entMan.GetComponent<WiresPanelComponent>(uid), true);

            var clown = entMan.SpawnEntity("IS14CyborgUpgradeClown", map.GridCoords);
            Assert.That(cyborgSys.TryInstallUpgrade(ent, (clown, entMan.GetComponent<CyborgUpgradeComponent>(clown)),
                user));
            Assert.Multiple(() =>
            {
                Assert.That(cyborg.Model, Is.EqualTo(new ProtoId<CyborgModelPrototype>("IS14CyborgModelClown")));
                Assert.That(CountTools(entMan, cyborg, "IS14CyborgToolClownHypo"), Is.EqualTo(1));
                Assert.That(CountTools(entMan, cyborg, "IS14CyborgToolRCD"), Is.Zero, "engineering tools must go");
                Assert.That(entMan.Deleted(clown) || entMan.IsQueuedForDeletion(clown), "model picker is used up");
            });

            var rename = entMan.SpawnEntity("IS14CyborgUpgradeRename", map.GridCoords);
            var renameUpgrade = (rename, entMan.GetComponent<CyborgUpgradeComponent>(rename));
            Assert.That(cyborgSys.TryInstallUpgrade(ent, renameUpgrade, user), Is.False, "no name written yet");

            entMan.GetComponent<CyborgRenameBoardComponent>(rename).NewName = "Robo Honk";
            Assert.That(cyborgSys.TryInstallUpgrade(ent, renameUpgrade, user));
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<MetaDataComponent>(uid).EntityName, Does.StartWith("Robo Honk"));
                Assert.That(cyborg.UpgradeContainer.ContainedEntities, Is.Empty, "consumed boards are not stored");
            });

            entMan.DeleteEntity(uid);
            entMan.DeleteEntity(user);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Chassis damage knocks the tool slots offline from the last one, like tg, and repairs bring them back.
    /// </summary>
    [Test]
    public async Task SlotsBreakWithDamage()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var cyborgSys = entMan.System<SharedCyborgSystem>();
        var damageSys = entMan.System<DamageableSystem>();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity(EngineeringProto, map.GridCoords);
            var cyborg = entMan.GetComponent<CyborgComponent>(uid);
            var damageable = entMan.GetComponent<DamageableComponent>(uid);

            DamageSpecifier Blunt(int amount) => new() { DamageDict = { ["Blunt"] = amount } };

            damageSys.TryChangeDamage(uid, Blunt(60), ignoreResistances: true, canMiss: false);
            Assert.That(cyborg.BrokenSlots, Is.EqualTo(1), "half of the critical damage breaks the third slot");
            Assert.That(cyborgSys.IsSlotWorking(cyborg, 2), Is.False);
            Assert.That(cyborgSys.IsSlotWorking(cyborg, 1));

            damageSys.TryChangeDamage(uid, Blunt(20), ignoreResistances: true, canMiss: false);
            Assert.That(cyborg.BrokenSlots, Is.EqualTo(2),
                "three quarters of the critical damage break the second slot");

            damageSys.TryChangeDamage(uid, Blunt(20), ignoreResistances: true, canMiss: false);
            Assert.That(cyborg.BrokenSlots, Is.EqualTo(3), "critical damage takes every slot");

            damageSys.SetAllDamage(uid, damageable, 0);
            Assert.That(cyborg.BrokenSlots, Is.Zero, "repairs bring every slot back");

            entMan.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     tg's tipping: a tipped cyborg can't move or take out modules, and rights itself after a while.
    /// </summary>
    [Test]
    public async Task TippingAndSelfRighting()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var timing = server.ResolveDependency<IGameTiming>();
        var tipping = entMan.System<CyborgTippingSystem>();
        EntityUid uid = default;

        await server.WaitAssertion(() =>
        {
            uid = entMan.SpawnEntity(EngineeringProto, map.GridCoords);
            var tippable = entMan.GetComponent<CyborgTippableComponent>(uid);
            tipping.SetTipped((uid, tippable), true);

            var speed = entMan.GetComponent<MovementSpeedModifierComponent>(uid);
            Assert.Multiple(() =>
            {
                Assert.That(tippable.Tipped);
                Assert.That(speed.CurrentSprintSpeed, Is.Zero, "a tipped cyborg can only turn around");
            });

            Assert.That(tipping.TrySelfRight((uid, tippable)), Is.False, "it has to wait before rolling over itself");
            tippable.SelfRightAt = timing.CurTime;
            Assert.That(tipping.TrySelfRight((uid, tippable)), "after the wait it may roll over itself");

            // Nobody playing it: skip to the fallback.
            tippable.AutoRightAt = timing.CurTime;
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var tippable = entMan.GetComponent<CyborgTippableComponent>(uid);
            Assert.That(tippable.Tipped, Is.False, "the cyborg rights itself once the time is up");
            Assert.That(entMan.GetComponent<MovementSpeedModifierComponent>(uid).CurrentSprintSpeed, Is.GreaterThan(0));
            entMan.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A cyborg linked to an AI copies its laws, and the AI's lockdown stops it in place.
    /// </summary>
    [Test]
    public async Task AiLinkSyncsLawsAndLocksDown()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var linkSys = entMan.System<Content.Server._IS14.Cyborgs.CyborgAiLinkSystem>();
        var lawSys = entMan.System<Content.Server.Silicons.Laws.SiliconLawSystem>();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity(EngineeringProto, map.GridCoords);
            var ai = entMan.SpawnEntity("StationAiBrain", map.GridCoords);
            entMan.EnsureComponent<Content.Shared.Silicons.StationAi.StationAiHeldComponent>(ai);
            var link = entMan.GetComponent<CyborgAiLinkComponent>(uid);

            linkSys.Link((uid, link), ai);
            var aiLaws = lawSys.GetLaws(ai).Laws.Select(l => l.LawString).ToList();
            var borgLaws = lawSys.GetLaws(uid).Laws.Select(l => l.LawString).ToList();
            Assert.That(borgLaws, Is.EqualTo(aiLaws), "linking copies the AI's laws");

            linkSys.SetLockdownByAi((uid, link), true, ai);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<MovementSpeedModifierComponent>(uid).CurrentSprintSpeed, Is.Zero);
                Assert.That(entMan.System<Content.Shared.ActionBlocker.ActionBlockerSystem>().CanInteract(uid, null),
                    Is.False, "a locked down cyborg can't act");
            });

            linkSys.SetLockdownByAi((uid, link), false, ai);
            Assert.That(entMan.GetComponent<MovementSpeedModifierComponent>(uid).CurrentSprintSpeed, Is.GreaterThan(0));

            entMan.DeleteEntity(uid);
            entMan.DeleteEntity(ai);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Xenoborgs run on the new system: their model, kit and radio come from the chassis.
    /// </summary>
    [TestCase("IS14XenoborgEngi")]
    [TestCase("IS14XenoborgHeavy")]
    [TestCase("IS14XenoborgScout")]
    [TestCase("IS14XenoborgStealthPrinted")]
    public async Task XenoborgsSpawnWithKits(string proto)
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity(proto, map.GridCoords);
            var cyborg = entMan.GetComponent<CyborgComponent>(uid);
            Assert.That(cyborg.Model, Is.Not.Null);
            var model = protoMan.Index(cyborg.Model!.Value);

            var transmitter = entMan.GetComponent<IntrinsicRadioTransmitterComponent>(uid);
            var active = entMan.GetComponent<ActiveRadioComponent>(uid);
            Assert.Multiple(() =>
            {
                Assert.That(cyborg.ToolContainer.ContainedEntities, Has.Count.EqualTo(model.BasicTools.Count));
                Assert.That(transmitter.Channels.Select(c => c.Id), Is.EquivalentTo(new[] { "Xenoborg" }),
                    "xenoborgs only talk on their own channel");
                Assert.That(active.Channels.Select(c => c.Id), Does.Contain("Mothership"), "but hear the mothership");
                Assert.That(entMan.HasComponent<CyborgTransponderComponent>(uid));
            });

            entMan.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     tg's ridable cyborgs: a rider buckled onto a cyborg goes wherever it goes.
    /// </summary>
    [Test]
    public async Task RiderMovesWithCyborg()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var xformSys = entMan.System<SharedTransformSystem>();

        await server.WaitAssertion(() =>
        {
            var borg = entMan.SpawnEntity(EngineeringProto, map.GridCoords);
            var human = entMan.SpawnEntity("MobHuman", map.GridCoords);

            Assert.That(entMan.System<Content.Shared.Buckle.SharedBuckleSystem>().TryBuckle(human, human, borg),
                "a human can climb onto a cyborg");

            xformSys.SetCoordinates(borg, new EntityCoordinates(map.Grid, new Vector2(3.5f, 0.5f)));
            var distance = (xformSys.GetWorldPosition(human) - xformSys.GetWorldPosition(borg)).Length();
            Assert.That(distance, Is.LessThan(0.5f), "the rider moves with the cyborg");

            entMan.DeleteEntity(human);
            entMan.DeleteEntity(borg);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The hug module only offers shock to the peacekeeper, or to anyone once emagged.
    /// </summary>
    [Test]
    public async Task HugModuleModes()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var user = entMan.SpawnEntity(null, map.GridCoords);
            var plain = entMan.SpawnEntity("IS14CyborgToolHugModule", map.GridCoords);
            var peace = entMan.SpawnEntity("IS14CyborgToolHugModulePeacekeeper", map.GridCoords);

            var ev = new UseInHandEvent(user);
            entMan.EventBus.RaiseLocalEvent(plain, ev);
            var ev2 = new UseInHandEvent(user);
            entMan.EventBus.RaiseLocalEvent(peace, ev2);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<CyborgHugModuleComponent>(plain).Mode, Is.EqualTo(CyborgHugMode.Hug),
                    "without an emag there is nothing but hugs");
                Assert.That(entMan.GetComponent<CyborgHugModuleComponent>(peace).Mode, Is.EqualTo(CyborgHugMode.Shock),
                    "the peacekeeper module may shock");
            });

            entMan.DeleteEntity(plain);
            entMan.DeleteEntity(peace);
            entMan.DeleteEntity(user);
        });

        await pair.CleanReturnAsync();
    }

    private static int CountTools(IEntityManager entMan, CyborgComponent cyborg, string proto)
    {
        return cyborg.ToolContainer.ContainedEntities.Count(tool =>
            entMan.GetComponent<MetaDataComponent>(tool).EntityPrototype?.ID == proto);
    }

    /// <summary>
    ///     Auto-wash works like the mop: tank water goes into the puddle, the mess goes into the tank,
    ///     and the puddle stays as water instead of vanishing.
    /// </summary>
    [Test]
    public async Task AutoWashSwapsPuddleWithTankWater()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var protoMan = server.ResolveDependency<IPrototypeManager>();

        await server.WaitAssertion(() =>
        {
            var mapSys = entMan.System<SharedMapSystem>();
            var xformSys = entMan.System<SharedTransformSystem>();
            var solutionSys = entMan.System<SharedSolutionContainerSystem>();

            // A second floor tile next to the first, straight east so it is the same grid.
            mapSys.SetTile(map.Grid, new Vector2i(1, 0), map.Tile.Tile);
            var next = new EntityCoordinates(map.Grid, new Vector2(1.5f, 0.5f));
            var start = new EntityCoordinates(map.Grid, new Vector2(0.5f, 0.5f));

            Assert.That(entMan.System<SharedPuddleSystem>().TrySpillAt(next, new Solution("Blood", 20), out var puddle,
                sound: false));

            var uid = entMan.SpawnEntity("IS14CyborgNTJanitor", start);
            var cyborg = entMan.GetComponent<CyborgComponent>(uid);
            var wash = entMan.GetComponent<CyborgAutoWashComponent>(uid);
            wash.Enabled = true;

            var tank = cyborg.ToolContainer.ContainedEntities
                .Single(e => entMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID == "IS14CyborgToolBucket");
            Assert.That(solutionSys.TryGetSolution(tank, "drink", out _, out var tankSolution));
            Assert.That(tankSolution!.GetTotalPrototypeQuantity("Water"), Is.EqualTo(FixedPoint2.New(500)),
                "the internal tank comes full");

            xformSys.SetCoordinates(uid, next);

            var puddleComp = entMan.GetComponent<PuddleComponent>(puddle);
            Assert.That(solutionSys.TryGetSolution(puddle, puddleComp.SolutionName, out _, out var puddleSolution));
            Assert.Multiple(() =>
            {
                Assert.That(entMan.EntityExists(puddle), "the puddle is washed, not deleted");
                Assert.That(puddleSolution!.GetTotalPrototypeQuantity("Blood"), Is.EqualTo(FixedPoint2.Zero));
                Assert.That(puddleSolution.GetTotalPrototypeQuantity("Water"), Is.EqualTo(FixedPoint2.New(20)));
                Assert.That(tankSolution.GetTotalPrototypeQuantity("Blood"), Is.EqualTo(FixedPoint2.New(20)),
                    "the mess goes into the tank");
                Assert.That(tankSolution.GetTotalPrototypeQuantity("Water"), Is.EqualTo(FixedPoint2.New(480)));
            });

            entMan.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The medical omnitool swaps whole surgery components per mode, and the hypospray picks reagents.
    /// </summary>
    [Test]
    public async Task MedicalOmnitoolAndHypo()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity("IS14CyborgNTMedical", map.GridCoords);
            var cyborg = entMan.GetComponent<CyborgComponent>(uid);
            var tools = cyborg.ToolContainer.ContainedEntities;
            string? Proto(EntityUid e) => entMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID;

            var omnitools = tools.Where(e => Proto(e) == "IS14CyborgToolMedicalOmnitool").ToList();
            Assert.That(omnitools, Has.Count.EqualTo(2), "medical cyborgs get two omnitools like engineers");

            var omni = omnitools[0];
            Assert.That(entMan.HasComponent<ScalpelComponent>(omni), "starts as a scalpel");

            var ev = new UseInHandEvent(uid);
            entMan.EventBus.RaiseLocalEvent(omni, ev);
            Assert.Multiple(() =>
            {
                Assert.That(entMan.HasComponent<ScalpelComponent>(omni), Is.False, "scalpel mode left");
                Assert.That(entMan.HasComponent<HemostatComponent>(omni), "second mode is the hemostat");
                Assert.That(entMan.HasComponent<SurgeryToolComponent>(omni), "every mode is a surgery tool");
            });

            var hypo = tools.Single(e => Proto(e) == "IS14CyborgToolHypo");
            var synthSys = entMan.System<CyborgReagentSynthesizerSystem>();
            var synth = entMan.GetComponent<CyborgReagentSynthesizerComponent>(hypo);
            Assert.That(synthSys.GetAvailableReagents((hypo, synth)).Select(r => r.Id),
                Is.EquivalentTo(new[] { "Bicaridine", "Tricordrazine", "Kelotane", "Dexalin", "Saline", "Dylovene" }));
            synthSys.Select((hypo, synth), "Saline", null);
            Assert.That(synth.Selected?.Id, Is.EqualTo("Saline"));

            entMan.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The engineering cyborg holds sheets and tiles in its module slots, but nothing else,
    ///     and never in an offline slot. Emptying the slot drops what it held.
    /// </summary>
    [Test]
    public async Task EngineeringPicksUpMaterials()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var cyborgSys = entMan.System<SharedCyborgSystem>();
        var hands = entMan.System<SharedHandsSystem>();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity(EngineeringProto, map.GridCoords);
            var cyborg = entMan.GetComponent<CyborgComponent>(uid);
            cyborgSys.SetActive((uid, cyborg), true);

            var steel = entMan.SpawnEntity("SheetSteel", map.GridCoords);
            var carpet = entMan.SpawnEntity("FloorCarpetItemRed", map.GridCoords);
            var crowbar = entMan.SpawnEntity("Crowbar", map.GridCoords);

            Assert.Multiple(() =>
            {
                Assert.That(cyborgSys.CanPickUp(cyborg, steel), "steel is on the engineering list");
                Assert.That(cyborgSys.CanPickUp(cyborg, carpet), "carpets count as floor tiles");
                Assert.That(cyborgSys.CanPickUp(cyborg, crowbar), Is.False, "a crowbar is not a material");
                Assert.That(hands.TryPickupAnyHand(uid, crowbar), Is.False);
            });

            Assert.That(hands.TryPickupAnyHand(uid, steel), "the sheet goes into a free slot");
            Assert.That(hands.IsHolding(uid, steel, out _));

            cyborgSys.StoreAllTools((uid, cyborg));
            Assert.That(hands.IsHolding(uid, steel, out _), Is.False, "emptying the slots drops picked-up items");

            // With every slot offline nothing fits anywhere.
            cyborg.BrokenSlots = cyborg.ToolSlots;
            Assert.That(hands.TryPickupAnyHand(uid, carpet), Is.False, "offline slots hold nothing");

            entMan.DeleteEntity(uid);

            // The medical cyborg carries treatment and body bags instead, in slots or its medkit.
            var medical = entMan.SpawnEntity("IS14CyborgNTMedical", map.GridCoords);
            var medCyborg = entMan.GetComponent<CyborgComponent>(medical);
            var gauze = entMan.SpawnEntity("Gauze", map.GridCoords);
            var bodyBag = entMan.SpawnEntity("BodyBagFolded", map.GridCoords);
            Assert.Multiple(() =>
            {
                Assert.That(cyborgSys.CanPickUp(medCyborg, gauze), "gauze is on the medical list");
                Assert.That(cyborgSys.CanPickUp(medCyborg, bodyBag), "body bags are on the medical list");
                Assert.That(cyborgSys.CanPickUp(medCyborg, steel), Is.False, "steel is engineering's");
                Assert.That(medCyborg.ToolContainer.ContainedEntities.Any(e =>
                    entMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID == "IS14CyborgToolMedkit"));
            });
            entMan.DeleteEntity(medical);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The station's cyborg rechargers take the new chassis and charge its cell.
    /// </summary>
    [Test]
    public async Task RechargerChargesCyborg()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var powerCell = entMan.System<PowerCellSystem>();
        EntityUid uid = default;
        EntityUid charger = default;
        var rateBefore = 0f;

        await server.WaitAssertion(() =>
        {
            uid = entMan.SpawnEntity(EngineeringProto, map.GridCoords);
            charger = entMan.SpawnEntity("BorgCharger", map.GridCoords);
            entMan.GetComponent<ApcPowerReceiverComponent>(charger).NeedsPower = false;
            Assert.That(powerCell.TryGetBatteryFromSlot(uid, out var battery));
            rateBefore = battery!.Value.Comp.ChargeRate;
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.System<SharedEntityStorageSystem>().Insert(uid, charger), "the cyborg should fit");
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(powerCell.TryGetBatteryFromSlot(uid, out var battery));
            Assert.That(battery!.Value.Comp.ChargeRate, Is.GreaterThan(rateBefore),
                "the recharger should feed the cell");
            entMan.DeleteEntity(uid);
            entMan.DeleteEntity(charger);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Stowing a lit welder turns it off.
    /// </summary>
    [Test]
    public async Task StowedWelderTurnsOff()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var cyborgSys = entMan.System<SharedCyborgSystem>();
        var toggle = entMan.System<ItemToggleSystem>();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity(EngineeringProto, map.GridCoords);
            var cyborg = entMan.GetComponent<CyborgComponent>(uid);
            cyborgSys.SetActive((uid, cyborg), true);
            var welder = cyborg.ToolContainer.ContainedEntities
                .Single(e => entMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID == "IS14CyborgToolWelder");

            Assert.That(cyborgSys.TryDeployTool((uid, cyborg),
                (welder, entMan.GetComponent<CyborgToolComponent>(welder))));
            toggle.TrySetActive(welder, true, uid);
            Assert.That(toggle.IsActivated(welder), "the welder should light");

            cyborgSys.StoreTool((uid, cyborg), welder);
            Assert.Multiple(() =>
            {
                Assert.That(cyborg.ToolContainer.Contains(welder));
                Assert.That(toggle.IsActivated(welder), Is.False, "a stowed welder goes out");
            });

            entMan.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The service cyborg's table dock puts one table on the floor and folds it back.
    /// </summary>
    [Test]
    public async Task TableDockDeploysAndPacks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var cyborgSys = entMan.System<SharedCyborgSystem>();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity("IS14CyborgNTService", map.GridCoords);
            var cyborg = entMan.GetComponent<CyborgComponent>(uid);
            var user = entMan.SpawnEntity(null, map.GridCoords);

            entMan.System<LockSystem>().Unlock(uid, null);
            entMan.System<SharedWiresSystem>().TogglePanel(uid, entMan.GetComponent<WiresPanelComponent>(uid), true);
            var board = entMan.SpawnEntity("IS14CyborgUpgradeRollingTable", map.GridCoords);
            Assert.That(cyborgSys.TryInstallUpgrade((uid, cyborg),
                (board, entMan.GetComponent<CyborgUpgradeComponent>(board)), user));

            var dock = cyborg.ToolContainer.ContainedEntities
                .Single(e => entMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID == "IS14CyborgToolTableDock");
            var comp = entMan.GetComponent<CyborgBedDeployerComponent>(dock);

            entMan.EventBus.RaiseLocalEvent(dock, new AfterInteractEvent(uid, dock, null, map.GridCoords, true));
            Assert.That(comp.Deployed, Is.Not.Null, "clicking the floor sets up a table");
            var table = comp.Deployed!.Value;

            entMan.EventBus.RaiseLocalEvent(dock, new AfterInteractEvent(uid, dock, null, map.GridCoords, true));
            Assert.That(comp.Deployed, Is.EqualTo(table), "only one table at a time");

            entMan.EventBus.RaiseLocalEvent(dock, new AfterInteractEvent(uid, dock, table, map.GridCoords, true));
            Assert.That(comp.Deployed, Is.Null, "clicking the table folds it");

            entMan.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A station cyborg starts without a model and shows the default skin.
    /// </summary>
    [Test]
    public async Task NoModelCyborgIsEmpty()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();
        var cyborgSys = entMan.System<SharedCyborgSystem>();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity(NoModelProto, map.GridCoords);
            var cyborg = entMan.GetComponent<CyborgComponent>(uid);

            Assert.Multiple(() =>
            {
                Assert.That(cyborg.Model, Is.Null);
                Assert.That(cyborg.ToolContainer.ContainedEntities, Is.Empty);
                Assert.That(cyborgSys.GetSkin(cyborg)?.ID, Is.EqualTo(cyborg.DefaultSkin.Id));
                Assert.That(cyborg.Active, Is.False);
            });

            entMan.DeleteEntity(uid);
        });

        await pair.CleanReturnAsync();
    }
}
