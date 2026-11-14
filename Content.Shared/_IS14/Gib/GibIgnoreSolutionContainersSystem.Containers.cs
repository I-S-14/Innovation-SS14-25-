using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Gibbing.Events;

namespace Content.Shared._IS14.Gib;

public sealed partial class GibIgnoreSolutionContainersSystem
{
    private void ExcludeManagedSolutionContainer(Entity<SolutionManagerComponent> ent, ref AttemptEntityContentsGibEvent args)
    {
        if (args.ExcludedContainers?.Contains(ent.Comp.Container) == true)
            return;

        // Modern solution entities share one internal container. Preserve the
        // caller's exclusions without mutating a list reused for other body parts.
        args.ExcludedContainers = args.ExcludedContainers == null ? [] : new(args.ExcludedContainers);
        args.ExcludedContainers.Add(ent.Comp.Container);
    }
}
