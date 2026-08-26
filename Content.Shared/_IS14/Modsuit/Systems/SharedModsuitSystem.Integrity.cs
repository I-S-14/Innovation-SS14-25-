// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modular;
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
///     Two lines matter. Past the first the hardpoints stop answering and the modules
///     bolted to that piece go dark, but the piece still holds pressure. Past the second
///     it cannot hold pressure either: it pops open on its own and refuses to close until
///     someone has worked on it.
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

        var structural = 0f;
        var electrical = 0f;

        foreach (var (type, value) in args.Args.OriginalDamage.DamageDict)
        {
            // Healing passing through the same relay must not repair the shell.
            if (value <= 0)
                continue;

            var amount = value.Float() * ent.Comp.DamageMultipliers.GetValueOrDefault(type, 1f);

            if (ent.Comp.StructuralDamage.Contains(type))
                structural += amount;
            else
                electrical += amount;
        }

        if (structural + electrical <= 0f)
            return;

        // Remembering the split is what lets the piece tell an engineer which tool it
        // wants without anybody having to watch the fight.
        ent.Comp.StructuralWear += structural;
        ent.Comp.ElectricalWear += electrical;

        ChangeIntegrity(ent, -(structural + electrical));
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
    ///     Writes a part's condition and deals with whichever line it just crossed:
    ///     modules first, then pressure.
    /// </summary>
    public void SetIntegrity(Entity<ModsuitPartComponent> ent, float value)
    {
        value = Math.Clamp(value, 0f, ent.Comp.MaxIntegrity);

        if (MathHelper.CloseTo(value, ent.Comp.Integrity))
            return;

        var wasBroken = IsPartBroken(ent);
        var wasRuptured = IsPartRuptured(ent);

        ent.Comp.Integrity = value;

        // A piece worked back up to full has nothing left to complain about.
        if (value >= ent.Comp.MaxIntegrity)
        {
            ent.Comp.StructuralWear = 0f;
            ent.Comp.ElectricalWear = 0f;
        }

        Dirty(ent);

        if (ent.Comp.Control is not { } control || !TryComp<ModsuitControlComponent>(control, out var controlComp))
            return;

        var suit = new Entity<ModsuitControlComponent>(control, controlComp);
        var broken = IsPartBroken(ent);
        var ruptured = IsPartRuptured(ent);

        if (ruptured && !wasRuptured)
        {
            if (controlComp.Wearer is { } exposed)
                _popup.PopupClient(Loc.GetString("modsuit-part-ruptured", ("part", Name(ent))), suit, exposed);

            // Blows the seal itself rather than waiting to be asked: this is the piece
            // failing, not the wearer choosing to open it.
            if (ent.Comp.Sealed)
                SetPartSealed(suit, ent, false);
        }
        else if (broken && !wasBroken && controlComp.Wearer is { } wearer)
        {
            _popup.PopupClient(Loc.GetString("modsuit-part-broken", ("part", Name(ent))), suit, wearer);
        }

        if (broken != wasBroken || ruptured != wasRuptured)
        {
            // Slot availability just changed, which is the whole point of the thresholds.
            RefreshChassis(suit);
            return;
        }

        UpdateUi(suit);
    }

    /// <summary>
    ///     Whether this piece is too far gone to carry modules.
    /// </summary>
    public bool IsPartBroken(Entity<ModsuitPartComponent> ent)
    {
        return ent.Comp.MaxIntegrity > 0f
               && ent.Comp.Integrity <= ent.Comp.MaxIntegrity * ent.Comp.ModuleThreshold;
    }

    /// <summary>
    ///     Whether this piece can no longer hold pressure and must be repaired before
    ///     it will seal again.
    /// </summary>
    public bool IsPartRuptured(Entity<ModsuitPartComponent> ent)
    {
        return ent.Comp.MaxIntegrity > 0f
               && ent.Comp.Integrity <= ent.Comp.MaxIntegrity * ent.Comp.UnsealThreshold;
    }

    /// <summary>
    ///     What this piece needs doing to it. Whichever kind of damage did the most is
    ///     the one an engineer has to answer; a piece at full condition needs nothing.
    /// </summary>
    public ChassisPartFault GetFault(Entity<ModsuitPartComponent> ent)
    {
        if (ent.Comp.Integrity >= ent.Comp.MaxIntegrity)
            return ChassisPartFault.None;

        if (ent.Comp.StructuralWear <= 0f && ent.Comp.ElectricalWear <= 0f)
            return ChassisPartFault.Structural;

        return ent.Comp.ElectricalWear > ent.Comp.StructuralWear
            ? ChassisPartFault.Electrical
            : ChassisPartFault.Structural;
    }
}
