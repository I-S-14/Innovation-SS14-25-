using Content.Server.Antag;
using Content.Shared.Antag.Components;
using Content.Shared.Mind;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class AntagJobEligibilityTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: playTimeTracker
  id: IS14TestRestrictedAntagJob
- type: playTimeTracker
  id: IS14TestAllowedAntagJob
- type: job
  id: IS14TestRestrictedAntagJob
  name: job-name-passenger
  playTimeTracker: IS14TestRestrictedAntagJob
  canBeAntag: false
- type: job
  id: IS14TestAllowedAntagJob
  name: job-name-passenger
  playTimeTracker: IS14TestAllowedAntagJob
  canBeAntag: true
";

    [TestCase("IS14TestRestrictedAntagJob", false)]
    [TestCase("IS14TestAllowedAntagJob", true)]
    public async Task DetachedMindKeepsAssignedJobRestriction(string job, bool eligible)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        await server.WaitAssertion(() =>
        {
            var minds = server.System<SharedMindSystem>();
            var mind = minds.CreateMind(pair.Player!.UserId);
            var temporary = server.EntMan.SpawnEntity(null, Robust.Shared.Map.MapCoordinates.Nullspace);
            // Assign the role through its normal API while the mind owns an entity,
            // then detach it to exercise the eligibility path without a body.
            minds.TransferTo(mind, temporary, createGhost: false);
            server.System<SharedRoleSystem>().MindAddJobRole(mind, silent: true, jobPrototype: job);
            minds.TransferTo(mind, null, createGhost: false);
            var uid = server.EntMan.SpawnEntity("Traitor", Robust.Shared.Map.MapCoordinates.Nullspace);
            var selection = server.EntMan.GetComponent<AntagSelectionComponent>(uid);
            var definition = selection.Antags[0].Proto;
            Assert.That(pair.Player.AttachedEntity, Is.Null);
            Assert.That(server.System<ServerAntagSelectionSystem>()
                .IsSessionValid(pair.Player, (uid, selection), definition), Is.EqualTo(eligible));
            server.EntMan.DeleteEntity(uid);
            minds.TransferTo(mind, temporary, createGhost: false);
            server.EntMan.DeleteEntity(mind);
            server.EntMan.DeleteEntity(temporary);
        });
        await pair.CleanReturnAsync();
    }
}
