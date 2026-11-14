using System.Linq;
using Content.Server.Body.Components;
using Content.Shared.Body.Systems;
using Content.Shared.Mobs.Components;
using Content.Shared.Weapons.Melee.Events;
using Content.Shared.Weapons.Melee.Metabolizer;

namespace Content.Server._IS14.Weapons.Melee;

/// <summary>Resolve metabolism-dependent weapon damage using Goob's actual organs.</summary>
public sealed partial class BonusDamageOnMetabolismSystem : EntitySystem
{
    [Dependency] private readonly SharedBodySystem _body = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BonusDamageOnMetabolismComponent, MeleeHitEvent>(OnHit);
    }

    private void OnHit(EntityUid uid, BonusDamageOnMetabolismComponent component, MeleeHitEvent args)
    {
        if (component.SelectedMetabolizer is not { } selected)
            return;

        foreach (var target in args.HitEntities)
        {
            if (TryComp<MobStateComponent>(target, out var state) &&
                !component.ValidMobStates.Contains(state.CurrentState))
                continue;

            if (!_body.GetBodyOrganEntityComps<MetabolizerComponent>((target, null))
                    .Any(organ => organ.Comp1.MetabolizerTypes?.Contains(selected) == true))
                continue;

            args.BonusDamage += component.Damage;
            return;
        }
    }
}
