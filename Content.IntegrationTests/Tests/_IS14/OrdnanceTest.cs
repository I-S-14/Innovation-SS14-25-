// Licensed under IS14's EULA, see EULA.txt for more information.

#nullable enable
using Content.Client.Atmos.EntitySystems;
using Content.IntegrationTests.Pair;
using Content.Server._IS14.Ordnance;
using Content.Shared._IS14.Ordnance;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Trigger.Components;
using Content.Shared.SubFloor;
using Content.Shared.Trigger.Systems;
using Robust.Client.GameObjects;
using Robust.Shared;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._IS14;

/// <summary>
///     The transfer valve: an ordinary manual valve that becomes an ordnance assembly once it is
///     unwrenched. What it accepts, when it accepts it, and that the bomb is the mixture rather than
///     the device — cold gas merely equalises, hot gas takes the assembly with it.
/// </summary>
[TestFixture]
[TestOf(typeof(IS14TransferValveSystem))]
public sealed class OrdnanceTest
{
    private const string ValveProto = "GasValve";
    private const string OxygenProto = "OxygenTankFilled";
    private const string PlasmaProto = "PlasmaTankFilled";
    private const string PocketProto = "EmergencyOxygenTankFilled";
    private const string TimerProto = "IS14OrdnanceTimer";

    /// <summary>
    ///     A valve in a pipe is just a valve; off the pipe it takes two tanks. With slot swapping
    ///     left on, the second tank knocked the first one out into the player's hands instead of
    ///     filling the empty slot, so the valve never had a pair.
    /// </summary>
    [Test]
    public async Task OnlyTheUnwrenchedValveTakesTanks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var slots = server.System<ItemSlotsSystem>();
        var transform = server.System<SharedTransformSystem>();

        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var valve = entMan.SpawnEntity(ValveProto, map.GridCoords);
            var comp = entMan.GetComponent<IS14TransferValveComponent>(valve);
            var slotsComp = entMan.GetComponent<ItemSlotsComponent>(valve);

            // A user with hands, because this is the path that was broken: the interaction one,
            // which is allowed to swap a filled slot's contents out into the hands of whoever clicked.
            var user = entMan.SpawnEntity("MobHuman", map.GridCoords);
            var tank = entMan.SpawnEntity(OxygenProto, map.GridCoords);

            Assert.That(slots.TryGetSlot(valve, comp.ReceiverSlot, out var receiverSlot, slotsComp), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(entMan.GetComponent<TransformComponent>(valve).Anchored, Is.True,
                    "a valve spawns wrenched into place, which is the state the station sees");
                Assert.That(slots.CanInsert(valve, tank, user, receiverSlot!), Is.False,
                    "a valve in a pipe is plumbing, not ordnance");
            });

            transform.Unanchor(valve, entMan.GetComponent<TransformComponent>(valve));

            foreach (var proto in new[] { OxygenProto, PlasmaProto })
            {
                var fitted = entMan.SpawnEntity(proto, map.GridCoords);
                slots.TryInsertWithConditions(valve, slotsComp, user, fitted, doAfter: false);
            }

            Assert.Multiple(() =>
            {
                Assert.That(slots.GetItemOrNull(valve, comp.ReceiverSlot), Is.Not.Null,
                    "the first tank stays put when the second one is fitted");
                Assert.That(slots.GetItemOrNull(valve, comp.DonorSlot), Is.Not.Null,
                    "the second tank fills the empty slot instead of swapping the first one out");
            });

            // Pocket tanks hold too little gas to be ordnance, and the valve says so.
            var pocket = entMan.SpawnEntity(PocketProto, map.GridCoords);

            Assert.That(slots.CanInsert(valve, pocket, user, receiverSlot!, swap: true), Is.False,
                "only a full-size oxygen tank and a plasma tank belong in a valve");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     No timer part, no timing. Fitting one gives the valve its countdown and the delays the
    ///     ordnance crew picks from; pulling it back out takes both away again.
    /// </summary>
    [Test]
    public async Task TimingComesFromTheFittedPart()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var slots = server.System<ItemSlotsSystem>();
        var transform = server.System<SharedTransformSystem>();

        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var valve = entMan.SpawnEntity(ValveProto, map.GridCoords);
            var comp = entMan.GetComponent<IS14TransferValveComponent>(valve);

            transform.Unanchor(valve, entMan.GetComponent<TransformComponent>(valve));

            Assert.That(entMan.HasComponent<TimerTriggerComponent>(valve), Is.False,
                "a bare valve has no countdown to speak of");

            var part = entMan.SpawnEntity(TimerProto, map.GridCoords);

            Assert.That(slots.TryInsert(valve, comp.TriggerSlot, part, null), Is.True);
            Assert.That(entMan.TryGetComponent<TimerTriggerComponent>(valve, out var timer), Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(timer!.Delay, Is.EqualTo(TimeSpan.FromSeconds(10)));
                Assert.That(timer.DelayOptions,
                    Is.EquivalentTo(new[]
                    {
                        TimeSpan.FromSeconds(5),
                        TimeSpan.FromSeconds(10),
                        TimeSpan.FromSeconds(15),
                        TimeSpan.FromSeconds(30),
                    }));
                Assert.That(timer.BeepSound, Is.Null, "and it does it quietly");
                Assert.That(timer.Examinable, Is.False, "the valve gives nothing away on examine");
            });

            Assert.That(slots.TryGetSlot(valve, comp.TriggerSlot, out var slot), Is.True);
            Assert.That(slots.TryEject(valve, slot!, null, out _), Is.True);

            Assert.That(entMan.HasComponent<TimerTriggerComponent>(valve), Is.False,
                "taking the part back out takes the countdown with it");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A loaded valve stops looking like plumbing: the pipe layers go, the assembly's own icon
    ///     comes up, and it says how many tanks are on it. Asserted on the client's sprite, because
    ///     three other systems write to those same layers and only the client shows who won.
    /// </summary>
    [Test]
    public async Task AssembledValveLooksLikeAnAssembly()
    {
        // A connected client, because this is the one test that asks what the player sees.
        await using var pair = await PoolManager.GetServerClient(
            new PoolSettings { Connected = true, Dirty = true });
        var server = pair.Server;
        var client = pair.Client;

        // Nobody is standing next to the test map, so without this the client never hears about
        // the valve at all and there is no sprite to look at.
        server.CfgMan.SetCVar(CVars.NetPVS, false);

        var map = await pair.CreateTestMap();

        EntityUid valve = default;
        EntityUid donor = default;

        await server.WaitPost(() =>
        {
            (valve, _, donor) = Assemble(pair, map.GridCoords, fillTemperature: null, open: false, timer: true);
        });

        await pair.RunTicksSync(15);

        var clientValve = pair.ToClientUid(valve);

        await client.WaitAssertion(() =>
        {
            var sprite = client.EntMan.GetComponent<SpriteComponent>(clientValve);
            var sprites = client.System<SpriteSystem>();
            var entity = (clientValve, sprite);

            Assert.That(sprites.LayerMapTryGet(entity, IS14TransferValveVisualLayers.Assembly, out var assembly, false),
                Is.True);
            Assert.That(sprites.LayerMapTryGet(entity, PipeVisualLayers.Pipe, out var pipe, false), Is.True);
            Assert.That(sprites.LayerMapTryGet(entity, SubfloorLayers.FirstLayer, out var valveLayer, false), Is.True);
            Assert.That(sprites.LayerMapTryGet(entity, IS14TransferValveVisualLayers.Trigger, out var wiring, false),
                Is.True);

            Assert.Multiple(() =>
            {
                Assert.That(sprite[assembly].Visible, Is.True, "the assembly's own icon is what you see");
                Assert.That(sprite[assembly].RsiState.Name, Is.EqualTo("valve_1"), "and with both tanks on, that icon");
                Assert.That(sprite[pipe].Visible, Is.False, "the pipe under it is gone");
                Assert.That(sprite[valveLayer].Visible, Is.False);
                Assert.That(sprite[wiring].Visible, Is.True, "the timer's cabling shows it is armed");
            });
        });

        // Pull one tank back off: half an assembly is drawn as the bare valve, not as two tanks.
        await server.WaitPost(() =>
        {
            var slots = server.System<ItemSlotsSystem>();
            var comp = server.EntMan.GetComponent<IS14TransferValveComponent>(valve);

            Assert.That(slots.TryGetSlot(valve, comp.DonorSlot, out var slot), Is.True);
            Assert.That(slots.TryEject(valve, slot!, null, out _), Is.True);
        });

        await pair.RunTicksSync(15);

        await client.WaitAssertion(() =>
        {
            var sprite = client.EntMan.GetComponent<SpriteComponent>(clientValve);
            var sprites = client.System<SpriteSystem>();
            var entity = (clientValve, sprite);

            Assert.That(sprites.LayerMapTryGet(entity, IS14TransferValveVisualLayers.Assembly, out var assembly, false),
                Is.True);

            Assert.That(sprite[assembly].RsiState.Name, Is.EqualTo("valve"));
        });

        Assert.That(server.EntMan.Deleted(donor), Is.False);

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A valve that has already merged a pair of tanks is spent, but only until the tanks
    ///     change: swapping in fresh ones makes it a working valve again rather than scrap.
    /// </summary>
    [Test]
    public async Task SpentValveTakesFreshTanks()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var slots = server.System<ItemSlotsSystem>();

        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            // A cold pair: merging it is a damp squib, which is exactly the case that used to
            // leave the valve refusing every tank afterwards.
            var (valve, _, _) = Assemble(pair, map.GridCoords, fillTemperature: null);
            var comp = entMan.GetComponent<IS14TransferValveComponent>(valve);

            Assert.That(comp.Open, Is.True, "the pair has been merged");
            Assert.That(slots.TryGetSlot(valve, comp.DonorSlot, out var slot), Is.True);
            Assert.That(slots.TryEject(valve, slot!, null, out _), Is.True);

            Assert.That(comp.Open, Is.False, "taking the spent tank off makes it a valve again");

            var fresh = entMan.SpawnEntity(PlasmaProto, map.GridCoords);

            Assert.That(slots.TryInsert(valve, comp.DonorSlot, fresh, null), Is.True,
                "and it takes a fresh tank");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Two cold tanks make no bomb: the valve doubles the pressure of a five-litre tank to
    ///     around twenty atmospheres, and cold plasma has no reason to burn.
    /// </summary>
    [Test]
    public async Task ColdMixOnlyEqualises()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var map = await pair.CreateTestMap();

        EntityUid receiver = default;

        await server.WaitPost(() =>
        {
            var (_, oxygen, _) = Assemble(pair, map.GridCoords, fillTemperature: null);
            receiver = oxygen;
        });

        // Long enough for several of the half-second tank updates, each of which reacts the
        // mixture and checks the tank against its own pressure limits.
        await pair.RunTicksSync(60);

        await server.WaitAssertion(() =>
        {
            Assert.That(entMan.Deleted(receiver), Is.False, "a cold mix has no energy to burn");

            var air = entMan.GetComponent<GasTankComponent>(receiver).Air;

            Assert.Multiple(() =>
            {
                Assert.That(air.Pressure, Is.GreaterThan(15f * Atmospherics.OneAtmosphere),
                    "the donor tank's gas did arrive");
                Assert.That(air.Pressure, Is.LessThan(30f * Atmospherics.OneAtmosphere),
                    "and it is nowhere near the leak threshold");
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Hot gas is the bomb. Two tanks filled at the pressure a canister can manage, but hot
    ///     enough to burn, take the assembly with them.
    /// </summary>
    [Test]
    public async Task HotMixDetonates()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var map = await pair.CreateTestMap();

        EntityUid valve = default;

        await server.WaitPost(() =>
        {
            // 1200 K at ten atmospheres is a canister filled off a hot loop: the department can
            // make this, and it is well past the point where plasma starts burning.
            (valve, _, _) = Assemble(pair, map.GridCoords, fillTemperature: 1200f);
        });

        await pair.RunTicksSync(10);

        Assert.That(entMan.Deleted(valve), Is.True, "the assembly is consumed by its own blast");

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     With a timer fitted it is the countdown that opens the valve, so the blast lands at the
    ///     end of it rather than the moment somebody touched the thing.
    /// </summary>
    [Test]
    public async Task TimerOpensTheValve()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var trigger = server.System<TriggerSystem>();

        var map = await pair.CreateTestMap();

        EntityUid valve = default;

        await server.WaitPost(() =>
        {
            (valve, _, _) = Assemble(pair, map.GridCoords, fillTemperature: 1200f, open: false, timer: true);

            // One second instead of the usual ten, so the test waits a second rather than ten.
            var timer = entMan.GetComponent<TimerTriggerComponent>(valve);
            timer.Delay = TimeSpan.FromSeconds(1);

            Assert.That(trigger.ActivateTimerTrigger((valve, timer)), Is.True);
        });

        await pair.RunTicksSync(15);

        Assert.That(entMan.Deleted(valve), Is.False, "the countdown has not run out yet");

        await pair.RunTicksSync(120);

        Assert.That(entMan.Deleted(valve), Is.True, "and when it does, the valve opens");

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     What the yield is made of: oxygen limits the burn, heat decides how much of it happens,
    ///     and tritium is worth an order of magnitude more than plasma.
    /// </summary>
    [Test]
    public async Task YieldRewardsHeatAndTritium()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;
        var valves = server.System<IS14TransferValveSystem>();

        var map = await pair.CreateTestMap();

        await server.WaitAssertion(() =>
        {
            var comp = entMan.GetComponent<IS14TransferValveComponent>(
                entMan.SpawnEntity(ValveProto, map.GridCoords));

            var cold = valves.GetYield(comp, Mix(Gas.Plasma, 0.4f, 0.6f, 293.15f));
            var warm = valves.GetYield(comp, Mix(Gas.Plasma, 0.4f, 0.6f, 700f));
            var hot = valves.GetYield(comp, Mix(Gas.Plasma, 0.4f, 0.6f, 1400f));
            var starved = valves.GetYield(comp, Mix(Gas.Plasma, 0.4f, 0f, 1400f));
            var tritium = valves.GetYield(comp, Mix(Gas.Tritium, 0.4f, 0.6f, 1400f));

            Assert.Multiple(() =>
            {
                Assert.That(cold.Intensity, Is.Zero, "cold plasma does not burn");
                Assert.That(starved.Intensity, Is.Zero, "without an oxidiser there is no blast");
                Assert.That(warm.Intensity, Is.GreaterThan(0f));
                Assert.That(hot.Intensity, Is.GreaterThan(warm.Intensity), "heat buys a fuller burn");
                Assert.That(tritium.Energy, Is.GreaterThan(5f * hot.Energy),
                    "tritium is the premium fuel, and worth the atmospherics work");
                Assert.That(tritium.Intensity, Is.EqualTo(comp.MaxIntensity),
                    "and the best mixes run into the ceiling rather than off the scale");
            });
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>A bare mixture: so much fuel, so much oxygen, at that temperature.</summary>
    private static GasMixture Mix(Gas fuel, float fuelMoles, float oxygenMoles, float temperature)
    {
        var mixture = new GasMixture(5f) { Temperature = temperature };
        mixture.SetMoles(fuel, fuelMoles);
        mixture.SetMoles(Gas.Oxygen, oxygenMoles);
        return mixture;
    }

    /// <summary>
    ///     Unwrenches a valve, hangs two tanks on it and (unless told otherwise) opens it. Returns
    ///     the valve and both tanks; the oxygen tank is the receiving one, so it is the one holding
    ///     the mixture afterwards.
    /// </summary>
    private static (EntityUid Valve, EntityUid Oxygen, EntityUid Plasma) Assemble(
        TestPair pair,
        EntityCoordinates where,
        float? fillTemperature,
        bool open = true,
        bool timer = false)
    {
        var entMan = pair.Server.EntMan;
        var slots = pair.Server.System<ItemSlotsSystem>();
        var valves = pair.Server.System<IS14TransferValveSystem>();
        var transform = pair.Server.System<SharedTransformSystem>();

        var valve = entMan.SpawnEntity(ValveProto, where);
        var comp = entMan.GetComponent<IS14TransferValveComponent>(valve);

        transform.Unanchor(valve, entMan.GetComponent<TransformComponent>(valve));

        var oxygen = entMan.SpawnEntity(OxygenProto, where);
        var plasma = entMan.SpawnEntity(PlasmaProto, where);

        Assert.Multiple(() =>
        {
            Assert.That(slots.TryInsert(valve, comp.ReceiverSlot, oxygen, null), Is.True);
            Assert.That(slots.TryInsert(valve, comp.DonorSlot, plasma, null), Is.True);
        });

        if (timer)
        {
            Assert.That(slots.TryInsert(valve, comp.TriggerSlot, entMan.SpawnEntity(TimerProto, where), null),
                Is.True);
        }

        if (fillTemperature is { } temperature)
        {
            Refill(entMan, oxygen, Gas.Oxygen, temperature);
            Refill(entMan, plasma, Gas.Plasma, temperature);
        }

        if (open)
            Assert.That(valves.TryOpen((valve, comp)), Is.True);

        return (valve, oxygen, plasma);
    }

    /// <summary>
    ///     Refills a tank the way a canister does: up to the ten atmospheres a canister will
    ///     release, and no further, so hot gas means fewer moles rather than a free overfill.
    /// </summary>
    private static void Refill(IEntityManager entMan, EntityUid tank, Gas gas, float temperature)
    {
        var air = entMan.GetComponent<GasTankComponent>(tank).Air;

        air.Clear();
        air.Temperature = temperature;
        air.SetMoles(gas, 10f * Atmospherics.OneAtmosphere * air.Volume / (Atmospherics.R * temperature));
    }
}
