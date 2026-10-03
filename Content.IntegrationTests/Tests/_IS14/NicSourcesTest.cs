// Licensed under IS14's EULA, see EULA.txt for more information.

#nullable enable
using System.Linq;
using System.Numerics;
using Content.Server._IS14.Research;
using Content.Server.GameTicking;
using Content.Server.Power.Components;
using Content.Server.Research.Components;
using Content.Server.Research.Systems;
using Content.Shared._IS14.CCVar;
using Content.Shared._IS14.Research;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.CCVar;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Paper;
using Content.Shared.Interaction;
using Content.Shared.Research.Components;
using Content.Shared.Research.Prototypes;
using Content.Shared.Station.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._IS14;

/// <summary>
///     Where industrial data comes from now that the passive drip is gone: records the station
///     has to beat, and gas mixtures nobody has made before.
/// </summary>
[TestFixture]
[TestOf(typeof(IS14RecordSystem))]
public sealed class NicSourcesTest
{
    private const string AtmosBenchProto = "IS14AtmosBench";
    private const string TelemetryProto = "IS14EngineTelemetry";
    private const string TankProto = "PlasmaTank";
    private const string TrophyBenchProto = "IS14TrophyBench";
    private const string WeaponProto = "WeaponPistolMk58";
    private const string VolunteerScannerProto = "IS14VolunteerScanner";
    private const string ConsentFormProto = "IS14ConsentForm";
    private const string HumanProto = "MobHuman";
    private const string NicConsoleProto = "IS14NicConsole";
    private const string WorkOrderConsoleProto = "IS14WorkOrderConsoleEngineering";

    /// <summary>
    ///     Priced in scientific data and pays out in scientific data, while the engineering
    ///     terminal's bonus is industrial — so the assertion sees the bonus alone.
    /// </summary>
    private const string OrderedTech = "IS14Metrology";

    private const string PublicationReceiverProto = "IS14PublicationReceiver";
    private const string PublicationPaperProto = "IS14PublicationPaper";
    private const string PublishedTech = "IS14Metallurgy";
    private const string ReverseBenchProto = "IS14ReverseEngineeringBench";
    private const string ContrabandProto = "EnergySword";

    /// <summary>Classified until the First Department takes an energy sword apart.</summary>
    private const string SecretTech = "IS14SecretEnergetics";

    private static readonly ProtoId<ResearchPointTypePrototype> Industrial = "Industrial";
    private static readonly ProtoId<ResearchPointTypePrototype> Science = "Science";
    private static readonly ProtoId<ResearchPointTypePrototype> Military = "Military";

    /// <summary>
    ///     A record pays for being beaten, and by how much. Standing still pays nothing — which
    ///     is the whole point of measuring engineering by records instead of by uptime.
    /// </summary>
    [Test]
    public async Task RecordsPayForBeatingThemselves()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var records = server.System<IS14RecordSystem>();

        await server.WaitAssertion(() =>
        {
            var station = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            var first = records.TryRecord(station, "engine:peak", 100_000f);
            var worse = records.TryRecord(station, "engine:peak", 90_000f);
            var equal = records.TryRecord(station, "engine:peak", 100_000f);
            var scrape = records.TryRecord(station, "engine:peak", 101_000f);
            var doubled = records.TryRecord(station, "engine:peak", 300_000f);

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.EqualTo(IS14RecordSystem.FirstRecordShare),
                    "the first figure of a kind has nothing to beat and still has to pay");
                Assert.That(worse, Is.Zero, "below the record is not a result");
                Assert.That(equal, Is.Zero, "matching the record is not beating it");
                Assert.That(scrape, Is.GreaterThan(0f).And.LessThan(0.2f),
                    "scraping past by a percent pays a scrap");
                Assert.That(doubled, Is.EqualTo(1f).Within(0.001f), "a full result is a full payout");
                Assert.That(records.Peak(station, "engine:peak"), Is.EqualTo(300_000f));
                Assert.That(records.Peak(station, "atmos:temperature"), Is.Null);
            });

            entMan.DeleteEntity(station);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A mixture is profiled by its composition, so the same recipe twice is the same
    ///     measurement and a different ratio is a different one.
    /// </summary>
    [Test]
    public async Task GasMixtureIsItsComposition()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var gas = server.System<IS14GasSampleSystem>();

        await server.WaitAssertion(() =>
        {
            var plasmaOxygen = new GasMixture(70f);
            plasmaOxygen.AdjustMoles(Gas.Plasma, 60f);
            plasmaOxygen.AdjustMoles(Gas.Oxygen, 40f);

            var sameRatio = new GasMixture(70f);
            sameRatio.AdjustMoles(Gas.Plasma, 30f);
            sameRatio.AdjustMoles(Gas.Oxygen, 20f);

            var otherRatio = new GasMixture(70f);
            otherRatio.AdjustMoles(Gas.Plasma, 20f);
            otherRatio.AdjustMoles(Gas.Oxygen, 80f);

            var tritium = new GasMixture(70f);
            tritium.AdjustMoles(Gas.Tritium, 60f);
            tritium.AdjustMoles(Gas.Oxygen, 40f);

            Assert.Multiple(() =>
            {
                Assert.That(gas.Composition(sameRatio), Is.EqualTo(gas.Composition(plasmaOxygen)),
                    "half as much of the same mix is the same mix");
                Assert.That(gas.Composition(otherRatio), Is.Not.EqualTo(gas.Composition(plasmaOxygen)),
                    "a different ratio is a different measurement");
                Assert.That(gas.Composition(tritium), Is.Not.EqualTo(gas.Composition(plasmaOxygen)),
                    "another fuel is another measurement");
                Assert.That(gas.Composition(plasmaOxygen), Does.Contain("plasma"));
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The upstream passive income is gone: the research server's own trickle reports zero
    ///     while the cvar is on, and comes back when it is off.
    /// </summary>
    [Test]
    public async Task PassiveIncomeIsCut()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = true, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();

        server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
        server.CfgMan.SetCVar(CCVars.GameMap, "Saltern");

        await server.WaitPost(() => ticker.RestartRound());
        await pair.RunTicksSync(25);

        var research = server.System<ResearchSystem>();

        EntityUid researchServer = default;

        await server.WaitPost(() =>
        {
            researchServer = entMan.AllComponentsList<ResearchServerComponent>().Single().Uid;

            // An unpowered server reports no income at all, whatever its sources say, so the
            // test powers it by hand instead of waiting for the station's APCs.
            if (entMan.TryGetComponent<ApcPowerReceiverComponent>(researchServer, out var receiver))
            {
                receiver.NeedsPower = false;
                receiver.Powered = true;
            }

            // Its own source, built out of components rather than spawned from a prototype:
            // with no power receiver at all the drip is unconditional, so the test measures
            // the suppression and not whether the station's APCs have settled.
            var source = entMan.SpawnEntity(null, MapCoordinates.Nullspace);

            entMan.AddComponent<ResearchClientComponent>(source);

            var drip = entMan.AddComponent<ResearchPointSourceComponent>(source);
            drip.Active = true;
            drip.PointsPerSecond = 25;

            research.RegisterClient(source, researchServer);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            Assert.That(research.GetPointsPerSecond(researchServer), Is.Zero,
                "nothing may tick research points on its own");

            server.CfgMan.SetCVar(IS14CVars.ResearchCutUpstreamPassive, false);

            // At least our own source; the station's research server carries a drip of its
            // own on top, which is exactly what the cvar exists to switch off.
            Assert.That(research.GetPointsPerSecond(researchServer), Is.GreaterThanOrEqualTo(25),
                "the drip is still there in the prototypes — we only suppress it");

            server.CfgMan.SetCVar(IS14CVars.ResearchCutUpstreamPassive, true);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The atmospheric bench end to end: a tank of gas is a sample, it pays industrial
    ///     data, the tank is handed back, and the same tank again pays less.
    /// </summary>
    [Test]
    public async Task AtmosBenchMeasuresTheMixture()
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
        var interaction = server.System<SharedInteractionSystem>();

        EntityUid bench = default;
        EntityUid tank = default;
        EntityUid station = default;
        EntityUid user = default;
        EntityCoordinates coordinates = default;

        await server.WaitPost(() =>
        {
            station = entMan.AllComponentsList<StationDataComponent>().Single().Uid;
            var grid = entMan.AllComponentsList<StationMemberComponent>().First().Uid;
            coordinates = new EntityCoordinates(grid, Vector2.Zero);

            bench = entMan.SpawnEntity(AtmosBenchProto, coordinates);
            tank = entMan.SpawnEntity(TankProto, coordinates);
            user = entMan.SpawnEntity(null, coordinates);

            entMan.GetComponent<ApcPowerReceiverComponent>(bench).NeedsPower = false;

            // Tanks spawn with an empty mixture; the bench measures gas, so it needs some.
            var air = entMan.GetComponent<GasTankComponent>(tank).Air;
            air.AdjustMoles(Gas.Plasma, 30f);
            air.AdjustMoles(Gas.Oxygen, 20f);
        });

        await pair.RunTicksSync(5);

        var first = 0;

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.HasComponent<GasTankComponent>(tank), Is.True,
                "the sample has to be an actual tank of gas");

            var before = points.GetPoints(station, Industrial);

            interaction.InteractUsing(user, tank, bench, coordinates, false, false);

            first = points.GetPoints(station, Industrial) - before;

            Assert.Multiple(() =>
            {
                Assert.That(first, Is.GreaterThan(0), "a mixture nobody has measured is worth data");
                Assert.That(entMan.Deleted(tank), Is.False, "the bench hands the container back");
            });
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            // The click path is proven above. The repeat goes straight through the measuring
            // core, because what is being checked here is the novelty rule on gas mixtures and
            // not the bench's cooldown.
            var experiment = server.System<IS14ExperimentSystem>();
            var benchComp = entMan.GetComponent<IS14ExperimentBenchComponent>(bench);

            var repeat = experiment.Measure(
                bench,
                tank,
                benchComp.PointType,
                benchComp.BaseValue,
                benchComp.Mode,
                benchComp.ProfileNamespace);

            Assert.Multiple(() =>
            {
                Assert.That(repeat.Refused, Is.False);
                Assert.That(repeat.PriorCount, Is.EqualTo(1), "the archive remembers the mixture");
                Assert.That(repeat.Payout, Is.GreaterThan(0).And.LessThan(first),
                    "the same mixture again is a confirmation, not a discovery");
            });

            entMan.DeleteEntity(bench);
            entMan.DeleteEntity(tank);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     An instrument that can go wrong does not simply waste the sample: a failure is a
    ///     negative result — a smaller payout, a lost sample, and a safer repeat.
    /// </summary>
    [Test]
    public async Task RiskTurnsFailureIntoANegativeResult()
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
            var bench = entMan.SpawnEntity(TrophyBenchProto, MapCoordinates.Nullspace);
            var benchComp = entMan.GetComponent<IS14ExperimentBenchComponent>(bench);

            var risk = entMan.GetComponent<IS14ExperimentRiskComponent>(bench);
            risk.Safety = null;
            risk.Risk = 1f;

            var failed = experiment.Measure(
                bench,
                entMan.SpawnEntity(WeaponProto, MapCoordinates.Nullspace),
                benchComp.PointType,
                benchComp.BaseValue,
                benchComp.Mode,
                benchComp.ProfileNamespace);

            var failureKey = IS14ExperimentSystem.FailureKey(failed.ProfileKey);

            Assert.Multiple(() =>
            {
                Assert.That(failed.Failed, Is.True, "a certainty has to fail");
                Assert.That(failed.Payout, Is.LessThan(benchComp.BaseValue),
                    "a negative result is worth a fraction of the real one");
                Assert.That(failed.Payout, Is.GreaterThan(0), "a negative result is still a result");
                Assert.That(failed.PreventConsume, Is.False, "the sample is lost with the experiment");
                Assert.That(points.PeekMeasurement(station, failureKey), Is.EqualTo(1),
                    "the failure is remembered, which is what makes the repeat safer");
            });

            risk.Risk = 0f;

            var repeat = experiment.Measure(
                bench,
                entMan.SpawnEntity(WeaponProto, MapCoordinates.Nullspace),
                benchComp.PointType,
                benchComp.BaseValue,
                benchComp.Mode,
                benchComp.ProfileNamespace);

            Assert.Multiple(() =>
            {
                Assert.That(repeat.Failed, Is.False);
                Assert.That(repeat.Payout, Is.GreaterThan(failed.Payout),
                    "the same measurement done properly beats the failed one");
            });

            entMan.DeleteEntity(bench);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A living subject is worth proper data only with a signed form. Without one the
    ///     examination still happens — that path has to exist — but the data is poor.
    /// </summary>
    [Test]
    public async Task VolunteerNeedsConsentToBeWorthMuch()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { DummyTicker = true, Dirty = true });
        var server = pair.Server;
        var entMan = server.EntMan;
        var ticker = server.System<GameTicker>();

        server.CfgMan.SetCVar(CCVars.GameDummyTicker, false);
        server.CfgMan.SetCVar(CCVars.GameMap, "Saltern");

        await server.WaitPost(() => ticker.RestartRound());
        await pair.RunTicksSync(25);

        var volunteers = server.System<IS14VolunteerSystem>();
        var hands = server.System<SharedHandsSystem>();

        await server.WaitAssertion(() =>
        {
            var grid = entMan.AllComponentsList<StationMemberComponent>().First().Uid;
            var coordinates = new EntityCoordinates(grid, Vector2.Zero);

            var scanner = entMan.SpawnEntity(VolunteerScannerProto, coordinates);
            var scannerEnt = new Entity<IS14VolunteerScannerComponent>(
                scanner,
                entMan.GetComponent<IS14VolunteerScannerComponent>(scanner));

            var scientist = entMan.SpawnEntity(HumanProto, coordinates);
            var unwilling = entMan.SpawnEntity(HumanProto, coordinates);
            var volunteer = entMan.SpawnEntity(HumanProto, coordinates);

            var form = entMan.SpawnEntity(ConsentFormProto, coordinates);
            var formEnt = new Entity<IS14ConsentFormComponent>(
                form,
                entMan.GetComponent<IS14ConsentFormComponent>(form));

            volunteers.Sign(formEnt, volunteer);
            hands.TryPickupAnyHand(volunteer, form);

            Assert.Multiple(() =>
            {
                Assert.That(volunteers.HasConsent(volunteer, volunteers.SubjectKey(volunteer)), Is.True,
                    "the volunteer is carrying their own signed form");
                Assert.That(volunteers.HasConsent(volunteer, volunteers.SubjectKey(unwilling)), Is.False,
                    "a form signed by one person does not cover another");
            });

            var consented = volunteers.Examine(scannerEnt, scientist, volunteer);
            var forced = volunteers.Examine(scannerEnt, scientist, unwilling);

            Assert.Multiple(() =>
            {
                Assert.That(consented.Refused, Is.False);
                Assert.That(forced.Refused, Is.False, "the unconsented path has to remain possible");
                Assert.That(forced.Payout, Is.LessThan(consented.Payout),
                    "an unwilling subject gives worse data");
            });

            entMan.DeleteEntity(scanner);
            entMan.DeleteEntity(scientist);
            entMan.DeleteEntity(unwilling);
            entMan.DeleteEntity(volunteer);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A department's order is a two-way trade: when the NIC delivers the topic, the
    ///     department's data and its credits move, and the order closes itself.
    /// </summary>
    [Test]
    public async Task WorkOrderPaysOnDelivery()
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
        var orders = server.System<IS14WorkOrderSystem>();
        var protoMan = server.ProtoMan;

        EntityUid console = default;
        EntityUid terminal = default;

        await server.WaitPost(() =>
        {
            var researchServer = entMan.AllComponentsList<ResearchServerComponent>().Single().Uid;
            var coordinates = entMan.GetComponent<TransformComponent>(researchServer).Coordinates;

            console = entMan.SpawnEntity(NicConsoleProto, coordinates);
            terminal = entMan.SpawnEntity(WorkOrderConsoleProto, coordinates);
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            var station = entMan.AllComponentsList<StationDataComponent>().Single().Uid;
            var department = entMan.GetComponent<IS14WorkOrderConsoleComponent>(terminal);
            var tech = protoMan.Index<TechnologyPrototype>(OrderedTech);

            // The head has signed for it. Placing the order through the terminal's UI is the
            // player's half; what matters here is what delivery does.
            entMan.EnsureComponent<IS14WorkOrdersComponent>(station).Orders[department.Department] = tech.ID;

            Assert.That(orders.GetOrder(station, department.Department), Is.EqualTo(tech.ID));

            foreach (var (type, amount) in costs.GetCost(tech))
            {
                points.AddPoints(station, type, amount);
            }

            var before = points.GetPoints(station, department.PointType);

            var consoleEnt = new Entity<IS14ResearchConsoleComponent>(
                console,
                entMan.GetComponent<IS14ResearchConsoleComponent>(console));

            Assert.That(consoleSystem.TryUnlock(consoleEnt, null, tech, out _), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(orders.GetOrder(station, department.Department), Is.Null,
                    "a delivered order closes itself");
                Assert.That(points.GetPoints(station, department.PointType),
                    Is.EqualTo(before + department.Bonus),
                    "the department pays its bonus in its own kind of data, and nothing else "
                    + "touches that currency here");
            });

            entMan.DeleteEntity(console);
            entMan.DeleteEntity(terminal);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A publication is a result signed off by the people who answer for it: unstamped it
    ///     is refused, stamped it pays the station in credits and data, and only once.
    /// </summary>
    [Test]
    public async Task PublicationNeedsBothStamps()
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
        var publications = server.System<IS14PublicationSystem>();

        await server.WaitAssertion(() =>
        {
            var station = entMan.AllComponentsList<StationDataComponent>().Single().Uid;
            var grid = entMan.AllComponentsList<StationMemberComponent>().First().Uid;
            var coordinates = new EntityCoordinates(grid, Vector2.Zero);

            var receiver = entMan.SpawnEntity(PublicationReceiverProto, coordinates);
            var receiverEnt = new Entity<IS14PublicationReceiverComponent>(
                receiver,
                entMan.GetComponent<IS14PublicationReceiverComponent>(receiver));

            entMan.GetComponent<ApcPowerReceiverComponent>(receiver).NeedsPower = false;

            var paper = entMan.SpawnEntity(PublicationPaperProto, coordinates);
            var paperComp = entMan.EnsureComponent<IS14PublicationComponent>(paper);
            paperComp.TechnologyId = PublishedTech;

            var before = points.GetPoints(station, Science);

            Assert.That(publications.File(receiverEnt, (paper, paperComp), null), Is.False,
                "an unstamped paper is just paper");

            // Both signatures, in the order the bureaucracy expects them.
            var stamps = entMan.GetComponent<PaperComponent>(paper);
            foreach (var required in receiverEnt.Comp.RequiredStamps)
            {
                stamps.StampedBy.Add(new StampDisplayInfo { StampedName = required });
            }

            Assert.That(publications.File(receiverEnt, (paper, paperComp), null), Is.True,
                "stamped by the research director and the captain, it is a result");

            Assert.Multiple(() =>
            {
                Assert.That(points.GetPoints(station, Science), Is.GreaterThan(before),
                    "the Academy pays in data as well as credits");
                Assert.That(paperComp.Filed, Is.True);
            });

            var second = entMan.SpawnEntity(PublicationPaperProto, coordinates);
            var secondComp = entMan.EnsureComponent<IS14PublicationComponent>(second);
            secondComp.TechnologyId = PublishedTech;

            var secondStamps = entMan.GetComponent<PaperComponent>(second);
            foreach (var required in receiverEnt.Comp.RequiredStamps)
            {
                secondStamps.StampedBy.Add(new StampDisplayInfo { StampedName = required });
            }

            Assert.That(publications.File(receiverEnt, (second, secondComp), null), Is.False,
                "one result, one paper — a copy is not a second discovery");

            entMan.DeleteEntity(receiver);
            entMan.DeleteEntity(second);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A classified topic does not exist until security hands the trophy over: the console
    ///     refuses it, the bench uncovers it, and only then can it be bought like anything else.
    /// </summary>
    [Test]
    public async Task ContrabandUncoversAHiddenTopic()
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
        var reveal = server.System<IS14ResearchRevealSystem>();
        var reverse = server.System<IS14ReverseEngineeringSystem>();
        var protoMan = server.ProtoMan;

        EntityUid console = default;
        EntityUid bench = default;
        EntityUid station = default;
        var before = 0;

        await server.WaitPost(() =>
        {
            var researchServer = entMan.AllComponentsList<ResearchServerComponent>().Single().Uid;
            var coordinates = entMan.GetComponent<TransformComponent>(researchServer).Coordinates;

            console = entMan.SpawnEntity(NicConsoleProto, coordinates);
            bench = entMan.SpawnEntity(ReverseBenchProto, coordinates);

            entMan.GetComponent<ApcPowerReceiverComponent>(bench).NeedsPower = false;
        });

        await pair.RunTicksSync(5);

        await server.WaitAssertion(() =>
        {
            station = entMan.AllComponentsList<StationDataComponent>().Single().Uid;

            var tech = protoMan.Index<TechnologyPrototype>(SecretTech);
            var consoleEnt = new Entity<IS14ResearchConsoleComponent>(
                console,
                entMan.GetComponent<IS14ResearchConsoleComponent>(console));

            foreach (var type in costs.SortedTypes())
            {
                points.AddPoints(station, type.ID, 1000);
            }

            Assert.Multiple(() =>
            {
                Assert.That(tech.Hidden, Is.True, "a classified topic is not on the map");
                Assert.That(reveal.IsRevealed(station, SecretTech), Is.False);
                Assert.That(consoleSystem.TryUnlock(consoleEnt, null, tech, out _), Is.False,
                    "data alone cannot buy what the station does not know exists");
            });

            before = points.GetPoints(station, Military);

            var benchEnt = new Entity<IS14ReverseEngineeringBenchComponent>(
                bench,
                entMan.GetComponent<IS14ReverseEngineeringBenchComponent>(bench));

            var junk = entMan.SpawnEntity(WeaponProto, entMan.GetComponent<TransformComponent>(bench).Coordinates);

            Assert.That(reverse.TryReverseEngineer(benchEnt, junk, null), Is.False,
                "station hardware teaches the First Department nothing");

            var trophy = entMan.SpawnEntity(ContrabandProto, entMan.GetComponent<TransformComponent>(bench).Coordinates);

            Assert.That(reverse.TryReverseEngineer(benchEnt, trophy, null), Is.True,
                "enemy hardware does");
        });

        // The cycle is the work; the reveal lands when it ends.
        await pair.RunTicksSync(500);

        await server.WaitAssertion(() =>
        {
            var tech = protoMan.Index<TechnologyPrototype>(SecretTech);
            var consoleEnt = new Entity<IS14ResearchConsoleComponent>(
                console,
                entMan.GetComponent<IS14ResearchConsoleComponent>(console));

            Assert.Multiple(() =>
            {
                Assert.That(reveal.IsRevealed(station, SecretTech), Is.True,
                    "the trophy uncovered the topic");
                Assert.That(points.GetPoints(station, Military), Is.GreaterThan(before),
                    "taking it apart is worth military data on its own");
            });

            Assert.That(consoleSystem.TryUnlock(consoleEnt, null, tech, out _), Is.True,
                "uncovered, it is bought with data like anything else");

            entMan.DeleteEntity(console);
            entMan.DeleteEntity(bench);
        });

        await pair.CleanReturnAsync();
    }
}
