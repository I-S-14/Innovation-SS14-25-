// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.Revolutionary;
using Content.Shared.Revolutionary.Components;
using Content.Shared.StatusIcon.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Revolutionary;

/// <summary>
///     Goob edit - enemies of the revolution get their status icon.
///     Split out of the client RevolutionarySystem when wizden moved that system to Shared.
/// </summary>
public sealed class GoobRevolutionarySystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RevolutionEnemyComponent, GetStatusIconsEvent>(EnemyGetIcon);
    }

    private void EnemyGetIcon(Entity<RevolutionEnemyComponent> ent, ref GetStatusIconsEvent args)
    {
        if (HasComp<RevolutionEnemyComponent>(ent)
        || !HasComp<RevolutionaryComponent>(ent))
            return;

        if (_prototype.TryIndex(ent.Comp.StatusIcon, out var iconPrototype))
            args.StatusIcons.Add(iconPrototype);
    }
}
