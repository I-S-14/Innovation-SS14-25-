using Content.Server._IS14.Economy.EconomyMonitor;
using Content.Server.Station.Systems;
using Content.Shared._IS14.Economy.EconomyMonitor;
using Content.Shared._IS14.Economy.VendingMachine;
using Content.Shared.Pinpointer;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Economy.EconomyMonitor;

public sealed class EconomyMonitorSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EconomyTransactionEvent>(OnTransaction);

        Subs.BuiEvents<EconomyMonitorConsoleComponent>(EconomyMonitorUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnConsoleOpened);
        });
    }

    private void OnTransaction(EconomyTransactionEvent ev)
    {
        NetCoordinates? location = null;
        if (ev.SourceEntity.HasValue && TryComp<TransformComponent>(ev.SourceEntity.Value, out var xform))
            location = GetNetCoordinates(xform.Coordinates);

        var record = new EconomyTransactionRecord(
            _timing.CurTime,
            ev.AccountNumber,
            ev.Delta,
            ev.NewBalance,
            ev.Description,
            ev.SourceEntity.HasValue ? GetNetEntity(ev.SourceEntity.Value) : null,
            location);

        var serverQuery = EntityQueryEnumerator<EconomyMonitorServerComponent>();
        while (serverQuery.MoveNext(out var serverUid, out var server))
        {
            server.Log.Add(record);
            if (server.Log.Count > server.MaxEntries)
                server.Log.RemoveAt(0);

            PushToOpenConsoles(serverUid, server);
        }
    }

    private void OnConsoleOpened(Entity<EconomyMonitorConsoleComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!TryFindServer(ent.Comp.NetworkId, out _, out var server))
            return;

        // Ensure the grid has a NavMapComponent so the client can render the map
        var xform = Transform(ent.Owner);
        if (xform.GridUid != null)
            EnsureComp<NavMapComponent>(xform.GridUid.Value);

        SendState(ent.Owner, ent.Owner, server);
    }

    private void PushToOpenConsoles(EntityUid serverUid, EconomyMonitorServerComponent server)
    {
        var consoleQuery = EntityQueryEnumerator<EconomyMonitorConsoleComponent>();
        while (consoleQuery.MoveNext(out var consoleUid, out var console))
        {
            if (console.NetworkId != server.NetworkId)
                continue;

            if (!_ui.IsUiOpen(consoleUid, EconomyMonitorUiKey.Key))
                continue;

            SendState(consoleUid, consoleUid, server);
        }
    }

    private void SendState(EntityUid consoleUid, EntityUid contextUid, EconomyMonitorServerComponent server)
    {
        var records = new List<EconomyTransactionRecord>(server.Log);
        records.Reverse();

        var vendors = CollectVendors(contextUid);

        var xform = Transform(contextUid);
        var gridEnt = xform.GridUid.HasValue ? GetNetEntity(xform.GridUid.Value) : (NetEntity?)null;

        _ui.SetUiState(consoleUid, EconomyMonitorUiKey.Key,
            new EconomyMonitorUiState(records, vendors, gridEnt));
    }

    private List<EconomyVendorBlipInfo> CollectVendors(EntityUid consoleUid)
    {
        var result = new List<EconomyVendorBlipInfo>();
        var consoleGrid = Transform(consoleUid).GridUid;

        var query = EntityQueryEnumerator<IS14VendingMachineComponent, TransformComponent, MetaDataComponent>();
        while (query.MoveNext(out var uid, out _, out var xform, out var meta))
        {
            // Only include vendors on the same grid as the console
            if (xform.GridUid != consoleGrid)
                continue;

            result.Add(new EconomyVendorBlipInfo(
                GetNetEntity(uid),
                meta.EntityName,
                GetNetCoordinates(xform.Coordinates),
                meta.EntityPrototype?.ID));
        }

        return result;
    }

    private bool TryFindServer(string networkId, out EntityUid uid, out EconomyMonitorServerComponent server)
    {
        var query = EntityQueryEnumerator<EconomyMonitorServerComponent>();
        while (query.MoveNext(out var sUid, out var sComp))
        {
            if (sComp.NetworkId != networkId)
                continue;

            uid = sUid;
            server = sComp;
            return true;
        }

        uid = EntityUid.Invalid;
        server = default!;
        return false;
    }
}
