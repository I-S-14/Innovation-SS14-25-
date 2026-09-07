using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.Components.Apps;
using Content.Shared._IS14.OS.Files;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.UI.Apps;
using Content.Shared.PDA;

namespace Content.Server._IS14.OS.Apps;

/// <summary>
///     The document editor. The smallest app that actually owns data, and therefore the one
///     that proves the "components on the device" model works end to end. Formatting lives in
///     the text itself as engine markup, so the server stores one string and never has to know
///     what bold is. Exporting turns the document into a file, which is the only form the
///     messenger can send.
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

        args.State = new OsNotesState(ent.Comp.Text, ent.Comp.Title, ent.Comp.Status);
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
                ent.Comp.Title = Clamp(save.Title);
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

        // The client sends the document's name; fall back to the saved one, then to a default.
        name = Clamp(name);
        if (name.Length == 0)
            name = ent.Comp.Title;
        if (name.Length == 0)
            name = Loc.GetString("is14-os-notes-export-default");

        var size = IS14OsFileSystem.SizeOf(ent.Comp.Text.Length);
        var author = CompOrNull<PdaComponent>(ent)?.OwnerName;

        var file = _files.TryAdd((ent.Owner, device, memory), name, OsFileKind.Text, size, author, ent.Comp.Text);

        ent.Comp.Status = file == null ? "is14-os-notes-export-no-memory" : "is14-os-notes-export-done";
    }

    /// <summary>A name is one short line: never trust the client for its length or its breaks.</summary>
    private static string Clamp(string name)
    {
        name = name.ReplaceLineEndings(" ").Trim();

        return name.Length > MaxNameLength ? name[..MaxNameLength] : name;
    }
}
