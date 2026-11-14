using System.Linq;
using Content.IntegrationTests.Tests.Helpers;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Medical;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Traits.Assorted;

namespace Content.IntegrationTests.Tests._IS14;

public sealed class GoobDefibrillatorCompatibilityTest : InteractionTest
{
    private sealed class ZapListener : TestListenerSystem<TargetDefibrillatedEvent>;

    protected override string PlayerPrototype => "MobHuman";

    [TestCase(false, false)]
    [TestCase(true, false)]
    [TestCase(false, true)]
    public async Task HeldDefibrillatorRespectsTheRetainedRevivalRules(bool buckled, bool unrevivable)
    {
        await AddAtmosphere();
        await SpawnTarget("MobHuman");
        var target = STarget!.Value;
        if (buckled)
        {
            var bed = ToServer(await Spawn("Bed"));
            await Server.WaitAssertion(() =>
            {
                SEntMan.System<SharedBuckleSystem>().TryBuckle(target, null, bed);
                Assert.That(Comp<BuckleComponent>().BuckledTo, Is.EqualTo(bed));
            });
        }

        await Server.WaitPost(() =>
        {
            SEntMan.EnsureComponent<TestListenerComponent>(target);
            if (unrevivable)
                SEntMan.EnsureComponent<UnrevivableComponent>(target);
            // Start with a dead patient whose vital damage is below the retained revival limit.
            SEntMan.System<MobStateSystem>().ChangeMobState(target, MobState.Dead);
        });
        var defib = await PlaceInHands("Defibrillator", enableToggleable: true);
        await RunSeconds((float) Comp<DefibrillatorComponent>(defib).ZapDelay.TotalSeconds);
        await Server.WaitAssertion(() => Assert.That(Comp<MobStateComponent>().CurrentState, Is.EqualTo(MobState.Dead)));
        await Interact();
        await Server.WaitAssertion(() =>
        {
            var zap = GetEvents<TargetDefibrillatedEvent>(target).Single();
            Assert.That(zap.Target, Is.EqualTo(target));
            Assert.That(zap.Defibrillator.Owner, Is.EqualTo(ToServer(defib)));
            Assert.That(Comp<MobStateComponent>().CurrentState == MobState.Dead, Is.EqualTo(unrevivable));
        });
    }
}
