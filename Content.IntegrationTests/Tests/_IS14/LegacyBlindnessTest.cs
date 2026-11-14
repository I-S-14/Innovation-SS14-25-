using Content.Shared.Eye.Blinding.Components;
using Content.Shared.Eye.Blinding.Systems;
using Content.Shared.StatusEffect;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class LegacyBlindnessTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: IS14TestLegacyBlindable
  components:
  - type: Blindable
  - type: StatusEffects
    allowed: [TemporaryBlindness]
";

    [Test]
    public async Task TemporaryBlindnessAppliesAndExpires()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        EntityUid entity = default;
        await server.WaitAssertion(() =>
        {
            entity = server.EntMan.SpawnEntity("IS14TestLegacyBlindable", map.GridCoords);
            Assert.That(server.EntMan.GetComponent<BlindableComponent>(entity).IsBlind, Is.False);
            Assert.That(server.System<StatusEffectsSystem>().TryAddStatusEffect(entity,
                TemporaryBlindnessSystem.BlindingStatusEffect, TimeSpan.FromSeconds(0.5), false,
                nameof(TemporaryBlindnessComponent).Replace("Component", "")), Is.True);
            Assert.That(server.EntMan.GetComponent<BlindableComponent>(entity).IsBlind, Is.True);
        });
        await PoolManager.WaitUntil(server, () => !server.EntMan.HasComponent<TemporaryBlindnessComponent>(entity));
        await server.WaitAssertion(() =>
        {
            Assert.That(server.EntMan.GetComponent<BlindableComponent>(entity).IsBlind, Is.False);
            server.EntMan.DeleteEntity(entity);
        });
        await pair.CleanReturnAsync();
    }
}
