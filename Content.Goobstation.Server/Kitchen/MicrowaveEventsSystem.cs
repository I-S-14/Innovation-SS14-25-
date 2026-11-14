using Content.Shared.Kitchen.Components;
// SPDX-License-Identifier: AGPL-3.0-or-later


using Robust.Shared.Containers;

namespace Content.Goobstation.Server.Kitchen;

/// <summary>
/// Prevents automation taking items out of an active microwave.
/// Only exists because microwave supercode only prevents it in interaction, not attempt events.
/// </summary>
public sealed partial class MicrowaveEventsSystem : EntitySystem
{
    [Dependency] private Robust.Shared.Timing.IGameTiming _timing = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ActiveMicrowaveComponent, ContainerIsRemovingAttemptEvent>(OnRemoveAttempt);
    }

    private void OnRemoveAttempt(Entity<ActiveMicrowaveComponent> ent, ref ContainerIsRemovingAttemptEvent args)
    {
        if (ent.Comp.CookTimeEnd > _timing.CurTime)
            args.Cancel();
    }
}
