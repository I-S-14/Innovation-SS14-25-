using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.Components.Apps;
using Content.Shared._IS14.OS.Files;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.UI.Apps;
using Content.Shared.PDA;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._IS14.OS.Apps;

/// <summary>
///     Camera. The client encodes the frame, this only decides whether to keep it: the payload
///     is attacker-controlled, so it is size-capped and checked for a PNG header before it is
///     ever stored or handed to another player's client.
/// </summary>
public sealed class IS14OsCameraSystem : EntitySystem
{
    public const string AppId = "AppCamera";

    private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IS14OsFileSystem _files = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14OsCameraComponent, OsAppGetStateEvent>(OnGetState);
        SubscribeLocalEvent<IS14OsCameraComponent, OsAppEventRaised>(OnAppEvent);
    }

    private void OnGetState(Entity<IS14OsCameraComponent> ent, ref OsAppGetStateEvent args)
    {
        if (args.App != AppId)
            return;

        var photos = 0;
        if (TryComp(ent, out IS14OsMemoryComponent? memory))
        {
            foreach (var file in memory.Files)
            {
                if (file.Kind == OsFileKind.Photo)
                    photos++;
            }
        }

        args.State = new OsCameraState { Photos = photos, Status = ent.Comp.Status };
    }

    private void OnAppEvent(Entity<IS14OsCameraComponent> ent, ref OsAppEventRaised args)
    {
        if (args.App != AppId || args.Event is not OsCameraCaptureEvent capture)
            return;

        if (!TryComp(ent, out IS14OsDeviceComponent? device) || !TryComp(ent, out IS14OsMemoryComponent? memory))
            return;

        if (capture.Data.Length == 0 || capture.Data.Length > ent.Comp.MaxBytes || !IsPng(capture.Data))
        {
            ent.Comp.Status = "is14-os-camera-failed";
            return;
        }

        var time = _timing.CurTime;
        var name = Loc.GetString("is14-os-camera-photo-name", ("time", time.ToString("hh\\:mm\\:ss")));
        var author = CompOrNull<PdaComponent>(ent)?.OwnerName;

        var file = _files.TryAdd((ent.Owner, device, memory),
            name,
            OsFileKind.Photo,
            IS14OsFileSystem.SizeOf(capture.Data.Length),
            author,
            data: capture.Data);

        if (file == null)
        {
            ent.Comp.Status = "is14-os-camera-no-memory";
            return;
        }

        ent.Comp.Status = "is14-os-camera-saved";
        _audio.PlayPvs(ent.Comp.ShutterSound, ent);
    }

    private static bool IsPng(byte[] data)
    {
        if (data.Length < PngSignature.Length)
            return false;

        for (var i = 0; i < PngSignature.Length; i++)
        {
            if (data[i] != PngSignature[i])
                return false;
        }

        return true;
    }
}
