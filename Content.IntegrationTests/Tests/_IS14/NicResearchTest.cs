// Licensed under IS14's EULA, see EULA.txt for more information.

#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Server._IS14.Research;
using Content.Server.GameTicking;
using Content.Server.Power.Components;
using Content.Server.Station.Systems;
using Content.Shared._IS14.CCVar;
using Content.Shared._IS14.Explosion;
using Content.Shared._IS14.Research;
using Content.Shared.CCVar;
using Content.Shared.Interaction;
using Content.Shared.Lathe;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Maths;
using Robust.Shared.Random;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._IS14;

/// <summary>
///     The NIC's five currencies: the ledger, the novelty rule that stops repetition from
///     paying, the multi-currency price table, and buying a technology with it.
/// </summary>
[TestFixture]
[TestOf(typeof(IS14ResearchPointSystem))]
public sealed class NicResearchTest
{
    private const string AnalyzerProto = "IS14UniversalAnalyzer";
    private const string ConsoleProto = "IS14NicConsole";
    private const string SampleProto = "Crowbar";
    private const string OtherSampleProto = "Wrench";

    /// <summary>A tier-1 arsenal technology with no prerequisites: 20 military data.</summary>
    private const string CheapTech = "SalvageWeapons";

    /// <summary>Gateway of the materials branch: no prerequisites, cheap, unlocks recipes.</summary>
    private const string GatewayTech = "IS14Metallurgy";

    /// <summary>
    ///     One variant of the randomised lathe-tuning slot: hidden, has to be revealed, and its
    ///     whole reward is a station modifier (-20% material use).
    /// </summary>
    private const string ModifierTech = "IS14TuningThrift";

    /// <summary>IS14 ballistics: no prerequisites, single currency, easy to price-check.</summary>
    private const string DiscountTech = "IS14Ballistics";

    private const string DopplerProto = "IS14DopplerArray";

    private const string AnalyzerMachineProto = "IS14DestructiveAnalyzer";

    /// <summary>A breakthrough: no amount of data buys it, only a bluespace crystal does.</summary>
    private const string BreakthroughTech = "IS14BluespaceTheory";

    private const string BreakthroughPrereq = "IS14SubspaceMath";

    /// <summary>Centre of the fundamental branch: every ray starts from it.</summary>
    private const string BranchCentre = "IS14Metrology";

    private const string BreakthroughSample = "MaterialBSCrystal";

    /// <summary>Two halves of one fork: researching either closes the other.</summary>
    private const string ForkTech = "IS14GosplanGrant";

    private const string ForkAlternative = "IS14PlannedFunding";

    private static readonly ProtoId<ResearchPointTypePrototype> Science = "Science";
    private static readonly ProtoId<ResearchPointTypePrototype> Military = "Military";

    /// <summary>
    ///     Balances are per station and per currency: spending checks every currency in the
    ///     price and takes nothing at all when one of them is short.
    /// </summary>
    [Test]
    public async Task LedgerSpendsAllOrNothing()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var points = server.System<IS14ResearchPointSystem>();

        await server.WaitAssertion(() =>
        {
            // Balances hang off whatever entity owns them, so a bare entity stands in for a station.
            var station = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            points.AddPoints(station, Science, 40);
            points.AddPoints(station, Military, 10);

            var price = new Dictionary<ProtoId<ResearchPointTypePrototype>, int>
            {
                [Science] = 30,
                [Military] = 25,
            };

            Assert.Multiple(() =>
            {
                Assert.That(points.GetPoints(station, Science), Is.EqualTo(40));
                Assert.That(points.CanAfford(station, price), Is.False, "military data is short");
                Assert.That(points.TrySpend(station, price), Is.False);
                Assert.That(points.GetPoints(station, Science), Is.EqualTo(40),
                    "a refused purchase must not take the currency it could cover");
            });

            points.AddPoints(station, Military, 20);

            Assert.Multiple(() =>
            {
                Assert.That(points.TrySpend(station, price), Is.True);
                Assert.That(points.GetPoints(station, Science), Is.EqualTo(10));
                Assert.That(points.GetPoints(station, Military), Is.EqualTo(5));
            });

            entMan.DeleteEntity(station);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The side table prices technologies in several currencies, and technologies it does
    ///     not mention still have a price — upstream additions must never become free.
    /// </summary>
    [Test]
    public async Task PriceTableIsMultiCurrency()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ProtoMan;
        var costs = server.System<IS14ResearchCostSystem>();

        await server.WaitAssertion(() =>
        {
            // Robotics is the showcase: a machine shop and a theory, neither alone.
            var robotics = protoMan.Index<TechnologyPrototype>("BasicRobotics");
            var roboticsCost = costs.GetCost(robotics);

            Assert.Multiple(() =>
            {
                Assert.That(roboticsCost, Has.Count.GreaterThanOrEqualTo(2));
                Assert.That(roboticsCost.ContainsKey("Industrial"), Is.True);
                Assert.That(roboticsCost.ContainsKey("Science"), Is.True);
            });

            // Every tier-3 technology has to need at least two departments.
            foreach (var tech in protoMan.EnumeratePrototypes<TechnologyPrototype>())
            {
                if (tech.Tier < 3 || tech.Hidden)
                    continue;

                Assert.That(costs.GetCost(tech), Has.Count.GreaterThanOrEqualTo(2),
                    $"{tech.ID} is tier 3 and must cost more than one currency");
            }

            // Fallback: anything missing from the table is charged as scientific data.
            var unlisted = new TechnologyPrototype();
            Assert.That(costs.GetCost(unlisted), Is.Not.Empty);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The archive is the anti-grind rule: the same sample pays a fraction the second
    ///     time, while a genuinely different sample pays in full.
    /// </summary>
    [Test]
    public async Task NoveltyDecaysOnRepeatedSamples()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = true, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();

        server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
        server.CfgMan.SetCVar(CCVars.GameMap, "Saltern");

        await server.WaitPost(() => ticker.RestartRound());
        await pair.RunTicksSync(25);

        var points = server.System<IS14ResearchPointSystem>();
        var experiment = server.System<IS14ExperimentSystem>();

        await server.WaitAssertion(() =>
        {
            var station = entMan.AllComponentsList<StationDataComponent>().Single().Uid;

            // The shift opens with a grant, so the department has a decision from minute one.
            Assert.That(points.GetPoints(station, Science),
                Is.EqualTo(server.CfgMan.GetCVar(IS14CVars.ResearchStartupGrant)));

            var analyzer = entMan.SpawnEntity(AnalyzerProto, MapCoordinates.Nullspace);
            var bench = entMan.GetComponent<IS14ExperimentBenchComponent>(analyzer);

            var before = points.GetPoints(station, Science);

            var first = Measure(experiment, analyzer, bench, entMan.SpawnEntity(SampleProto, MapCoordinates.Nullspace));
            var repeat = Measure(experiment, analyzer, bench, entMan.SpawnEntity(SampleProto, MapCoordinates.Nullspace));
            var different = Measure(experiment, analyzer, bench, entMan.SpawnEntity(OtherSampleProto, MapCoordinates.Nullspace));

            Assert.Multiple(() =>
            {
                Assert.That(first.Refused, Is.False);
                Assert.That(first.Payout, Is.EqualTo(bench.BaseValue), "a new profile pays the full rate");
                Assert.That(first.PriorCount, Is.Zero);

                Assert.That(repeat.PriorCount, Is.EqualTo(1));
                Assert.That(repeat.Payout, Is.LessThan(first.Payout),
                    "the same sample again must pay noticeably less");

                Assert.That(different.PriorCount, Is.Zero, "another prototype is another profile");
                Assert.That(different.Payout, Is.EqualTo(first.Payout));

                Assert.That(points.GetPoints(station, Science),
                    Is.EqualTo(before + first.Payout + repeat.Payout + different.Payout),
                    "every payout lands in the station's ledger");
            });

            entMan.DeleteEntity(analyzer);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The console buys a technology with IS14 currencies and files it with the upstream
    ///     research server, which is what keeps lathes working unchanged.
    /// </summary>
    [Test]
    public async Task ConsoleBuysTechnologyWithData()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = true, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();

        server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
        server.CfgMan.SetCVar(CCVars.GameMap, "Saltern");

        await server.WaitPost(() => ticker.RestartRound());
        await pair.RunTicksSync(25);

        var points = server.System<IS14ResearchPointSystem>();
        var costs = server.System<IS14ResearchCostSystem>();
        var consoleSystem = server.System<IS14ResearchConsoleSystem>();
        var protoMan = server.ProtoMan;

        EntityUid console = default;
        EntityUid researchServer = default;

        // Spawned next to the map's research server so the console registers with it on init.
        await server.WaitPost(() =>
        {
            researchServer = entMan.AllComponentsList<ResearchServerComponent>().Single().Uid;
            console = entMan.SpawnEntity(ConsoleProto, entMan.GetComponent<TransformComponent>(researchServer).Coordinates);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var station = entMan.AllComponentsList<StationDataComponent>().Single().Uid;
            var tech = protoMan.Index<TechnologyPrototype>(CheapTech);
            var price = costs.GetCost(tech);
            var consoleEnt = new Entity<IS14ResearchConsoleComponent>(
                console,
                entMan.GetComponent<IS14ResearchConsoleComponent>(console));

            Assert.That(entMan.GetComponent<ResearchClientComponent>(console).Server, Is.EqualTo(researchServer),
                "the NIC console has to be an ordinary research client");

            // Nothing to pay with yet.
            Assert.That(consoleSystem.TryUnlock(consoleEnt, null, tech, out var reason), Is.False);
            Assert.That(reason, Is.EqualTo("is14-research-console-cannot-afford"));

            foreach (var (type, amount) in price)
            {
                points.AddPoints(station, type, amount);
            }

            Assert.That(consoleSystem.TryUnlock(consoleEnt, null, tech, out _), Is.True);

            var database = entMan.GetComponent<TechnologyDatabaseComponent>(researchServer);

            Assert.Multiple(() =>
            {
                Assert.That(new List<ProtoId<TechnologyPrototype>>(database.UnlockedTechnologies),
                    Does.Contain(new ProtoId<TechnologyPrototype>(CheapTech)),
                    "the unlock has to land in the upstream research server");

                foreach (var (type, _) in price)
                {
                    Assert.That(points.GetPoints(station, type), Is.Zero, "the price is deducted in full");
                }

                Assert.That(consoleSystem.TryUnlock(consoleEnt, null, tech, out _), Is.False,
                    "already researched");
            });

            entMan.DeleteEntity(console);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A technology whose only reward is a station modifier still changes something real:
    ///     every lathe on the station is retuned when it lands.
    /// </summary>
    [Test]
    public async Task ModifierEffectRetunesLathes()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = true, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();

        server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
        server.CfgMan.SetCVar(CCVars.GameMap, "Saltern");

        await server.WaitPost(() => ticker.RestartRound());
        await pair.RunTicksSync(25);

        var points = server.System<IS14ResearchPointSystem>();
        var costs = server.System<IS14ResearchCostSystem>();
        var modifiers = server.System<IS14ResearchModifierSystem>();
        var consoleSystem = server.System<IS14ResearchConsoleSystem>();
        var reveal = server.System<IS14ResearchRevealSystem>();
        var protoMan = server.ProtoMan;

        EntityUid console = default;
        EntityUid researchServer = default;

        await server.WaitPost(() =>
        {
            researchServer = entMan.AllComponentsList<ResearchServerComponent>().Single().Uid;
            console = entMan.SpawnEntity(ConsoleProto, entMan.GetComponent<TransformComponent>(researchServer).Coordinates);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var station = entMan.AllComponentsList<StationDataComponent>().Single().Uid;
            var gateway = protoMan.Index<TechnologyPrototype>(GatewayTech);
            var tech = protoMan.Index<TechnologyPrototype>(ModifierTech);
            var consoleEnt = new Entity<IS14ResearchConsoleComponent>(
                console,
                entMan.GetComponent<IS14ResearchConsoleComponent>(console));

            Assert.That(modifiers.GetModifier(station, "LatheMaterialEfficiency"), Is.EqualTo(1f),
                "nothing researched yet, so the station prints at prototype cost");

            // The upgrade is hidden: it can only be bought once this station has rolled it.
            Assert.That(consoleSystem.TryUnlock(consoleEnt, null, tech, out _), Is.False,
                "an unrevealed variant must not be buyable at all");

            reveal.Reveal(station, ModifierTech);

            foreach (var (type, amount) in costs.GetCost(gateway))
            {
                points.AddPoints(station, type, amount);
            }

            Assert.That(consoleSystem.TryUnlock(consoleEnt, null, gateway, out _), Is.True,
                "the upgrade sits behind the branch's gateway topic");

            foreach (var (type, amount) in costs.GetCost(tech))
            {
                points.AddPoints(station, type, amount);
            }

            Assert.That(consoleSystem.TryUnlock(consoleEnt, null, tech, out _), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(modifiers.GetModifier(station, "LatheMaterialEfficiency"), Is.EqualTo(0.8f).Within(0.001f),
                    "the thrifty tuning has to move the station's material efficiency");

                var stationSystem = server.System<StationSystem>();

                var lathe = entMan.AllComponentsList<LatheComponent>()
                    .FirstOrDefault(pair => stationSystem.GetOwningStation(pair.Uid) == station);

                Assert.That(lathe.Component, Is.Not.Null, "the station should have a lathe to retune");
                Assert.That(lathe.Component!.MaterialUseMultiplier, Is.EqualTo(0.8f).Within(0.001f));
            });

            entMan.DeleteEntity(console);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Reverse-engineering credit is real money off: the console charges the discounted
    ///     price and the credit is spent along with it.
    /// </summary>
    [Test]
    public async Task ReverseEngineeringDiscountsPurchase()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = true, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();

        server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
        server.CfgMan.SetCVar(CCVars.GameMap, "Saltern");

        await server.WaitPost(() => ticker.RestartRound());
        await pair.RunTicksSync(25);

        var points = server.System<IS14ResearchPointSystem>();
        var costs = server.System<IS14ResearchCostSystem>();
        var consoleSystem = server.System<IS14ResearchConsoleSystem>();
        var protoMan = server.ProtoMan;

        EntityUid console = default;

        await server.WaitPost(() =>
        {
            var researchServer = entMan.AllComponentsList<ResearchServerComponent>().Single().Uid;
            console = entMan.SpawnEntity(ConsoleProto, entMan.GetComponent<TransformComponent>(researchServer).Coordinates);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var station = entMan.AllComponentsList<StationDataComponent>().Single().Uid;
            var tech = protoMan.Index<TechnologyPrototype>(DiscountTech);
            var fullPrice = costs.GetCost(tech);
            var consoleEnt = new Entity<IS14ResearchConsoleComponent>(
                console,
                entMan.GetComponent<IS14ResearchConsoleComponent>(console));

            // Half the price already paid for in samples taken apart.
            var discounts = entMan.EnsureComponent<IS14ResearchDiscountComponent>(station);
            discounts.Discounts[DiscountTech] = 0.5f;

            var discounted = IS14ResearchCostSystem.Discounted(fullPrice, 0.5f);

            foreach (var (type, amount) in discounted)
            {
                points.AddPoints(station, type, amount);
            }

            Assert.Multiple(() =>
            {
                Assert.That(discounted.Values.Sum(), Is.LessThan(fullPrice.Values.Sum()));
                Assert.That(consoleSystem.TryUnlock(consoleEnt, null, tech, out _), Is.True,
                    "the discounted price is all the console may charge");
                Assert.That(discounts.Discounts.ContainsKey(DiscountTech), Is.False,
                    "the credit is spent with the purchase");
            });

            entMan.DeleteEntity(console);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Every randomised upgrade slot has to offer exactly one of its variants per round —
    ///     the whole point is that the alternatives cannot be stacked.
    /// </summary>
    [Test]
    public async Task UpgradeSlotsRollExactlyOneVariant()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = true, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();

        server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
        server.CfgMan.SetCVar(CCVars.GameMap, "Saltern");

        await server.WaitPost(() => ticker.RestartRound());
        await pair.RunTicksSync(25);

        var reveal = server.System<IS14ResearchRevealSystem>();
        var protoMan = server.ProtoMan;

        await server.WaitAssertion(() =>
        {
            var station = entMan.AllComponentsList<StationDataComponent>().Single().Uid;
            var groups = protoMan.EnumeratePrototypes<IS14ResearchUpgradeGroupPrototype>().ToList();

            Assert.That(groups, Is.Not.Empty, "there should be upgrade slots to roll");

            foreach (var group in groups)
            {
                var revealed = group.Variants.Count(variant => reveal.IsRevealed(station, variant.Id));

                Assert.That(revealed, Is.EqualTo(1),
                    $"slot {group.ID} must offer exactly one of its {group.Variants.Count} variants");

                foreach (var variant in group.Variants)
                {
                    Assert.That(protoMan.Index<TechnologyPrototype>(variant.Id).Hidden, Is.True,
                        $"{variant.Id} has to be hidden, or every variant would show up at once");
                }
            }

            // Rolling again keeps the invariant: one variant, never two.
            reveal.RollUpgrades(station);

            foreach (var group in groups)
            {
                Assert.That(group.Variants.Count(variant => reveal.IsRevealed(station, variant.Id)),
                    Is.EqualTo(1), $"slot {group.ID} after a re-roll");
            }
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The doppler array is directional and pays for records: a blast behind it is invisible,
    ///     the first blast in front of it sets the record, and the same blast again pays a fraction.
    /// </summary>
    [Test]
    public async Task DopplerArrayPaysForRecords()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = true, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();

        server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
        server.CfgMan.SetCVar(CCVars.GameMap, "Saltern");

        await server.WaitPost(() => ticker.RestartRound());
        await pair.RunTicksSync(25);

        var points = server.System<IS14ResearchPointSystem>();
        var xform = server.System<SharedTransformSystem>();

        EntityUid array = default;
        EntityUid station = default;
        MapCoordinates arrayPosition = default;
        Vector2 facing = default;

        await server.WaitPost(() =>
        {
            station = entMan.AllComponentsList<StationDataComponent>().Single().Uid;

            // Put the array on the station's own grid so it resolves the right station.
            var anchor = entMan.AllComponentsList<ResearchServerComponent>().Single().Uid;
            array = entMan.SpawnEntity(DopplerProto, entMan.GetComponent<TransformComponent>(anchor).Coordinates);

            // The test is about the sensor, not about the station's wiring: an array dropped in
            // by hand is not plugged into an APC, and unpowered it would measure nothing.
            entMan.GetComponent<ApcPowerReceiverComponent>(array).NeedsPower = false;
            arrayPosition = xform.GetMapCoordinates(array);

            // Where the array actually looks, rather than where a hand-set angle should have put
            // it: the test is about the sensor cone, not about rotation bookkeeping.
            facing = xform.GetWorldRotation(array).ToWorldVec();
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<IS14DopplerArrayComponent>(array);
            var before = points.GetPoints(station, Military);

            Assert.That(entMan.GetComponent<ApcPowerReceiverComponent>(array).Powered, Is.True,
                "an unpowered array measures nothing, which would make the rest of this meaningless");

            // Behind the array: nothing is measured at all.
            Blast(entMan, arrayPosition, -facing * 30f, 120f);

            Assert.That(comp.Records, Is.Empty, "a blast behind the array must not be seen");
            Assert.That(points.GetPoints(station, Military), Is.EqualTo(before));

            // In front of it: the first reading sets the station record.
            Blast(entMan, arrayPosition, facing * 30f, 120f);

            Assert.That(comp.Records, Has.Count.EqualTo(1));

            var first = comp.Records[0];
            var afterFirst = points.GetPoints(station, Military);

            Assert.Multiple(() =>
            {
                Assert.That(first.Record, Is.True, "the first blast of a kind sets the station record");
                Assert.That(first.Payout, Is.GreaterThan(0));
                Assert.That(first.ShockwaveRadius, Is.GreaterThan(0f), "radii are estimated from the blast");
                Assert.That(afterFirst, Is.EqualTo(before + first.Payout));
            });

            // The same charge again: a confirmation, worth a fraction.
            Blast(entMan, arrayPosition, facing * 30f, 120f);

            var second = comp.Records[1];

            Assert.Multiple(() =>
            {
                Assert.That(second.Record, Is.False, "matching the record is not beating it");
                Assert.That(second.Payout, Is.LessThan(first.Payout));
            });

            entMan.DeleteEntity(array);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>Fires a blast at an offset from the array.</summary>
    /// <summary>
    ///     The tree checks its own design rules at runtime, not only while being generated:
    ///     every topic is priced, nothing above tier two is payable in a single currency, and
    ///     no two topics of a branch sit on the same square of the map.
    /// </summary>
    [Test]
    public async Task TreeObeysItsOwnRules()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var protoMan = server.ProtoMan;
        var costs = server.System<IS14ResearchCostSystem>();

        await server.WaitAssertion(() =>
        {
            var ours = protoMan.EnumeratePrototypes<TechnologyPrototype>()
                .Where(tech => tech.Discipline.Id.StartsWith("IS14"))
                .ToList();

            var squares = new Dictionary<string, string>();
            var hidden = ours.Where(tech => tech.Hidden).Select(tech => tech.ID).ToHashSet();

            Assert.That(ours, Has.Count.GreaterThan(120),
                "the tree is supposed to be big enough that nobody researches all of it");

            Assert.Multiple(() =>
            {
                foreach (var tech in ours)
                {
                    var data = costs.GetData(tech.ID);

                    Assert.That(data, Is.Not.Null, $"{tech.ID} has no IS14 price");
                    Assert.That(data!.Costs, Is.Not.Empty, $"{tech.ID} is free");

                    if (tech.Tier >= 3)
                    {
                        Assert.That(data.Costs, Has.Count.GreaterThan(1),
                            $"{tech.ID} is tier {tech.Tier} and has to need a partner department");
                    }

                    foreach (var prereq in tech.TechnologyPrerequisites)
                    {
                        Assert.That(protoMan.HasIndex<TechnologyPrototype>(prereq),
                            $"{tech.ID} requires missing {prereq}");
                        Assert.That(hidden, Does.Not.Contain(prereq.Id),
                            $"{tech.ID} depends on randomised {prereq} — the tree would change shape per round");
                    }

                    if (data.Breakthrough is { } breakthrough)
                    {
                        Assert.That(breakthrough.Samples, Is.Not.Empty, $"{tech.ID} wants no sample");

                        // Samples may be named by an abstract parent — "any anomaly core" —
                        // which is not indexed at runtime. What has to hold is that something
                        // in the game actually satisfies the requirement.
                        var obtainable = protoMan.EnumeratePrototypes<EntityPrototype>()
                            .Any(candidate =>
                                IS14PrototypeKin.MatchesAny(protoMan, candidate.ID, breakthrough.Samples));

                        Assert.That(obtainable, Is.True,
                            $"{tech.ID} wants a sample nothing in the game satisfies");

                        if (breakthrough.Icon is { } icon)
                        {
                            Assert.That(protoMan.HasIndex<EntityPrototype>(icon),
                                $"{tech.ID} draws a missing sample icon {icon}");
                        }
                    }

                    // Variants of one slot share a square on purpose; everything else must not.
                    if (tech.Hidden)
                        continue;

                    var square = $"{tech.Discipline.Id}:{tech.Position.X},{tech.Position.Y}";

                    Assert.That(squares.ContainsKey(square), Is.False,
                        $"{tech.ID} and {(squares.TryGetValue(square, out var other) ? other : "?")} overlap on the map");

                    squares[square] = tech.ID;
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A breakthrough is paid for with an object, not with data: the console refuses it
    ///     with every balance full, and takes it the moment the sample has been destroyed.
    /// </summary>
    [Test]
    public async Task BreakthroughCannotBeBoughtWithData()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = true, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();

        server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
        server.CfgMan.SetCVar(CCVars.GameMap, "Saltern");

        await server.WaitPost(() => ticker.RestartRound());
        await pair.RunTicksSync(25);

        var points = server.System<IS14ResearchPointSystem>();
        var costs = server.System<IS14ResearchCostSystem>();
        var consoleSystem = server.System<IS14ResearchConsoleSystem>();
        var breakthroughs = server.System<IS14BreakthroughSystem>();
        var protoMan = server.ProtoMan;

        EntityUid console = default;

        await server.WaitPost(() =>
        {
            var researchServer = entMan.AllComponentsList<ResearchServerComponent>().Single().Uid;
            console = entMan.SpawnEntity(ConsoleProto, entMan.GetComponent<TransformComponent>(researchServer).Coordinates);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var station = entMan.AllComponentsList<StationDataComponent>().Single().Uid;
            var consoleEnt = new Entity<IS14ResearchConsoleComponent>(
                console,
                entMan.GetComponent<IS14ResearchConsoleComponent>(console));

            var centre = protoMan.Index<TechnologyPrototype>(BranchCentre);
            var gate = protoMan.Index<TechnologyPrototype>(BreakthroughPrereq);
            var tech = protoMan.Index<TechnologyPrototype>(BreakthroughTech);

            // Enough of everything to buy the whole branch twice over.
            foreach (var type in costs.SortedTypes())
            {
                points.AddPoints(station, type.ID, 1000);
            }

            Assert.Multiple(() =>
            {
                Assert.That(consoleSystem.TryUnlock(consoleEnt, null, centre, out _), Is.True,
                    "a branch is entered through its centre");
                Assert.That(consoleSystem.TryUnlock(consoleEnt, null, gate, out _), Is.True,
                    "the way up to the breakthrough is ordinary research");
            });

            Assert.Multiple(() =>
            {
                Assert.That(costs.GetBreakthrough(BreakthroughTech), Is.Not.Null);
                Assert.That(consoleSystem.TryUnlock(consoleEnt, null, tech, out var reason), Is.False,
                    "data must never buy a breakthrough");
                Assert.That(reason, Is.EqualTo("is14-research-console-needs-sample"));
            });

            breakthroughs.Complete(station, BreakthroughTech);

            Assert.That(consoleSystem.TryUnlock(consoleEnt, null, tech, out _), Is.True,
                "with the sample destroyed it is an ordinary purchase");

            entMan.DeleteEntity(console);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The sample for the object: the crystal goes into the destructive analyzer, the
    ///     analyzer gives the station the breakthrough and the crystal is gone for good.
    /// </summary>
    [Test]
    public async Task AnalyzerTradesTheSampleForTheBreakthrough()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = true, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();

        server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
        server.CfgMan.SetCVar(CCVars.GameMap, "Saltern");

        await server.WaitPost(() => ticker.RestartRound());
        await pair.RunTicksSync(25);

        var points = server.System<IS14ResearchPointSystem>();
        var breakthroughs = server.System<IS14BreakthroughSystem>();
        var interaction = server.System<SharedInteractionSystem>();

        EntityUid analyzer = default;
        EntityUid sample = default;
        EntityUid station = default;
        var before = 0;

        await server.WaitPost(() =>
        {
            station = entMan.AllComponentsList<StationDataComponent>().Single().Uid;
            var grid = entMan.AllComponentsList<StationMemberComponent>().First().Uid;
            var coordinates = new EntityCoordinates(grid, Vector2.Zero);

            analyzer = entMan.SpawnEntity(AnalyzerMachineProto, coordinates);
            sample = entMan.SpawnEntity(BreakthroughSample, coordinates);

            // Spawned machines are not wired into the station's power.
            entMan.GetComponent<ApcPowerReceiverComponent>(analyzer).NeedsPower = false;

            before = points.GetPoints(station, Science);

            var analyzerSystem = server.System<IS14DestructiveAnalyzerSystem>();
            var ent = new Entity<IS14DestructiveAnalyzerComponent>(
                analyzer,
                entMan.GetComponent<IS14DestructiveAnalyzerComponent>(analyzer));

            Assert.That(analyzerSystem.TryAnalyze(ent, sample, null), Is.True,
                "the analyzer has to take a breakthrough sample");
        });

        // The cycle is the analysis: the object is taken at once, the data lands when it ends.
        await pair.RunTicksSync(400);

        await server.WaitAssertion(() =>
        {
            Assert.Multiple(() =>
            {
                Assert.That(entMan.Deleted(sample), Is.True, "a destructive analysis destroys the sample");
                Assert.That(breakthroughs.IsComplete(station, BreakthroughTech), Is.True,
                    "the crystal is what opens the bluespace branch");
                Assert.That(points.GetPoints(station, Science), Is.GreaterThan(before),
                    "the teardown pays data as well as opening the way");
            });

            entMan.DeleteEntity(analyzer);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A fork is a decision, not a shopping list: taking one road closes the other for the
    ///     rest of the shift, whatever the balances say.
    /// </summary>
    [Test]
    public async Task ForkClosesTheOtherRoad()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = true, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();

        server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
        server.CfgMan.SetCVar(CCVars.GameMap, "Saltern");

        await server.WaitPost(() => ticker.RestartRound());
        await pair.RunTicksSync(25);

        var points = server.System<IS14ResearchPointSystem>();
        var costs = server.System<IS14ResearchCostSystem>();
        var consoleSystem = server.System<IS14ResearchConsoleSystem>();
        var protoMan = server.ProtoMan;

        EntityUid console = default;

        await server.WaitPost(() =>
        {
            var researchServer = entMan.AllComponentsList<ResearchServerComponent>().Single().Uid;
            console = entMan.SpawnEntity(ConsoleProto, entMan.GetComponent<TransformComponent>(researchServer).Coordinates);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var station = entMan.AllComponentsList<StationDataComponent>().Single().Uid;
            var consoleEnt = new Entity<IS14ResearchConsoleComponent>(
                console,
                entMan.GetComponent<IS14ResearchConsoleComponent>(console));

            var centre = protoMan.Index<TechnologyPrototype>(BranchCentre);
            var chosen = protoMan.Index<TechnologyPrototype>(ForkTech);
            var closed = protoMan.Index<TechnologyPrototype>(ForkAlternative);

            foreach (var type in costs.SortedTypes())
            {
                points.AddPoints(station, type.ID, 1000);
            }

            Assert.That(consoleSystem.TryUnlock(consoleEnt, null, centre, out _), Is.True,
                "both halves of the fork hang off the branch centre");

            Assert.Multiple(() =>
            {
                Assert.That(costs.GetExclusiveSiblings(ForkTech),
                    Does.Contain(ForkAlternative), "the two have to know about each other");
                Assert.That(costs.GetExclusiveSiblings(ForkAlternative), Does.Contain(ForkTech));
            });

            Assert.That(consoleSystem.TryUnlock(consoleEnt, null, chosen, out _), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(consoleSystem.TryUnlock(consoleEnt, null, closed, out var reason), Is.False,
                    "the alternative is closed by the choice, not by the price");
                Assert.That(reason, Is.EqualTo("is14-research-console-excluded"));
            });

            entMan.DeleteEntity(console);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Every shift Gosplan declares a few topics a priority, which is the one thing about
    ///     the map that changes between rounds: the structure stays, the prices move.
    /// </summary>
    [Test]
    public async Task PrioritiesDiscountSomeTopics()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = true, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();

        server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
        server.CfgMan.SetCVar(CCVars.GameMap, "Saltern");

        await server.WaitPost(() => ticker.RestartRound());
        await pair.RunTicksSync(25);

        var costs = server.System<IS14ResearchCostSystem>();
        var consoleSystem = server.System<IS14ResearchConsoleSystem>();
        var reveal = server.System<IS14ResearchRevealSystem>();
        var protoMan = server.ProtoMan;

        await server.WaitAssertion(() =>
        {
            var station = entMan.AllComponentsList<StationDataComponent>().Single().Uid;
            var expected = server.CfgMan.GetCVar(IS14CVars.ResearchPriorityCount);

            reveal.RollPriorities(station);

            var priorities = entMan.GetComponent<IS14ResearchPrioritiesComponent>(station).Priorities;

            Assert.That(priorities, Has.Count.EqualTo(expected), "the shift gets exactly its quota");

            Assert.Multiple(() =>
            {
                foreach (var (technologyId, discount) in priorities)
                {
                    var tech = protoMan.Index<TechnologyPrototype>(technologyId);

                    Assert.That(tech.Hidden, Is.False, "a randomised variant is already a roll");
                    Assert.That(tech.Tier, Is.GreaterThan(1),
                        "a discount on a starter topic is not a decision");
                    Assert.That(discount, Is.GreaterThan(0f));

                    var full = costs.GetCost(tech);
                    var charged = IS14ResearchCostSystem.Discounted(
                        full,
                        consoleSystem.GetDiscount(station, technologyId));

                    Assert.That(charged.Values.Sum(), Is.LessThan(full.Values.Sum()),
                        $"{technologyId} is a priority and has to cost less");
                }
            });
        });

        await pair.CleanReturnAsync();
    }

    private static void Blast(IEntityManager entMan, MapCoordinates arrayPosition, Vector2 offset, float intensity)
    {
        var epicenter = new MapCoordinates(arrayPosition.Position + offset, arrayPosition.MapId);
        entMan.EventBus.RaiseEvent(EventSource.Local,
            new IS14ExplosionQueuedEvent(epicenter, "Default", intensity, 5f, 100f, null));
    }

    private static IS14MeasurementResult Measure(
        IS14ExperimentSystem experiment,
        EntityUid device,
        IS14ExperimentBenchComponent bench,
        EntityUid sample)
    {
        return experiment.Measure(
            device,
            sample,
            bench.PointType,
            bench.BaseValue,
            bench.Mode,
            bench.ProfileNamespace);
    }
}
