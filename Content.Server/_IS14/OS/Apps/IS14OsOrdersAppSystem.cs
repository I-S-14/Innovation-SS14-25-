using Content.Server.Cargo.Systems;
using Content.Shared.Cargo.Components;
using Content.Shared.Cargo.Events;
using Content.Shared.Cargo;
using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.UI.Apps;
using Robust.Shared.Timing;

namespace Content.Server._IS14.OS.Apps;

/// <summary>
///     Department orders as an OS application (Docs §12.2).
///
///     Unlike the economy consoles, the ordering rules live upstream, so nothing about them is
///     touched: the app asks <see cref="CargoSystem"/> for the very state the wall console gets,
///     and sends the console's own messages back as directed events — which is exactly how the
///     engine delivers a BUI message anyway, <c>Subs.BuiEvents</c> being a directed subscription
///     with a UI-key filter. One upstream method had to become public to build the state; that
///     is the whole footprint outside <c>_IS14/</c>.
/// </summary>
public sealed class IS14OsOrdersAppSystem : EntitySystem
{
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IS14OsSystem _os = default!;

    public const string AppId = "AppOrders";

    /// <summary>
    ///     Orders change from the shuttle, the pallets and other consoles, none of which report
    ///     back. A slow poll is cheaper than teaching upstream to notify us.
    /// </summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(2);

    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CargoOrderConsoleComponent, OsAppGetStateEvent>(OnGetState);
        SubscribeLocalEvent<CargoOrderConsoleComponent, OsAppEventRaised>(OnAppEvent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + RefreshInterval;

        var query = EntityQueryEnumerator<CargoOrderConsoleComponent, IS14OsDeviceComponent>();
        while (query.MoveNext(out var uid, out _, out var device))
        {
            if (device.Open.Contains(AppId))
                _os.MarkDirty(uid);
        }
    }

    private void OnGetState(Entity<CargoOrderConsoleComponent> ent, ref OsAppGetStateEvent args)
    {
        if (args.App != AppId)
            return;

        args.State = new OsConsoleState(_cargo.BuildOrderState(ent.Owner));
    }

    private void OnAppEvent(Entity<CargoOrderConsoleComponent> ent, ref OsAppEventRaised args)
    {
        if (args.App != AppId || args.Event is not OsConsoleMessageEvent wrapped)
            return;

        // Only the three order messages are forwarded: anything else the client dreamed up must
        // not reach an upstream handler through a door the OS opened.
        if (wrapped.Message is not (CargoConsoleAddOrderMessage
            or CargoConsoleApproveOrderMessage
            or CargoConsoleRemoveOrderMessage))
        {
            return;
        }

        // Restore what a real BUI delivery would have stamped on: who pressed it, and which
        // interface it claims to be, since the upstream handlers filter on the key.
        wrapped.Message.Actor = args.Actor;
        wrapped.Message.UiKey = CargoConsoleUiKey.Orders;
        wrapped.Message.Entity = GetNetEntity(ent.Owner);

        var message = wrapped.Message;
        RaiseLocalEvent(ent.Owner, (object) message);
    }
}
