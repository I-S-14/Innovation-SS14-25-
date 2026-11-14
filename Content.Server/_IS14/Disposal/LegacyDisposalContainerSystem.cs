using System.Linq;
using Content.Shared.Containers;
using Content.Shared.Disposal.Components;
using Robust.Shared.Containers;

namespace Content.Server._IS14.Disposal;

/// <summary>Preserves contents of disposal containers saved under previous names.</summary>
public sealed class LegacyDisposalContainerSystem : EntitySystem
{
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    private static readonly string[] LegacyIds = ["disposals", "DisposalUnit"];

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DisposalUnitComponent, MapInitEvent>(OnMapInit,
            after: [typeof(ContainerFillSystem)]);
    }

    private void OnMapInit(Entity<DisposalUnitComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Container is not { } destination)
        {
            Log.Error($"Missing disposal container on {ToPrettyString(ent)} during legacy migration.");
            return;
        }

        foreach (var id in LegacyIds)
        {
            if (!_containers.TryGetContainer(ent.Owner, id, out var legacy))
                continue;

            foreach (var item in legacy.ContainedEntities.ToArray())
            {
                // Existing map contents must survive even if insertion rules changed.
                if (!_containers.Insert(item, destination, force: true))
                    Log.Error($"Failed to migrate {ToPrettyString(item)} from legacy disposal container {id} on {ToPrettyString(ent)}.");
            }
        }
    }
}
