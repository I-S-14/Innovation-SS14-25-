using Content.Server.Station.Systems;
using Content.Shared._IS14.OS.Components.Apps;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.UI.Apps;
using Content.Shared.MassMedia.Components;
using Content.Shared.MassMedia.Systems;

namespace Content.Server._IS14.OS.Apps;

/// <summary>
///     The station press, read off the same <see cref="StationNewsComponent"/> the news writer
///     console publishes into. Nothing is copied onto the device: the paper is the station's,
///     and a device that leaves the station stops being able to read it.
/// </summary>
public sealed class IS14OsNewsSystem : EntitySystem
{
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IS14OsSystem _os = default!;

    public const string AppId = "AppNews";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14OsNewsComponent, OsAppGetStateEvent>(OnGetState);
        SubscribeLocalEvent<IS14OsNewsComponent, OsAppEventRaised>(OnAppEvent);
        SubscribeLocalEvent<IS14OsNewsComponent, OsAppClosedEvent>(OnClosed);
    }

    private void OnGetState(Entity<IS14OsNewsComponent> ent, ref OsAppGetStateEvent args)
    {
        if (args.App != AppId)
            return;

        var state = new OsNewsState();
        var articles = GetArticles(ent);

        foreach (var article in articles)
        {
            state.Headlines.Add(new OsNewsHeadline
            {
                Title = article.Title,
                Author = article.Author,
                Published = article.ShareTime,
            });
        }

        if (ent.Comp.Reading is { } index && index >= 0 && index < articles.Count)
        {
            state.Reading = index;
            state.Body = articles[index].Content;
        }
        else
        {
            // Nothing open means the player is looking at the list, so the list is now seen.
            ent.Comp.SeenCount = articles.Count;
        }

        state.Unread = Math.Max(0, articles.Count - ent.Comp.SeenCount);
        args.State = state;
    }

    private void OnAppEvent(Entity<IS14OsNewsComponent> ent, ref OsAppEventRaised args)
    {
        if (args.App != AppId || args.Event is not OsNewsReadEvent read)
            return;

        var count = GetArticles(ent).Count;

        ent.Comp.Reading = read.Article is { } index && index >= 0 && index < count
            ? index
            : null;
    }

    /// <summary>Closing the app puts the reader back on the list, not on last shift's article.</summary>
    private void OnClosed(Entity<IS14OsNewsComponent> ent, ref OsAppClosedEvent args)
    {
        if (args.App == AppId)
            ent.Comp.Reading = null;
    }

    private List<NewsArticle> GetArticles(EntityUid device)
    {
        // A device in a bag can lose its own grid; whoever is carrying it has not.
        var station = _station.GetOwningStation(device)
            ?? _station.GetOwningStation(_os.FindCarrier(device));

        if (station == null || !TryComp(station, out StationNewsComponent? news))
            return new List<NewsArticle>();

        return news.Articles;
    }
}
