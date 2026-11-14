// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Server.EntityEffects;
using Content.Goobstation.Shared.EntityEffects;
using Content.Shared.Atmos.Components;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Body.Components;
using Content.Server.Body.Systems;
using Content.Server.Fluids.EntitySystems;
using Content.Server.Spreader;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.Reaction;
using Content.Shared.Coordinates.Helpers;
using Content.Shared.EntityEffects.Effects;
using Robust.Shared.Map;

namespace Content.Goobstation.Server.Xenobiology.Systems;

// any other bs needed serverside
public sealed class XenobiologyMiscSystems : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<ReactiveComponent, ExtinguishNearby>(OnExtinguish);
        SubscribeLocalEvent<ReactiveComponent, OxygenateNearby>(OnOxygenate);
        SubscribeLocalEvent<ReactiveComponent, IgniteNearbyEffect>(OnIgniteNearby);
        SubscribeLocalEvent<ReactiveComponent, DoSmokeEntityEffect>(OnSmoke);
    }

    public void OnExtinguish(EntityUid uid, ReactiveComponent component, ref ExtinguishNearby args)
    {

        var lookupSys = EntityManager.System<EntityLookupSystem>();
        var flamSys = EntityManager.System<FlammableSystem>();

        foreach (var entity in lookupSys.GetEntitiesInRange(uid, args.Range))
        {
            if (TryComp(entity, out FlammableComponent? flammable))
                flamSys.Extinguish(entity, flammable);
        }
    }

    public void OnOxygenate(EntityUid uid, ReactiveComponent component, ref OxygenateNearby args)
    {
        var lookupSys = EntityManager.System<EntityLookupSystem>();
        var respSys = EntityManager.System<RespiratorSystem>();

        foreach (var entity in lookupSys.GetEntitiesInRange(uid, args.Range))
        {
            if (TryComp(entity, out RespiratorComponent? resp))
                respSys.UpdateSaturation(entity, args.Factor, resp);
        }
    }

    public void OnIgniteNearby(EntityUid uid, ReactiveComponent component, ref IgniteNearbyEffect args)
    {
        var lookupSys = EntityManager.System<EntityLookupSystem>();
        var flamSys = EntityManager.System<FlammableSystem>();

        foreach (var entity in lookupSys.GetEntitiesInRange(uid, args.Radius))
        {
            if (TryComp(entity, out FlammableComponent? flammable))
                flamSys.AdjustFireStacks(entity, args.FireStacks, flammable, true);
        }
    }

    public void OnSmoke(EntityUid uid, ReactiveComponent component, ref DoSmokeEntityEffect args)
    {

        var mapSys = IoCManager.Resolve<IEntityManager>().System<SharedMapSystem>();
        var transformSys = EntityManager.System<SharedTransformSystem>();
        var spreaderSys = EntityManager.System<SpreaderSystem>();
        var smokeSys = EntityManager.System<SmokeSystem>();

        if (!TryComp(uid, out TransformComponent? xform))
            return;

        var mapCoords = transformSys.GetMapCoordinates(uid, xform);


        if (!mapSys.TryFindGridAt(mapCoords, out var gridUid, out var grid)
            || !mapSys.TryGetTileRef(gridUid, grid, xform.Coordinates, out var tileRef)
            || tileRef.Tile.IsEmpty)
            return;

        if (spreaderSys.RequiresFloorToSpread(args.SmokePrototype.ToString()) && tileRef.Tile.IsEmpty)
            return;

        var coords = mapSys.MapToGrid(gridUid, mapCoords);
        var ent = SpawnAtPosition(args.SmokePrototype, coords.SnapToGrid());
        if (!TryComp<SmokeComponent>(ent, out var smoke))
        {
            QueueDel(ent);
            return;
        }

        smokeSys.StartSmoke(ent, args.Solution, args.Duration, args.SpreadAmount, smoke);
    }

}
