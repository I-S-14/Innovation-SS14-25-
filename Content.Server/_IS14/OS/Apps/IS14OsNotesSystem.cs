using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.Components.Apps;
using Content.Shared._IS14.OS.Files;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.UI.Apps;
using Content.Shared.PDA;

namespace Content.Server._IS14.OS.Apps;

/// <summary>
///     Notes: the smallest app that actually owns data, and therefore the one that proves the
///     "components on the device" model works end to end. Exporting turns a note into a file,
///     which is the only form the messenger can send.
/// </summary>
public sealed class IS14OsNotesSystem : EntitySystem
{
    public const string AppId = "AppNotes";

    private const int MaxNameLength = 32;

    [Dependency] private readonly IS14OsFileSystem _files = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14OsNotesComponent, OsAppGetStateEvent>(OnGetState);
        SubscribeLocalEvent<IS14OsNotesComponent, OsAppEventRaised>(OnAppEvent);
    }

    private void OnGetState(Entity<IS14OsNotesComponent> ent, ref OsAppGetStateEvent args)
    {
        if (args.App != AppId)
            return;

        args.State = new OsNotesState(ent.Comp.Text, ent.Comp.Status);
    }

    private void OnAppEvent(Entity<IS14OsNotesComponent> ent, ref OsAppEventRaised args)
    {
        if (args.App != AppId)
            return;

        switch (args.Event)
        {
            case OsNotesSaveEvent save:
                var text = save.Text;
                if (text.Length > ent.Comp.MaxLength)
                    text = text[..ent.Comp.MaxLength];

                ent.Comp.Text = text;
                ent.Comp.Status = null;
                break;

            case OsNotesExportEvent export:
                Export(ent, export.Name);
                break;
        }
    }

    private void Export(Entity<IS14OsNotesComponent> ent, string name)
    {
        if (ent.Comp.Text.Length == 0)
        {
            ent.Comp.Status = "is14-os-notes-export-empty";
            return;
        }

        if (!TryComp(ent, out IS14OsDeviceComponent? device) || !TryComp(ent, out IS14OsMemoryComponent? memory))
            return;

        name = name.Trim();
        if (name.Length == 0)
            name = Loc.GetString("is14-os-notes-export-default");
        else if (name.Length > MaxNameLength)
            name = name[..MaxNameLength];

        var size = IS14OsFileSystem.SizeOf(ent.Comp.Text.Length);
        var author = CompOrNull<PdaComponent>(ent)?.OwnerName;

        var file = _files.TryAdd((ent.Owner, device, memory), name, OsFileKind.Text, size, author, ent.Comp.Text);

        ent.Comp.Status = file == null ? "is14-os-notes-export-no-memory" : "is14-os-notes-export-done";
    }
}
