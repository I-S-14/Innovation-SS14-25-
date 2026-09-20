using Content.Shared._IS14.OS.Components.Apps;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.UI.Apps;

namespace Content.Server._IS14.OS.Apps;

/// <summary>
///     The to-do list. Entirely local to the device — no station, no network, nothing to
///     sabotage. It exists because half of what a shift asks of a player is remembering the
///     other half.
/// </summary>
public sealed class IS14OsTasksSystem : EntitySystem
{
    public const string AppId = "AppTasks";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14OsTasksComponent, OsAppGetStateEvent>(OnGetState);
        SubscribeLocalEvent<IS14OsTasksComponent, OsAppEventRaised>(OnAppEvent);
    }

    private void OnGetState(Entity<IS14OsTasksComponent> ent, ref OsAppGetStateEvent args)
    {
        if (args.App != AppId)
            return;

        var state = new OsTasksState
        {
            Full = ent.Comp.Tasks.Count >= ent.Comp.MaxTasks,
        };

        foreach (var task in ent.Comp.Tasks)
        {
            state.Tasks.Add(new OsTaskEntry
            {
                Id = task.Id,
                Text = task.Text,
                Done = task.Done,
            });
        }

        args.State = state;
    }

    private void OnAppEvent(Entity<IS14OsTasksComponent> ent, ref OsAppEventRaised args)
    {
        if (args.App != AppId)
            return;

        switch (args.Event)
        {
            case OsTaskAddEvent add:
                Add(ent, add.Text);
                break;

            case OsTaskToggleEvent toggle:
                if (Find(ent.Comp, toggle.Task) is { } task)
                    task.Done = !task.Done;

                break;

            case OsTaskRemoveEvent remove:
                if (remove.Task is { } id)
                    ent.Comp.Tasks.RemoveAll(t => t.Id == id);
                else
                    ent.Comp.Tasks.RemoveAll(t => t.Done);

                break;
        }
    }

    private static void Add(Entity<IS14OsTasksComponent> ent, string text)
    {
        text = text.Trim();
        if (text.Length == 0)
            return;

        if (text.Length > ent.Comp.MaxLength)
            text = text[..ent.Comp.MaxLength];

        if (ent.Comp.Tasks.Count >= ent.Comp.MaxTasks)
        {
            // Drop the oldest thing already done rather than refusing the new one. Only a list
            // with nothing crossed off can actually be full.
            var finished = ent.Comp.Tasks.FindIndex(t => t.Done);
            if (finished < 0)
                return;

            ent.Comp.Tasks.RemoveAt(finished);
        }

        ent.Comp.Tasks.Add(new OsTask
        {
            Id = ent.Comp.NextId++,
            Text = text,
        });
    }

    private static OsTask? Find(IS14OsTasksComponent tasks, int id)
    {
        foreach (var task in tasks.Tasks)
        {
            if (task.Id == id)
                return task;
        }

        return null;
    }
}
