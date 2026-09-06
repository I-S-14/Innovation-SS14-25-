using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.Components.Apps;
using Content.Shared._IS14.OS.Prototypes;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.UI.Apps;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.PDA;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._IS14.OS.Apps;

/// <summary>
///     The app store. Downloads take time, and the only thing that can stop one finishing is
///     running out of memory — the platform's single real constraint (Docs §7).
/// </summary>
public sealed class IS14OsAppHubSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IS14OsSystem _os = default!;
    [Dependency] private readonly IS14OsMemorySystem _memory = default!;
    [Dependency] private readonly AccessReaderSystem _access = default!;

    public const string AppId = "AppHub";

    private TimeSpan _nextTick;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14OsAppHubComponent, OsAppGetStateEvent>(OnGetState);
        SubscribeLocalEvent<IS14OsAppHubComponent, OsAppEventRaised>(OnAppEvent);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Downloads are slow enough that a quarter-second tick is plenty, and it keeps the
        // progress bar off the per-frame path.
        var now = _timing.CurTime;
        if (now < _nextTick)
            return;

        var step = 0.25f;
        _nextTick = now + TimeSpan.FromSeconds(step);

        var query = EntityQueryEnumerator<IS14OsAppHubComponent, IS14OsDeviceComponent, IS14OsMemoryComponent>();
        while (query.MoveNext(out var uid, out var hub, out var device, out var memory))
        {
            if (hub.Downloading is not { } app)
                continue;

            if (!_proto.TryIndex(app, out var proto))
            {
                Abort(hub, null);
                continue;
            }

            hub.Downloaded += hub.Speed * step;

            if (hub.Downloaded < proto.Size)
            {
                _os.MarkDirty(uid);
                continue;
            }

            Finish((uid, hub, device, memory), app);
            _os.UpdateUi(uid, device);
        }
    }

    private void Finish(Entity<IS14OsAppHubComponent, IS14OsDeviceComponent, IS14OsMemoryComponent> ent,
        ProtoId<IS14OsAppPrototype> app)
    {
        if (!_memory.Install((ent.Owner, ent.Comp2, ent.Comp3), app))
        {
            // Almost always "no room left" — something else got installed while this downloaded.
            Abort(ent.Comp1, "is14-os-hub-error-memory");
            return;
        }

        Abort(ent.Comp1, null);
    }

    private static void Abort(IS14OsAppHubComponent hub, string? error)
    {
        hub.Downloading = null;
        hub.Downloaded = 0f;
        hub.Error = error;
    }

    private void OnGetState(Entity<IS14OsAppHubComponent> ent, ref OsAppGetStateEvent args)
    {
        if (args.App != AppId)
            return;

        var state = new OsAppHubState
        {
            Downloading = ent.Comp.Downloading,
            Error = ent.Comp.Error,
        };

        if (ent.Comp.Downloading is { } app && _proto.TryIndex(app, out var downloading) && downloading.Size > 0)
            state.Progress = Math.Clamp(ent.Comp.Downloaded / downloading.Size, 0f, 1f);

        if (!TryComp(ent, out IS14OsDeviceComponent? device))
            return;

        var flags = _memory.GetDeviceFlags(device);
        var access = GetAccessTags(ent);

        foreach (var proto in _proto.EnumeratePrototypes<IS14OsAppPrototype>())
        {
            if ((proto.Source & OsAppSource.NtStore) == 0 || (proto.DeviceFlags & flags) == 0)
                continue;

            state.Catalog.Add(proto.ID);

            if (!HasAccess(proto, access))
                state.AccessDenied.Add(proto.ID);
        }

        args.State = state;
    }

    private void OnAppEvent(Entity<IS14OsAppHubComponent> ent, ref OsAppEventRaised args)
    {
        if (args.App != AppId)
            return;

        switch (args.Event)
        {
            case OsHubDownloadEvent download:
                TryStart(ent, download.App);
                break;

            case OsHubCancelEvent:
                Abort(ent.Comp, null);
                break;

            case OsHubDismissErrorEvent:
                ent.Comp.Error = null;
                break;
        }
    }

    private void TryStart(Entity<IS14OsAppHubComponent> ent, ProtoId<IS14OsAppPrototype> app)
    {
        if (ent.Comp.Downloading != null)
            return;

        if (!TryComp(ent, out IS14OsDeviceComponent? device) || !TryComp(ent, out IS14OsMemoryComponent? memory))
            return;

        if (!_proto.TryIndex(app, out var proto))
            return;

        // Everything the client could have lied about, re-checked here.
        if ((proto.Source & OsAppSource.NtStore) == 0
            || (proto.DeviceFlags & _memory.GetDeviceFlags(device)) == 0
            || _memory.IsInstalled(memory, app))
            return;

        if (!HasAccess(proto, GetAccessTags(ent)))
        {
            ent.Comp.Error = "is14-os-hub-error-access";
            return;
        }

        if (proto.Size > _memory.GetFreeMemory((ent.Owner, device, memory)))
        {
            ent.Comp.Error = "is14-os-hub-error-memory";
            return;
        }

        ent.Comp.Downloading = app;
        ent.Comp.Downloaded = 0f;
        ent.Comp.Error = null;
    }

    /// <summary>
    ///     The device's credentials are the ID card sitting in it, not the person holding it:
    ///     handing someone your PDA hands them what it can download.
    /// </summary>
    private ICollection<ProtoId<AccessLevelPrototype>> GetAccessTags(EntityUid device)
    {
        if (TryComp(device, out PdaComponent? pda) && pda.ContainedId is { } id)
            return _access.FindAccessTags(id);

        return Array.Empty<ProtoId<AccessLevelPrototype>>();
    }

    private static bool HasAccess(IS14OsAppPrototype app, ICollection<ProtoId<AccessLevelPrototype>> tags)
    {
        var required = app.DownloadAccess.Count > 0 ? app.DownloadAccess : app.RequiredAccess;

        if (required.Count == 0)
            return true;

        foreach (var level in required)
        {
            if (tags.Contains(level))
                return true;
        }

        return false;
    }
}
