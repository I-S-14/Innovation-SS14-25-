using Content.Shared.EntityTable.EntitySelectors;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.StationEvents.Components;
using Robust.Shared.Prototypes;

namespace Content.Server.StationEvents;

public sealed partial class EventManagerSystem
{
    public EventTable AvailableEvents(bool ignoreTimings, int? players, TimeSpan? time, float reoccurrenceMult)
    {
        if (!ignoreTimings)
            return AvailableEvents(players, time, reoccurrenceMult);
        var events = AllEvents().Select(pair => (pair.Key, pair.Value.Weight)).ToList();
        return new EventTable(events.AsReadOnly(), events.Sum(pair => (double) pair.Weight));
    }

    public bool TryBuildLimitedEvents(EntityTableSelector table, EventTable available,
        [NotNullWhen(true)] out Dictionary<EntityPrototype, StationEventComponent>? result) =>
        TryBuildLimitedEvents(_entityTable.GetSpawns(table), available, out result);

    public bool TryBuildLimitedEvents(IEnumerable<EntProtoId> selected, EventTable available,
        [NotNullWhen(true)] out Dictionary<EntityPrototype, StationEventComponent>? result)
    {
        var allowed = selected.ToHashSet();
        result = new Dictionary<EntityPrototype, StationEventComponent>();
        foreach (var (prototype, _) in available.Events)
        {
            if (allowed.Contains(prototype.ID) && prototype.TryGetComponent<StationEventComponent>(out var component, Factory))
                result.Add(prototype, component);
        }
        return result.Count > 0;
    }
}
