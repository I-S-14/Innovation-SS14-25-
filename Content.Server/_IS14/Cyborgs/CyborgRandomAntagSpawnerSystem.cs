// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Server.Antag;
using Content.Shared.Random.Helpers;
using Robust.Shared.Random;

namespace Content.Server._IS14.Cyborgs;

public sealed class CyborgRandomAntagSpawnerSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgRandomAntagSpawnerComponent, AntagSelectEntityEvent>(OnSelectEntity);
    }

    private void OnSelectEntity(Entity<CyborgRandomAntagSpawnerComponent> ent, ref AntagSelectEntityEvent args)
    {
        if (args.Handled || ent.Comp.Prototypes.Count == 0)
            return;

        args.Entity = Spawn(_random.Pick(ent.Comp.Prototypes));
    }
}
