using System.Linq;
using Content.Shared.Audio;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class MachineAmbienceTest
{
    [TestCase("IS14VendingMachineMed")]
    [TestCase("IS14VendingMachineCaptain")]
    [TestCase("MedicalTechFab")]
    public async Task EffectsFollowPower(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
            NoLoadTestPrototypes = true,
        });
        var map = await pair.CreateTestMap();
        EntityUid machine = default;
        EntityUid observer = default;
        EntityUid? previous = null;
        ICommonSession session = null!;
        await pair.Server.WaitPost(() =>
        {
            session = pair.Server.PlayerMan.Sessions.Single();
            previous = session.AttachedEntity;
            observer = pair.Server.EntMan.SpawnEntity("MobObserver", map.GridCoords);
            pair.Server.PlayerMan.SetAttachedEntity(session, observer);
            machine = pair.Server.EntMan.SpawnEntity(prototype, map.GridCoords);
            pair.Server.System<SharedPowerReceiverSystem>().SetNeedsPower(machine, false);
        });
        await pair.RunSeconds(1);
        var clientMachine = pair.ToClientUid(machine);
        await AssertEffects(true);

        await pair.Server.WaitPost(() =>
            pair.Server.System<SharedPowerReceiverSystem>().SetNeedsPower(machine, true));
        // Wait longer than the longest light-behaviour loop.
        await pair.RunSeconds(2);
        await AssertEffects(false);

        await pair.Server.WaitPost(() =>
            pair.Server.System<SharedPowerReceiverSystem>().SetNeedsPower(machine, false));
        await pair.RunSeconds(1);
        await AssertEffects(true);
        await pair.Server.WaitPost(() =>
        {
            pair.Server.PlayerMan.SetAttachedEntity(session, previous);
            pair.Server.EntMan.DeleteEntity(machine);
            pair.Server.EntMan.DeleteEntity(observer);
            pair.Server.System<SharedMapSystem>().DeleteMap(map.MapId);
        });
        await pair.RunTicksSync(3);
        await pair.CleanReturnAsync();

        async Task AssertEffects(bool enabled)
        {
            await pair.Server.WaitAssertion(() =>
                Assert.That(pair.Server.EntMan.GetComponent<AmbientSoundComponent>(machine).Enabled,
                    Is.EqualTo(enabled), "Server ambience state"));
            await pair.Client.WaitAssertion(() =>
            {
                var em = pair.Client.EntMan;
                Assert.That(em.GetComponent<AmbientSoundComponent>(clientMachine).Enabled,
                    Is.EqualTo(enabled), "Client ambience state");
                Assert.That(pair.Client.System<SharedPointLightSystem>().TryGetLight(clientMachine, out var light), Is.True);
                Assert.That(light!.Enabled, Is.EqualTo(enabled), "Client light state");
                if (enabled)
                    Assert.That(light.Energy, Is.GreaterThan(0), "Visible light energy");
            });
        }
    }
}
