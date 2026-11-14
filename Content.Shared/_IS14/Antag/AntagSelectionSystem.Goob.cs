using Content.Shared.Antag.Components;
using Content.Shared.Antag.Selectors;

namespace Content.Shared.Antag;

public abstract partial class AntagSelectionSystem
{
    public void SetAntagCounts(Entity<AntagSelectionComponent> entity, AntagCountSelector[] counts)
    {
        entity.Comp.Antags = counts;
    }
}
