// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Server.Destructible;
using Content.Server.Polymorph.Components;
using Content.Server.Popups;
using Content.Server.Storage.Components;
using Content.Server.Storage.EntitySystems;
using Content.Server.Stunnable;
using Content.Shared.Body.Components;
using Content.Shared.Administration.Logs;
using Content.Shared.Body;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Popups;
using Content.Shared.Storage.Components;
using Content.Shared.Tag;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Shared.Gibbing.Systems;
using Content.Server.Body.Systems;

namespace Content.Server.ImmovableRod;

public sealed partial class ImmovableRodSystem : EntitySystem
{
    //IS14-change start: restore declarations used by merged implementation.
    [Dependency] private ISharedAdminLogManager _adminLogger = default!;
    [Dependency] private Content.Shared.Body.Systems.SharedBodySystem _gibbing = default!;
    //IS14-change end

    [Dependency] private IRobustRandom _random = default!;

    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly DestructibleSystem _destructible = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly EntityStorageSystem _entityStorage = default!; // Goobstation
    [Dependency] private readonly TagSystem _tag = default!; // Goobstation
    [Dependency] private readonly StunSystem _stun = default!; // Goobstation

    private static readonly ProtoId<TagPrototype> IgnoreTag = "IgnoreImmovableRod"; // Goobstation

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // we are deliberately including paused entities. rod hungers for all
        foreach (var (rod, trans) in EntityQuery<ImmovableRodComponent, TransformComponent>(true))
        {
            if (!rod.DestroyTiles)
                continue;

            if (!TryComp<MapGridComponent>(trans.GridUid, out var grid))
                continue;

            _map.SetTile(trans.GridUid.Value, grid, trans.Coordinates, Tile.Empty);
        }
    }

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ImmovableRodComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<ImmovableRodComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ImmovableRodComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(EntityUid uid, ImmovableRodComponent component, MapInitEvent args)
    {
        if (TryComp(uid, out PhysicsComponent? phys))
        {
            _physics.SetLinearDamping(uid, phys, 0f);
            _physics.SetFriction(uid, phys, 0f);
            _physics.SetBodyStatus(uid, phys, BodyStatus.InAir);

            var (worldPos, worldRot) = _transform.GetWorldPositionRotation(uid);
            var vel = worldRot.ToWorldVec() * component.MaxSpeed;

            if (component.RandomizeVelocity)
            {
                vel = component.DirectionOverride.Degrees switch
                {
                    0f => _random.NextVector2(component.MinSpeed, component.MaxSpeed),
                    _ => worldRot.RotateVec(component.DirectionOverride.ToVec()) * _random.NextFloat(component.MinSpeed, component.MaxSpeed)
                };
            }

            _physics.ApplyLinearImpulse(uid, vel, body: phys);
            _transform.SetLocalRotationNoLerp(uid, (vel - worldPos).ToWorldAngle() + MathHelper.PiOver2);
        }
    }

    private void OnCollide(EntityUid uid, ImmovableRodComponent component, ref StartCollideEvent args)
    {
        var ent = args.OtherEntity;

        // Goobstation start
        if (component.DamagedEntities.Contains(ent))
            return;

        if (!component.DestroyTiles && _tag.HasTag(ent, IgnoreTag))
            return;
        // Goobstation end

        if (_random.Prob(component.HitSoundProbability))
        {
            _audio.PlayPvs(component.Sound, uid);
        }

        if (HasComp<ImmovableRodComponent>(ent))
        {
            // oh god.
            var coords = Transform(uid).Coordinates;

            _popup.PopupCoordinates(Loc.GetString("immovable-rod-collided-rod-not-good"), coords, PopupType.LargeCaution);
            _adminLogger.Add(LogType.Gib, LogImpact.Low, $"{ToPrettyString(uid)} and {ToPrettyString(ent)} created singularity at X:{coords.X} Y:{coords.Y}");

            Del(uid);
            Del(ent);
            Spawn("Singularity", coords);

            return;
        }

        // Don't delete/hurt self if polymorphed into a rod
        if (TryComp<PolymorphedEntityComponent>(uid, out var polymorphed))
        {
            if (polymorphed.Parent == ent)
                return;
        }

        // gib or damage em
        if (HasComp<BodyComponent>(ent))
        {
            component.MobCount++;

            var coords = Transform(uid).Coordinates;

            _adminLogger.Add(LogType.Gib, LogImpact.Medium, $"Entity {ToPrettyString(uid)} hit {ToPrettyString(ent)} at X:{coords.X} Y:{coords.Y}");

            if (!component.ShouldGib || !_destructible.DestroyEntity(ent))
            {
                if (component.Damage is null)
                    return;

                component.DamagedEntities.Add(ent); // Goobstation
                _damageable.TryChangeDamage(ent, component.Damage, component.IgnoreResistances, origin: uid, partMultiplier: component.PartDamageMultiplier); // Goob edit
                if (component.KnockdownTime > TimeSpan.Zero) // Goobstation
                    _stun.KnockdownOrStun(ent, component.KnockdownTime, true);
                return;
            }

            _gibbing.GibBody(ent);
            _popup.PopupEntity(Loc.GetString("immovable-rod-penetrated-mob", ("rod", uid), ("mob", ent)), uid, PopupType.LargeCaution);
            return;
        }

        // trycomp cause otherwise tests log emptycontents errors. goob.
        if (TryComp<EntityStorageComponent>(ent, out var entStore))// Goobstation
            _entityStorage.EmptyContents(ent, entStore); // Goobstation

        _destructible.DestroyEntity(ent);
    }

    private void OnExamined(EntityUid uid, ImmovableRodComponent component, ExaminedEvent args)
    {
        if (component.MobCount == 0)
        {
            args.PushText(Loc.GetString("immovable-rod-consumed-none", ("rod", uid)));
        }
        else
        {
            args.PushText(Loc.GetString("immovable-rod-consumed-souls", ("rod", uid), ("amount", component.MobCount)));
        }
    }
}
