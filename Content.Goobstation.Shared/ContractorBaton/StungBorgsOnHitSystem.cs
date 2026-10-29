// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Jittering;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Stunnable;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared._IS14.Cyborgs.Components; // IS14-change

namespace Content.Goobstation.Shared.ContractorBaton;

public sealed class StungBorgsOnHitSystem : EntitySystem
{
    [Dependency] private readonly ItemToggleSystem _toggle = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<StunBorgsOnHitComponent, MeleeHitEvent>(OnHit);
    }

    private void OnHit(Entity<StunBorgsOnHitComponent> ent, ref MeleeHitEvent args)
    {
        if (!_toggle.IsActivated(ent.Owner))
            return;

        //IS14-change start - tgstation-style cyborgs count as borgs
        foreach (var borg in args.HitEntities.Where(e => (HasComp<BorgChassisComponent>(e) || HasComp<CyborgComponent>(e)) || HasComp<CyborgComponent>(e)))
        //IS14-change end
        {
            _stun.TryUpdateParalyzeDuration(borg, ent.Comp.ParalyzeDuration);
            _jitter.DoJitter(borg, ent.Comp.ParalyzeDuration, true);
        }
    }
}
