using Content.Server._IS14.Economy.Gosplan;
using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.UI.Apps;
using Robust.Shared.Timing;

namespace Content.Server._IS14.OS.Apps;

/// <summary>
///     The plan board as an OS application (Docs §12.2). Read-only, like the board it came
///     from — nobody edits the plan, they only work it — so this has no message path at all.
/// </summary>
public sealed class IS14OsGosplanAppSystem : EntitySystem
{
    [Dependency] private readonly GosplanConsoleSystem _gosplan = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IS14OsSystem _os = default!;

    public const string AppId = "AppGosplan";

    /// <summary>Progress moves continuously, so the board is pushed on a timer, not on events.</summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GosplanConsoleComponent, OsAppGetStateEvent>(OnGetState);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + RefreshInterval;

        var query = EntityQueryEnumerator<GosplanConsoleComponent, IS14OsDeviceComponent>();
        while (query.MoveNext(out var uid, out _, out var device))
        {
            if (device.Open.Contains(AppId))
                _os.MarkDirty(uid);
        }
    }

    private void OnGetState(Entity<GosplanConsoleComponent> ent, ref OsAppGetStateEvent args)
    {
        if (args.App != AppId)
            return;

        args.State = new OsConsoleState(_gosplan.BuildState(ent.Owner));
    }
}
