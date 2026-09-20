using Content.Server._IS14.Economy.Treasury;
using Content.Shared._IS14.Economy.EconomyMonitor;
using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.UI.Apps;

namespace Content.Server._IS14.OS.Apps;

/// <summary>
///     The treasury as an OS application (Docs §12.2). A view onto
///     <see cref="TreasuryConsoleSystem"/>: no allocation rule is restated here.
/// </summary>
public sealed class IS14OsTreasuryAppSystem : EntitySystem
{
    [Dependency] private readonly TreasuryConsoleSystem _treasury = default!;
    [Dependency] private readonly IS14OsSystem _os = default!;

    public const string AppId = "AppTreasury";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TreasuryConsoleComponent, OsAppGetStateEvent>(OnGetState);
        SubscribeLocalEvent<TreasuryConsoleComponent, OsAppEventRaised>(OnAppEvent);
        SubscribeLocalEvent<EconomyTransactionEvent>(OnTransaction);
    }

    private void OnGetState(Entity<TreasuryConsoleComponent> ent, ref OsAppGetStateEvent args)
    {
        if (args.App != AppId)
            return;

        args.State = new OsConsoleState(_treasury.BuildState(ent.Owner));
    }

    private void OnAppEvent(Entity<TreasuryConsoleComponent> ent, ref OsAppEventRaised args)
    {
        if (args.App != AppId || args.Event is not OsConsoleMessageEvent wrapped)
            return;

        wrapped.Message.Actor = args.Actor;
        _treasury.HandleMessage(ent, wrapped.Message);
    }

    private void OnTransaction(EconomyTransactionEvent args)
    {
        var query = EntityQueryEnumerator<TreasuryConsoleComponent, IS14OsDeviceComponent>();
        while (query.MoveNext(out var uid, out _, out var device))
        {
            if (device.Open.Contains(AppId))
                _os.MarkDirty(uid);
        }
    }
}
