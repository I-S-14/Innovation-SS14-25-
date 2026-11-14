// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._IS14.Silicons;
using Content.Shared.Light.Components;
using Content.Shared.Light.EntitySystems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Dynamics;
using Robust.Shared.Physics.Events;

namespace Content.Server._IS14.Silicons;

/// <summary>
/// Retains Goob's automatic camera lights without coupling them to the newer camera activity sensor.
/// </summary>
public sealed class AiCameraLightSystem : EntitySystem
{
    [Dependency] private readonly SlimPoweredLightSystem _lights = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<LightOnCollideColliderComponent, PreventCollideEvent>(OnPreventCollide);
        SubscribeLocalEvent<LightOnCollideColliderComponent, StartCollideEvent>(OnStart);
        SubscribeLocalEvent<LightOnCollideColliderComponent, EndCollideEvent>(OnEnd);
        SubscribeLocalEvent<LightOnCollideColliderComponent, ComponentShutdown>(OnColliderShutdown);
        SubscribeLocalEvent<LightOnCollideComponent, PreventCollideEvent>(OnReceiverPreventCollide);
        SubscribeLocalEvent<LightOnCollideComponent, ComponentShutdown>(OnCameraShutdown);
    }

    private bool MatchesFixture(EntityUid owner, Fixture fixture, string id)
    {
        return TryComp<FixturesComponent>(owner, out var fixtures) &&
               fixtures.Fixtures.TryGetValue(id, out var expected) && ReferenceEquals(fixture, expected);
    }

    private void OnPreventCollide(Entity<LightOnCollideColliderComponent> ent, ref PreventCollideEvent args)
    {
        // Leave the eye's existing fixtures and camera activity contacts alone.
        if (!MatchesFixture(ent.Owner, args.OurFixture, ent.Comp.FixtureId))
            return;

        if (!TryComp<LightOnCollideComponent>(args.OtherEntity, out var camera) ||
            !MatchesFixture(args.OtherEntity, args.OtherFixture, camera.FixtureId))
            args.Cancelled = true;
    }

    private void OnReceiverPreventCollide(Entity<LightOnCollideComponent> ent, ref PreventCollideEvent args)
    {
        if (!MatchesFixture(ent.Owner, args.OurFixture, ent.Comp.FixtureId))
            return;

        // The eye's large activity fixture must not contact the separate light sensor.
        if (!TryComp<LightOnCollideColliderComponent>(args.OtherEntity, out var collider) ||
            !MatchesFixture(args.OtherEntity, args.OtherFixture, collider.FixtureId))
            args.Cancelled = true;
    }

    private void OnStart(Entity<LightOnCollideColliderComponent> ent, ref StartCollideEvent args)
    {
        if (args.OurFixtureId != ent.Comp.FixtureId ||
            !TryComp<LightOnCollideComponent>(args.OtherEntity, out var camera) ||
            args.OtherFixtureId != camera.FixtureId)
            return;

        camera.Colliders.Add(ent.Owner);
        ent.Comp.LitCameras.Add(args.OtherEntity);
        SetLight(args.OtherEntity, true);
    }

    private void OnEnd(Entity<LightOnCollideColliderComponent> ent, ref EndCollideEvent args)
    {
        if (args.OurFixtureId != ent.Comp.FixtureId ||
            !TryComp<LightOnCollideComponent>(args.OtherEntity, out var camera) ||
            args.OtherFixtureId != camera.FixtureId)
            return;

        ent.Comp.LitCameras.Remove(args.OtherEntity);
        camera.Colliders.Remove(ent.Owner);
        if (camera.Colliders.Count == 0)
            SetLight(args.OtherEntity, false);
    }

    private void OnColliderShutdown(Entity<LightOnCollideColliderComponent> ent, ref ComponentShutdown args)
    {
        foreach (var uid in ent.Comp.LitCameras.ToArray())
        {
            if (!TryComp<LightOnCollideComponent>(uid, out var camera))
                continue;

            camera.Colliders.Remove(ent.Owner);
            if (camera.Colliders.Count == 0)
                SetLight(uid, false);
        }

        ent.Comp.LitCameras.Clear();
    }

    private void OnCameraShutdown(Entity<LightOnCollideComponent> ent, ref ComponentShutdown args)
    {
        foreach (var uid in ent.Comp.Colliders)
        {
            if (TryComp<LightOnCollideColliderComponent>(uid, out var collider))
                collider.LitCameras.Remove(ent.Owner);
        }

        ent.Comp.Colliders.Clear();
        SetLight(ent.Owner, false);
    }

    private void SetLight(EntityUid uid, bool enabled)
    {
        if (!TerminatingOrDeleted(uid) && TryComp<SlimPoweredLightComponent>(uid, out var light))
            _lights.SetEnabled((uid, light), enabled);
    }
}
