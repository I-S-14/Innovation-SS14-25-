// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Atmos.Rotting;
using Content.Shared.Inventory.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Overlays;
using Content.Shared.StatusIcon;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Damage.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Overlays;

/// <summary>
/// Shows a healthy icon on mobs.
/// </summary>
public sealed partial class ShowHealthIconsSystem : EquipmentHudSystem<ShowHealthIconsComponent> // Goob - made partial
{
    [ViewVariables]
    public HashSet<string> DamageContainers = new();

    protected override bool WorksInHands => true; // Goobstation

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamageableComponent, GetStatusIconsEvent>(OnGetStatusIconsEvent);
        SubscribeLocalEvent<ShowHealthIconsComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    protected override void UpdateInternal(RefreshEquipmentHudEvent<ShowHealthIconsComponent> component)
    {
        base.UpdateInternal(component);

        DamageContainers.Clear();
        foreach (var comp in component.Components)
        {
            foreach (var damageContainerId in comp.DamageContainers)
            {
                DamageContainers.Add(damageContainerId);
            }
        }
    }

    protected override void DeactivateInternal()
    {
        base.DeactivateInternal();

        DamageContainers.Clear();
    }

    private void OnHandleState(Entity<ShowHealthIconsComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        RefreshOverlay();
    }

    private void OnGetStatusIconsEvent(Entity<DamageableComponent> entity, ref GetStatusIconsEvent args)
    {
        if (!IsActive)
            return;

        var healthIcons = DecideHealthIcons(entity);

        args.StatusIcons.AddRange(healthIcons);
    }

    private IReadOnlyList<StatusIconPrototype> DecideHealthIcons(Entity<DamageableComponent> entity)
    {
        var injurableComp = entity.Comp;

        if (injurableComp.DamageContainerID == null ||
            !DamageContainers.Contains(injurableComp.DamageContainerID))
        {
            return Array.Empty<StatusIconPrototype>();
        }

        var result = new List<StatusIconPrototype>();

        // Here you could check health status, diseases, mind status, etc. and pick a good icon, or multiple depending on whatever.
        if (injurableComp?.DamageContainerID == "Biological")
        {
            if (TryComp<MobStateComponent>(entity, out var state))
            {
                // Since there is no MobState for a rotting mob, we have to deal with this case first.
                if (HasComp<RottingComponent>(entity) && ProtoMan.Resolve(injurableComp.RottingIcon, out var rottingIcon))
                    result.Add(rottingIcon);
                else if (injurableComp.HealthIcons.TryGetValue(state.CurrentState, out var value) && ProtoMan.Resolve(value, out var icon))
                    result.Add(icon);
            }
        }

        return result;
    }
}
