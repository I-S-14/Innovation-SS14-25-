using Content.Client.Atmos.Components;
using Content.Client.Items.Systems;
using Content.Shared._IS14.Atmos;
using Content.Shared.Hands;
using Content.Shared.Item;
using Robust.Client.GameObjects;

namespace Content.Client._IS14.Atmos;

public sealed partial class PipeItemColorSystem : EntitySystem
{
    [Dependency] private readonly SharedItemSystem _items = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<PipeColorVisualsComponent, GetInhandVisualsEvent>(OnInhand, after: [typeof(ItemSystem)]);
        SubscribeLocalEvent<PipeColorVisualsComponent, StoredItemColorEvent>(OnStored);
        SubscribeLocalEvent<AtmosPipeColorComponent, AppearanceChangeEvent>(OnAppearance);
    }

    private void OnInhand(Entity<PipeColorVisualsComponent> ent, ref GetInhandVisualsEvent args)
    {
        if (!TryComp<AtmosPipeColorComponent>(ent, out var pipe))
            return;
        foreach (var (_, layer) in args.Layers)
            layer.Color = pipe.Color;
    }

    private void OnStored(Entity<PipeColorVisualsComponent> ent, ref StoredItemColorEvent args)
    {
        if (TryComp<AtmosPipeColorComponent>(ent, out var pipe))
            args.Color = pipe.Color;
    }

    private void OnAppearance(Entity<AtmosPipeColorComponent> ent, ref AppearanceChangeEvent args)
    {
        _items.VisualsChanged(ent);
    }
}
