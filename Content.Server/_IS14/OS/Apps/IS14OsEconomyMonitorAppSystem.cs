using Content.Server._IS14.Economy.EconomyMonitor;
using Content.Shared._IS14.Economy.EconomyMonitor;
using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.UI.Apps;

namespace Content.Server._IS14.OS.Apps;

/// <summary>
///     The economy monitor as an OS application (Docs §12.2). The log and the vending map both
///     come from <see cref="EconomyMonitorSystem"/> exactly as the wall console gets them; the
///     only thing that changed is which window they are drawn in.
/// </summary>
public sealed class IS14OsEconomyMonitorAppSystem : EntitySystem
{
    [Dependency] private readonly EconomyMonitorSystem _monitor = default!;
    [Dependency] private readonly IS14OsSystem _os = default!;

    public const string AppId = "AppEconomyMonitor";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<EconomyMonitorConsoleComponent, OsAppGetStateEvent>(OnGetState);
        SubscribeLocalEvent<EconomyMonitorConsoleComponent, OsAppEventRaised>(OnAppEvent);
        SubscribeLocalEvent<EconomyTransactionEvent>(OnTransaction);
    }

    private void OnGetState(Entity<EconomyMonitorConsoleComponent> ent, ref OsAppGetStateEvent args)
    {
        if (args.App != AppId)
            return;

        // A null state is honest: it means no monitor server answers on this network, which is
        // exactly what the standalone console shows by staying blank.
        args.State = new OsConsoleState(_monitor.BuildState(ent));
    }

    private void OnAppEvent(Entity<EconomyMonitorConsoleComponent> ent, ref OsAppEventRaised args)
    {
        if (args.App != AppId || args.Event is not OsConsoleMessageEvent wrapped)
            return;

        wrapped.Message.Actor = args.Actor;
        _monitor.HandleMessage(ent, wrapped.Message);
    }

    private void OnTransaction(EconomyTransactionEvent args)
    {
        var query = EntityQueryEnumerator<EconomyMonitorConsoleComponent, IS14OsDeviceComponent>();
        while (query.MoveNext(out var uid, out _, out var device))
        {
            if (device.Open.Contains(AppId))
                _os.MarkDirty(uid);
        }
    }
}
