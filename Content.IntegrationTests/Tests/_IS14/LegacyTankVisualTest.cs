using System.Collections.Generic;
using Content.Shared.Atmos.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class LegacyTankVisualTest
{
    [TestCase("DoubleEmergencyPlasmaTank")]
    [TestCase("WaterVaporTank")]
    public async Task PressureLossChangesTintAndRecoveryRestoresIt(string prototype)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var client = pair.Client;
        await client.WaitAssertion(() =>
        {
            var entity = client.EntMan.SpawnEntity(prototype, MapCoordinates.Nullspace);
            var sprite = client.EntMan.GetComponent<SpriteComponent>(entity);
            var appearance = client.EntMan.GetComponent<AppearanceComponent>(entity);
            var original = sprite.Color.WithAlpha(0.4f);
            client.System<SpriteSystem>().SetColor((entity, sprite), original);
            void SetIntegrity(float integrity)
            {
                var update = new AppearanceChangeEvent
                {
                    Component = appearance,
                    Sprite = sprite,
                    AppearanceData = new Dictionary<Enum, object>
                    {
                        [GasIntegrity.Integrity] = integrity,
                        [GasIntegrity.MaxIntegrity] = 10f,
                    },
                };
                client.EntMan.EventBus.RaiseLocalEvent(entity, ref update);
            }
            SetIntegrity(5);
            Assert.That(sprite.Color, Is.Not.EqualTo(original));
            Assert.That(sprite.Color.A, Is.EqualTo(original.A));
            SetIntegrity(10);
            Assert.That(sprite.Color, Is.EqualTo(original));
            client.EntMan.DeleteEntity(entity);
        });
        await pair.CleanReturnAsync();
    }
}
