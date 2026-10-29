// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Containers;
using Robust.Shared.Timing;
using Robust.Shared.Prototypes;

namespace Content.Shared.Containers;

/// <summary>
/// Applies / removes an entity prototype from a child entity when it's inserted into a container.
/// </summary>
public sealed partial class ContainerCompSystem : EntitySystem
{
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ContainerCompComponent, EntInsertedIntoContainerMessage>(OnConInsert);
        SubscribeLocalEvent<ContainerCompComponent, EntRemovedFromContainerMessage>(OnConRemove);
    }

    private void OnConRemove(Entity<ContainerCompComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.Container || _timing.ApplyingState)
            return;

        if (ProtoMan.Resolve(ent.Comp.Proto, out var entProto))
        {
            EntityManager.RemoveComponents(args.Entity, entProto.Components);
        }
    }

    private void OnConInsert(Entity<ContainerCompComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID != ent.Comp.Container || _timing.ApplyingState)
            return;

        if (ProtoMan.Resolve(ent.Comp.Proto, out var entProto))
        {
            EntityManager.AddComponents(args.Entity, entProto.Components);
        }
    }
}