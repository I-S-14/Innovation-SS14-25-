// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Damage;
using Content.Shared.Mobs;

namespace Content.Shared._IS14.Cyborgs.Systems;

public abstract partial class SharedCyborgSystem
{
    private void InitializeSlots()
    {
        SubscribeLocalEvent<CyborgComponent, DamageChangedEvent>(OnDamageChanged);
    }

    /// <summary>
    /// tgstation's breakable module slots: chassis damage knocks the slots offline from the last one,
    /// repairs bring them back.
    /// </summary>
    private void OnDamageChanged(Entity<CyborgComponent> ent, ref DamageChangedEvent args)
    {
        // Damage is not predicted, neither are the alarms.
        if (_net.IsClient || !_mobThreshold.TryGetThresholdForState(ent.Owner, MobState.Critical, out var crit))
            return;

        var damage = args.Damageable.TotalDamage.Float();
        var broken = 0;
        foreach (var fraction in ent.Comp.SlotBreakFractions)
        {
            if (damage >= fraction * crit.Value.Float())
                broken++;
        }

        broken = Math.Min(broken, ent.Comp.ToolSlots);
        if (broken == ent.Comp.BrokenSlots)
            return;

        var old = ent.Comp.BrokenSlots;
        ent.Comp.BrokenSlots = broken;
        Dirty(ent);

        if (broken <= old)
            return;

        for (var i = ent.Comp.ToolSlots - broken; i < ent.Comp.ToolSlots - old; i++)
        {
            if (_hands.TryGetHeldItem(ent.Owner, GetToolHandId(ent.Comp, i), out var held))
                ClearSlot(ent, held.Value);
        }

        _audio.PlayPvs(ent.Comp.SlotBreakSound, ent);
    }

    /// <summary>
    /// Whether a tool slot still works, see <see cref="CyborgComponent.BrokenSlots"/>.
    /// </summary>
    public bool IsSlotWorking(CyborgComponent cyborg, int index)
    {
        return index < cyborg.ToolSlots - cyborg.BrokenSlots;
    }
}
