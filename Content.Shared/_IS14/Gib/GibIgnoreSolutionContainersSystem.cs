using Content.Shared.Chemistry.Components.SolutionManager;
using Content.Shared.Gibbing.Events;

namespace Content.Shared._IS14.Gib;

public sealed partial class GibIgnoreSolutionContainersSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SolutionManagerComponent, AttemptEntityContentsGibEvent>(OnAttempt);
    }

    private void OnAttempt(Entity<SolutionManagerComponent> ent, ref AttemptEntityContentsGibEvent args)
    {
        ExcludeManagedSolutionContainer(ent, ref args); //IS14-change: modern solutions use a shared internal container.
    }
}
