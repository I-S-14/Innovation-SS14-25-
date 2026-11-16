// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Linq;
using System.Numerics;
using Content.IntegrationTests.Fixtures.Attributes;
using Content.IntegrationTests.Tests.GameRules;
using Content.Server._IS14.Cyborgs;
using Content.Server.Ghost.Roles;
using Content.Server.Ghost.Roles.Components;
using Content.Shared._IS14.Cyborgs;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared._IS14.Cyborgs.Prototypes;
using Content.Shared.Antag.Components;
using Content.Shared.Eye;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Silicons.Borgs.Components;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class MothershipEyeTest : AntagTest
{
    public override PoolSettings PoolSettings => new()
    {
        Dirty = true,
        DummyTicker = false,
        Connected = true,
        Map = PoolManager.TestStation
    };

    [Test]
    [RunOnSide(Side.Server)]
    public void CoreKeepsCyborgBodyWhileEyeJumpsFollowsAndReturnsOnLostCoverage()
    {
        STicker.StartGameRule("Xenoborgs", out var rule);
        var spawner = SEntMan.EntityQuery<GhostRoleAntagSpawnerComponent>().Single(c =>
            c.Rule == rule!.Value.Owner && c.Definition?.Id == "IS14MothershipCoreAntag");
        var role = SEntMan.GetComponent<GhostRoleComponent>(spawner.Owner);
        Assert.That(Server.System<GhostRoleSystem>().Takeover(ServerSession!, role.Identifier), Is.True);
        var core = ServerSession!.AttachedEntity!.Value;
        var cyborg = SEntMan.GetComponent<CyborgComponent>(core);
        Assert.That(cyborg.Model?.Id, Is.EqualTo("IS14CyborgModelMothership"));
        Assert.That(cyborg.ToolContainer.ContainedEntities.Count,
            Is.EqualTo(SProtoMan.Index<CyborgModelPrototype>(cyborg.Model!.Value).BasicTools.Count));
        Assert.That(SEntMan.HasComponent<BorgChassisComponent>(core), Is.False);

        var system = Server.System<MothershipEyeSystem>();
        var mover = Server.System<SharedMoverController>();
        var transform = Server.System<SharedTransformSystem>();
        var component = SEntMan.GetComponent<MothershipEyeComponent>(core);
        Entity<MothershipEyeComponent> ent = (core, component);
        var home = SEntMan.GetComponent<TransformComponent>(core).Coordinates;
        var originalMask = SEntMan.GetComponent<EyeComponent>(core).VisibilityMask;
        Assert.That(system.Start(ent), Is.True);
        Assert.That(SEntMan.GetComponent<EyeComponent>(core).VisibilityMask & (int) VisibilityFlags.Ghost,
            Is.Not.Zero, "the core must see the AI eye marker while observing");
        Assert.That(ServerSession.AttachedEntity, Is.EqualTo(core));
        Assert.That(mover.GetEffectiveMover(core), Is.EqualTo(component.Eye));
        Assert.That(SEntMan.GetComponent<InputMoverComponent>(core).CanMove, Is.True,
            "the static core must accept WASD input to relay it to the eye");
        Assert.That(SEntMan.GetComponent<InputMoverComponent>(component.Eye!.Value).CanMove, Is.True);
        Assert.That(system.InCoverage(ent, component.Eye!.Value), Is.True);

        // A xenoborg far outside the ship provides a separate, bounded vision area.
        var distant = new EntityCoordinates(home.EntityId, home.Position + new Vector2(100, 100));
        var borg = SEntMan.SpawnEntity("IS14XenoborgScout", distant);
        Assert.That(system.Start(ent, borg, follow: true), Is.True);
        var eye = component.Eye!.Value;
        Assert.That(component.Following, Is.EqualTo(borg));
        Assert.That(system.InCoverage(ent, eye), Is.True);
        transform.SetCoordinates(borg, new EntityCoordinates(distant.EntityId, distant.Position + new Vector2(3, 0)));
        system.Update(0.1f);
        Assert.That(transform.GetMapCoordinates(eye).Position,
            Is.EqualTo(transform.GetMapCoordinates(borg).Position));

        // Moving the eye beyond every borg's sight must restore body vision and stop relaying input.
        transform.SetCoordinates(eye, new EntityCoordinates(distant.EntityId, distant.Position + new Vector2(50, 0)));
        system.Update(0.1f);
        Assert.That(component.Eye, Is.Null);
        Assert.That(SEntMan.GetComponent<EyeComponent>(core).VisibilityMask, Is.EqualTo(originalMask),
            "returning must restore the original visibility mask");
        Assert.That(SEntMan.HasComponent<RelayInputMoverComponent>(core), Is.False);
        Assert.That(mover.GetEffectiveMover(core), Is.EqualTo(core));
        Assert.That(SEntMan.GetComponent<InputMoverComponent>(core).CanMove, Is.False,
            "returning to the core must restore its immobility");
        Assert.That(SEntMan.GetComponent<TransformComponent>(core).Coordinates, Is.EqualTo(home));

        Assert.That(system.Start(ent, borg, follow: true), Is.True);
        SEntMan.DeleteEntity(borg);
        system.Update(0.1f);
        Assert.That(component.Eye, Is.Null, "a deleted followed borg must not leave a stranded eye");
        STicker.ClearGameRules();
    }
}
