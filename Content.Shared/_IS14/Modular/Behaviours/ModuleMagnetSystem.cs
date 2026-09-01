// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modular.Components;
using Content.Shared._IS14.Modular.Systems;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Whitelist;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     The ore satchel's magnet.
///
///     Written rather than borrowed from <c>MagnetPickup</c>: that one keys off
///     <c>ItemToggle</c> and a scan clock stamped at map init, neither of which a module
///     granted mid-round has. Driving it from the module's own switch also means the sweep
///     costs charge, which is what makes it a module rather than a free upgrade.
/// </summary>
public sealed class ModuleMagnetSystem : ModuleBehaviourSystem<ModuleMagnetComponent>
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ChassisPowerSystem _power = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<PhysicsComponent> _physicsQuery;

    public override void Initialize()
    {
        base.Initialize();

        _physicsQuery = GetEntityQuery<PhysicsComponent>();
    }

    protected override bool RequiresActive(Entity<ModuleMagnetComponent> ent) => true;

    protected override void Start(Entity<ModuleMagnetComponent> ent, EntityUid chassis)
    {
        ent.Comp.Running = true;
        ent.Comp.NextScan = _timing.CurTime + TimeSpan.FromSeconds(ent.Comp.Interval);
        Dirty(ent);
    }

    protected override void Stop(Entity<ModuleMagnetComponent> ent, EntityUid chassis)
    {
        ent.Comp.Running = false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ModuleMagnetComponent, ModuleStorageComponent, ChassisModuleComponent>();

        while (query.MoveNext(out var uid, out var magnet, out var storage, out var module))
        {
            if (!magnet.Running || module.Chassis is not { } chassis)
                continue;

            if (magnet.NextScan > now)
                continue;

            magnet.NextScan = now + TimeSpan.FromSeconds(magnet.Interval);
            Dirty(uid, magnet);

            Sweep((uid, magnet), storage, chassis);
        }
    }

    private void Sweep(Entity<ModuleMagnetComponent> ent, ModuleStorageComponent module, EntityUid chassis)
    {
        // The bag is whichever entity the storage behaviour hung the grid off.
        var host = module.Host ?? ent.Owner;

        if (!TryComp<StorageComponent>(host, out var storage) || !_storage.HasSpace((host, storage)))
            return;

        // Reach from the suit, not from the module: the module's own transform is inside
        // a container, and what the wearer expects is a field around themselves.
        var origin = chassis;
        var xform = Transform(origin);
        var moverCoords = _transform.GetMoverCoordinates(origin, xform);
        var finalCoords = xform.Coordinates;

        var picked = false;

        foreach (var near in _lookup.GetEntitiesInRange(origin, ent.Comp.Range, LookupFlags.Dynamic | LookupFlags.Sundries))
        {
            if (_whitelist.IsWhitelistFail(storage.Whitelist, near))
                continue;

            // Only things lying about. Without this the magnet reaches into other
            // people's pockets, which is a different module entirely.
            if (!_physicsQuery.TryGetComponent(near, out var physics) || physics.BodyStatus != BodyStatus.OnGround)
                continue;

            if (near == origin || near == host)
                continue;

            var nearXform = Transform(near);
            var nearMap = _transform.GetMapCoordinates(near, xform: nearXform);
            var nearCoords = _transform.ToCoordinates(moverCoords.EntityId, nearMap);

            if (!_storage.Insert(host, near, out var stacked, storageComp: storage, playSound: !picked))
                continue;

            _storage.PlayPickupAnimation(stacked ?? near, nearCoords, finalCoords, nearXform.LocalRotation);
            picked = true;

            if (!_storage.HasSpace((host, storage)))
                break;
        }

        // Charged for a sweep that caught something. A magnet humming over bare rock
        // costs nothing, which is the only reason it is worth leaving switched on.
        if (picked && ent.Comp.Cost > 0f)
            _power.TryUseCharge(chassis, ent.Comp.Cost);
    }
}
