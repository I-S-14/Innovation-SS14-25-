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
using Content.Shared._Shitmed.Medical.Surgery.Tools;
using Content.Shared.Stacks;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Content.Shared.Lock;
using Content.Shared.Wires;
using Content.Shared.Damage.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._IS14;

/// <summary>
///     Checks the core of the tgstation-style cyborgs: models, tool storage and tool slots.
/// </summary>
[TestFixture]
[TestOf(typeof(SharedCyborgSystem))]
public sealed class CyborgTest
{
    private const string NoModelProto = "CyborgNT";
    private const string EngineeringProto = "CyborgNTEngineering";
    private static readonly ProtoId<CyborgModelPrototype> EngineeringModel = "CyborgModelEngineering";

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
                        Assert.That(toolProto!.TryGetComponent<CyborgToolComponent>(out _, server.ResolveDependency<IComponentFactory>()),
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
                Assert.That(cyborg.ToolContainer.ContainedEntities, Has.Count.EqualTo(model.BasicTools.Count));
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
            var uid = entMan.SpawnEntity(EngineeringProto, map.GridCoords);
            var cyborg = entMan.GetComponent<CyborgComponent>(uid);

            sheets = cyborg.ToolContainer.ContainedEntities
                .Single(e => entMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID == "CyborgToolSheetSteel");
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
            Assert.That(cyborgSys.TryInstallUpgrade(ent, Board("CyborgUpgradeIonThrusters"), user), Is.False);

            entMan.System<LockSystem>().Unlock(uid, null);
            Assert.That(entMan.System<SharedWiresSystem>()
                .TogglePanel(uid, entMan.GetComponent<WiresPanelComponent>(uid), true), "cover should open");

            Assert.Multiple(() =>
            {
                Assert.That(cyborgSys.TryInstallUpgrade(ent, Board("CyborgUpgradeDiamondDrill"), user), Is.False,
                    "miner board must not fit an engineering cyborg");
                Assert.That(cyborgSys.TryInstallUpgrade(ent, Board("CyborgUpgradeAdvancedTools"), user));
                Assert.That(cyborg.ToolContainer.ContainedEntities, Has.Count.EqualTo(baseTools + 3));
                Assert.That(cyborgSys.TryInstallUpgrade(ent, Board("CyborgUpgradeAdvancedTools"), user), Is.False,
                    "duplicate board");
                Assert.That(cyborgSys.TryInstallUpgrade(ent, Board("CyborgUpgradeSelfRepair"), user));
                Assert.That(entMan.HasComponent<PassiveDamageComponent>(uid));
                Assert.That(cyborgSys.TryInstallUpgrade(ent, Board("CyborgUpgradeIllegal"), user));
                Assert.That(cyborg.EmagToolsUnlocked);
            });

            Assert.That(cyborgSys.TryInstallUpgrade(ent, Board("CyborgUpgradeReclassification"), user));

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

            var tools = entMan.SpawnEntity("CyborgUpgradeAdvancedTools", map.GridCoords);
            var thrusters = entMan.SpawnEntity("CyborgUpgradeIonThrusters", map.GridCoords);
            Assert.That(cyborgSys.TryInstallUpgrade(ent, (tools, entMan.GetComponent<CyborgUpgradeComponent>(tools)), user));
            Assert.That(cyborgSys.TryInstallUpgrade(ent, (thrusters, entMan.GetComponent<CyborgUpgradeComponent>(thrusters)), user));

            Assert.That(cyborgSys.TryRemoveUpgrade(ent, tools, user));
            Assert.That(cyborgSys.TryRemoveUpgrade(ent, thrusters, user));

            Assert.Multiple(() =>
            {
                Assert.That(cyborg.ToolContainer.ContainedEntities, Has.Count.EqualTo(baseTools), "board tools must go");
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

            Assert.That(entMan.System<SharedPuddleSystem>().TrySpillAt(next, new Solution("Blood", 20), out var puddle, sound: false));

            var uid = entMan.SpawnEntity("IS14CyborgNTJanitor", start);
            var cyborg = entMan.GetComponent<CyborgComponent>(uid);
            var wash = entMan.GetComponent<CyborgAutoWashComponent>(uid);
            wash.Enabled = true;

            var tank = cyborg.ToolContainer.ContainedEntities
                .Single(e => entMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID == "CyborgToolBucket");
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

            var omnitools = tools.Where(e => Proto(e) == "CyborgToolMedicalOmnitool").ToList();
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

            var hypo = tools.Single(e => Proto(e) == "CyborgToolHypo");
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
    ///     The roller bed deployer puts one bed on the floor and packs it back.
    /// </summary>
    [Test]
    public async Task RollerBedDeploysAndPacks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        var entMan = server.ResolveDependency<IEntityManager>();

        await server.WaitAssertion(() =>
        {
            var uid = entMan.SpawnEntity("IS14CyborgNTMedical", map.GridCoords);
            var cyborg = entMan.GetComponent<CyborgComponent>(uid);
            var deployer = cyborg.ToolContainer.ContainedEntities
                .Single(e => entMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID == "CyborgToolRollerBed");
            var comp = entMan.GetComponent<CyborgBedDeployerComponent>(deployer);

            entMan.EventBus.RaiseLocalEvent(deployer, new AfterInteractEvent(uid, deployer, null, map.GridCoords, true));
            Assert.That(comp.Deployed, Is.Not.Null, "clicking the floor deploys a bed");
            var bed = comp.Deployed!.Value;
            Assert.That(entMan.HasComponent<StrapComponent>(bed), "the deployed bed can hold a patient");

            entMan.EventBus.RaiseLocalEvent(deployer, new AfterInteractEvent(uid, deployer, null, map.GridCoords, true));
            Assert.That(comp.Deployed, Is.EqualTo(bed), "only one bed at a time");

            entMan.EventBus.RaiseLocalEvent(deployer, new AfterInteractEvent(uid, deployer, bed, map.GridCoords, true));
            Assert.That(comp.Deployed, Is.Null, "clicking the bed packs it");

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
