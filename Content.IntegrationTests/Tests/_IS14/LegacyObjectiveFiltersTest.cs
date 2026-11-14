using System.Collections.Generic;
using System.Linq;
using Content.Server.Objectives.Components;
using Content.Server.Objectives.Systems;
using Content.Server.Revolutionary.Components;
using Content.Shared.Mind;
using Content.Shared.Objectives.Components;
using Content.Shared.Roles;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class LegacyObjectiveFiltersTest
{
    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: IS14TestCommandTraitorObjective
  parent: PermaKillRandomTraitorObjective
  components:
  - type: PickRandomPerson
    conditions:
    - !type:BodyWhitelistCondition
      whitelist:
        components: [CommandStaff]
";

    [TestCase(false)]
    [TestCase(true)]
    public async Task RoleFilterAndNewConditionsBothRestrictAssignment(bool withCondition)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var before = Entities(em);
            var minds = server.System<SharedMindSystem>();
            var picker = minds.CreateMind(null, "Picker");
            var traitor = minds.CreateMind(null, "Traitor");
            var other = minds.CreateMind(null, "Other");
            var traitorBody = em.SpawnEntity("MobHuman", map.GridCoords);
            var otherBody = em.SpawnEntity("MobHuman", map.GridCoords);
            minds.TransferTo(traitor, traitorBody, createGhost: false);
            minds.TransferTo(other, otherBody, createGhost: false);
            server.System<SharedRoleSystem>().MindAddRole(traitor, "MindRoleTraitor", silent: true);
            em.AddComponent<CommandStaffComponent>(otherBody);
            var objective = em.SpawnEntity(withCondition ? "IS14TestCommandTraitorObjective" : "PermaKillRandomTraitorObjective", map.GridCoords);
            Assert.That(em.GetComponent<PickRandomPersonComponent>(objective).Filters, Has.Count.EqualTo(2));
            var assigned = new ObjectiveAssignedEvent(picker, picker.Comp);
            em.EventBus.RaiseLocalEvent(objective, ref assigned);
            if (withCondition)
            {
                Assert.That(assigned.Cancelled, Is.True, "Neither candidate satisfies both conditions and filters.");
                Assert.That(em.GetComponent<TargetObjectiveComponent>(objective).Target, Is.Null);
                em.AddComponent<CommandStaffComponent>(traitorBody);
                assigned = new ObjectiveAssignedEvent(picker, picker.Comp);
                em.EventBus.RaiseLocalEvent(objective, ref assigned);
            }
            Assert.That(assigned.Cancelled, Is.False);
            Assert.That(em.GetComponent<TargetObjectiveComponent>(objective).Target, Is.EqualTo(traitor.Owner));
            DeleteCreated(em, before);
        });
        await pair.CleanReturnAsync();
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task DuplicateTargetsRespectAssigningMindAndInversion(bool inverted)
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        var server = pair.Server;
        var map = await pair.CreateTestMap();
        await server.WaitAssertion(() =>
        {
            var em = server.EntMan;
            var before = Entities(em);
            var minds = server.System<SharedMindSystem>();
            var picker = minds.CreateMind(null, "Picker");
            var candidate = minds.CreateMind(null, "Candidate");
            var body = em.SpawnEntity("MobHuman", map.GridCoords);
            minds.TransferTo(candidate, body, createGhost: false);
            var existing = em.SpawnEntity(null, map.GridCoords);
            em.AddComponent<KillPersonConditionComponent>(existing);
            em.AddComponent<TargetObjectiveComponent>(existing);
            server.System<TargetObjectiveSystem>().SetTarget(existing, candidate);
            picker.Comp.Objectives.Add(existing);
            var objective = em.SpawnEntity("PermaKillRandomPersonObjective", map.GridCoords);
            var filters = em.GetComponent<PickRandomPersonComponent>(objective).Filters;
            Assert.That(filters, Has.Count.EqualTo(1));
            filters[0].Inverted = inverted;
            var assigned = new ObjectiveAssignedEvent(picker, picker.Comp);
            em.EventBus.RaiseLocalEvent(objective, ref assigned);
            Assert.That(assigned.Cancelled, Is.EqualTo(!inverted));
            Assert.That(em.GetComponent<TargetObjectiveComponent>(objective).Target,
                Is.EqualTo(inverted ? candidate.Owner : (EntityUid?) null));
            DeleteCreated(em, before);
        });
        await pair.CleanReturnAsync();
    }

    private static HashSet<EntityUid> Entities(IEntityManager manager)
    {
        var result = new HashSet<EntityUid>();
        var query = manager.AllEntityQueryEnumerator<MetaDataComponent>();
        while (query.MoveNext(out var uid, out _))
            result.Add(uid);
        return result;
    }

    private static void DeleteCreated(IEntityManager manager, HashSet<EntityUid> before)
    {
        foreach (var uid in Entities(manager).Except(before).ToArray())
            if (manager.EntityExists(uid))
                manager.DeleteEntity(uid);
    }
}
