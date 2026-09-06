using Content.Shared._IS14.OS.Components.Apps;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.UI.Apps;

namespace Content.Server._IS14.OS.Apps;

/// <summary>
///     Notes: the smallest app that actually owns data, and therefore the one that proves the
///     "components on the device" model works end to end.
/// </summary>
public sealed class IS14OsNotesSystem : EntitySystem
{
    public const string AppId = "AppNotes";

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

        args.State = new OsNotesState(ent.Comp.Text);
    }

    private void OnAppEvent(Entity<IS14OsNotesComponent> ent, ref OsAppEventRaised args)
    {
        if (args.App != AppId || args.Event is not OsNotesSaveEvent save)
            return;

        var text = save.Text;
        if (text.Length > ent.Comp.MaxLength)
            text = text[..ent.Comp.MaxLength];

        ent.Comp.Text = text;
    }
}
