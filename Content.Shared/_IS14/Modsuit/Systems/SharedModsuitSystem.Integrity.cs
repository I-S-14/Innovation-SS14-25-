// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Damage;
using Content.Shared.Inventory;
using Robust.Shared.Utility;

namespace Content.Shared._IS14.Modsuit.Systems;

/// <summary>
///     Plating wear. Every piece of the suit has its own condition, and hits that land
///     on the body underneath it wear it down — so a suit is something that degrades
///     over a shift rather than a costume that either exists or does not.
///
///     A worn-out piece keeps sealing: it is the hardpoint inside it that stops
///     answering, which takes the modules bolted to that piece offline with it.
/// </summary>
public sealed partial class SharedModsuitSystem
{
    private void InitializeIntegrity()
    {
        SubscribeLocalEvent<ModsuitPartComponent, InventoryRelayedEvent<DamageModifyEvent>>(OnPartDamaged);
    }

    /// <summary>
    ///     Damage aimed at the body relays through the inventory to whatever is worn on it.
    ///     We read <see cref="DamageModifyEvent.OriginalDamage"/> rather than the running
    ///     total: the plating wears out from what struck it, not from the remainder its own
    ///     armour let through — otherwise the better the suit, the longer it would last,
    ///     which is backwards.
    /// </summary>
    private void OnPartDamaged(Entity<ModsuitPartComponent> ent, ref InventoryRelayedEvent<DamageModifyEvent> args)
    {
        if (!ent.Comp.Deployed || args.Args.TargetPart is not { } hit)
            return;

        if (!Covers(ent.Comp, hit))
            return;

        var total = 0f;

        foreach (var (type, value) in args.Args.OriginalDamage.DamageDict)
        {
            // Healing passing through the same relay must not repair the shell.
            if (value <= 0)
                continue;

            total += value.Float() * ent.Comp.DamageMultipliers.GetValueOrDefault(type, 1f);
        }

        if (total <= 0f)
            return;

        ChangeIntegrity(ent, -total);
    }

    private static bool Covers(ModsuitPartComponent comp, TargetBodyPart hit)
    {
        foreach (var covered in comp.CoveredParts)
        {
            if ((covered & hit) != 0)
                return true;
        }

        return false;
    }

    public void ChangeIntegrity(Entity<ModsuitPartComponent> ent, float delta)
    {
        SetIntegrity(ent, ent.Comp.Integrity + delta);
    }

    /// <summary>
    ///     Writes a part's condition and, if that crossed the break threshold, tells the
    ///     suit so the modules hanging off this piece are re-evaluated.
    /// </summary>
    public void SetIntegrity(Entity<ModsuitPartComponent> ent, float value)
    {
        value = Math.Clamp(value, 0f, ent.Comp.MaxIntegrity);

        if (MathHelper.CloseTo(value, ent.Comp.Integrity))
            return;

        var wasBroken = IsPartBroken(ent);

        ent.Comp.Integrity = value;
        Dirty(ent);

        if (ent.Comp.Control is not { } control || !TryComp<ModsuitControlComponent>(control, out var controlComp))
            return;

        var suit = new Entity<ModsuitControlComponent>(control, controlComp);
        var broken = IsPartBroken(ent);

        if (broken == wasBroken)
        {
            UpdateUi(suit);
            return;
        }

        if (broken && controlComp.Wearer is { } wearer)
            _popup.PopupClient(Loc.GetString("modsuit-part-broken", ("part", Name(ent))), suit, wearer);

        // Slot availability just changed, which is the whole point of the threshold.
        RefreshChassis(suit);
    }

    /// <summary>
    ///     Whether this piece is too far gone to carry modules.
    /// </summary>
    public bool IsPartBroken(Entity<ModsuitPartComponent> ent)
    {
        return ent.Comp.MaxIntegrity > 0f
               && ent.Comp.Integrity <= ent.Comp.MaxIntegrity * ent.Comp.BreakThreshold;
    }
}
