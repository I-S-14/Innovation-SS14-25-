using Content.IntegrationTests.Fixtures;
using Content.Shared.Movement.Systems;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class AngularFrictionCompatibilityTest : GameTest
{
    [TestCase(2f, 1f)]
    [TestCase(-2f, -1f)]
    [TestCase(0.05f, 0.05f)]
    [TestCase(-0.05f, -0.05f)]
    public async Task AngularFrictionTreatsBothDirectionsEqually(float initial, float expected)
    {
        await Server.WaitAssertion(() =>
        {
            var velocity = initial;
            Server.System<SharedMoverController>().Friction(0.1f, 0.1f, 5f, ref velocity);
            Assert.That(velocity, Is.EqualTo(expected).Within(0.00001f));
        });
    }
}
