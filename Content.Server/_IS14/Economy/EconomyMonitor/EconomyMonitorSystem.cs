using System.Text;
using Content.Server._IS14.Economy.EconomyMonitor;
using Content.Server.Station.Systems;
using Content.Shared._IS14.Economy.EconomyMonitor;
using Content.Shared._IS14.Economy.VendingMachine;
using Content.Shared.Paper;
using Content.Shared.Pinpointer;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Economy.EconomyMonitor;

public sealed class EconomyMonitorSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly PaperSystem _paper = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;

    private static readonly SoundSpecifier PrintSound = new SoundPathSpecifier("/Audio/Machines/printer.ogg");

    /// <summary>Cap on how many records fit on one printed report.</summary>
    private const int MaxPrintedRecords = 50;

    /// <summary>
    /// How often an open console is refreshed from incoming transactions. The whole log is
    /// re-sent on every update, and a salary tick can fire hundreds of transactions at once,
    /// so batching them into one refresh keeps that off the wire. User actions stay instant.
    /// </summary>
    private static readonly TimeSpan PassiveRefreshInterval = TimeSpan.FromSeconds(1);

    private readonly HashSet<EntityUid> _pendingRefresh = new();
    private readonly List<EntityUid> _refreshBuffer = new();
    private TimeSpan _nextPassiveRefresh;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EconomyTransactionEvent>(OnTransaction);

        Subs.BuiEvents<EconomyMonitorConsoleComponent>(EconomyMonitorUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnConsoleOpened);
            subs.Event<BoundUIClosedEvent>(OnConsoleClosed);
            subs.Event<EconomyMonitorDeleteMessage>(OnDelete);
            subs.Event<EconomyMonitorPrintMessage>(OnPrint);
        });
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingRefresh.Count == 0 || _timing.CurTime < _nextPassiveRefresh)
            return;

        _nextPassiveRefresh = _timing.CurTime + PassiveRefreshInterval;

        // Drain into a buffer first: SendState clears the console's pending flag,
        // which would otherwise mutate the set we're iterating.
        _refreshBuffer.Clear();
        _refreshBuffer.AddRange(_pendingRefresh);
        _pendingRefresh.Clear();

        foreach (var consoleUid in _refreshBuffer)
        {
            if (!TryComp<EconomyMonitorConsoleComponent>(consoleUid, out var console)
                || !_ui.IsUiOpen(consoleUid, EconomyMonitorUiKey.Key)
                || !TryFindServer(console.NetworkId, out _, out var server))
            {
                continue;
            }

            SendState(consoleUid, consoleUid, server);
        }
    }

    private void OnTransaction(EconomyTransactionEvent ev)
    {
        NetCoordinates? location = null;
        if (ev.SourceEntity.HasValue && TryComp<TransformComponent>(ev.SourceEntity.Value, out var xform))
            location = GetNetCoordinates(xform.Coordinates);

        var serverQuery = EntityQueryEnumerator<EconomyMonitorServerComponent>();
        while (serverQuery.MoveNext(out var serverUid, out var server))
        {
            var record = new EconomyTransactionRecord(
                server.NextRecordId++,
                _timing.CurTime,
                ev.AccountNumber,
                ev.Delta,
                ev.NewBalance,
                ev.Description,
                ev.SourceEntity.HasValue ? GetNetEntity(ev.SourceEntity.Value) : null,
                location,
                ev.GroupId);

            server.Log.Add(record);
            if (server.Log.Count > server.MaxEntries)
                server.Log.RemoveAt(0);

            QueueRefresh(server);
        }
    }

    /// <summary>Marks every open console on this server's network for the next batched refresh.</summary>
    private void QueueRefresh(EconomyMonitorServerComponent server)
    {
        var consoleQuery = EntityQueryEnumerator<EconomyMonitorConsoleComponent>();
        while (consoleQuery.MoveNext(out var consoleUid, out var console))
        {
            if (console.NetworkId != server.NetworkId)
                continue;

            if (_ui.IsUiOpen(consoleUid, EconomyMonitorUiKey.Key))
                _pendingRefresh.Add(consoleUid);
        }
    }

    private void OnDelete(Entity<EconomyMonitorConsoleComponent> ent, ref EconomyMonitorDeleteMessage args)
    {
        if (args.RecordIds.Count == 0 || !TryFindServer(ent.Comp.NetworkId, out var serverUid, out var server))
            return;

        var ids = new HashSet<int>(args.RecordIds);
        server.Log.RemoveAll(record => ids.Contains(record.Id));

        PushToOpenConsoles(serverUid, server);
    }

    private void OnPrint(Entity<EconomyMonitorConsoleComponent> ent, ref EconomyMonitorPrintMessage args)
    {
        if (args.RecordIds.Count == 0 || !TryFindServer(ent.Comp.NetworkId, out _, out var server))
            return;

        var ids = new HashSet<int>(args.RecordIds);
        var report = new StringBuilder();
        report.AppendLine(Loc.GetString("economy-monitor-report-header"));

        var printed = 0;
        foreach (var record in server.Log)
        {
            if (!ids.Contains(record.Id))
                continue;

            report.AppendLine(Loc.GetString("economy-monitor-report-line",
                ("time", FormatTime(record.Timestamp)),
                ("account", record.AccountNumber),
                ("amount", record.Delta >= 0 ? $"+{record.Delta}" : record.Delta.ToString()),
                ("description", record.Description),
                ("balance", record.NewBalance)));

            if (++printed >= MaxPrintedRecords)
                break;
        }

        if (printed == 0)
            return;

        var paper = Spawn("Paper", Transform(ent.Owner).Coordinates);
        _metaData.SetEntityName(paper, Loc.GetString("economy-monitor-report-name"));
        _paper.SetContent(paper, report.ToString());
        _audio.PlayPvs(PrintSound, ent.Owner);
    }

    private static string FormatTime(TimeSpan ts)
        => $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";

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

    /// <summary>
    /// Drops the stored state once the last viewer leaves. A BUI state lives on in the
    /// component and is replicated to everyone who can see the console, so a 500-record
    /// log left behind would be sent to every passer-by for the rest of the round.
    /// </summary>
    private void OnConsoleClosed(Entity<EconomyMonitorConsoleComponent> ent, ref BoundUIClosedEvent args)
    {
        if (_ui.IsUiOpen(ent.Owner, EconomyMonitorUiKey.Key))
            return;

        _pendingRefresh.Remove(ent.Owner);
        _ui.SetUiState(ent.Owner, EconomyMonitorUiKey.Key, null);
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
        _pendingRefresh.Remove(consoleUid);

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
