// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modular;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Modsuit.Systems;

/// <summary>
///     Servicing the shell. Work happens on the piece itself: deploy it, then put a sheet
///     of plasteel or a coil of cable to the slot it is sitting in. Clicking an occupied
///     equipment slot with something in hand is already an interaction with whatever is
///     worn there, so a dented gauntlet is repaired by working on the gauntlet — not by
///     waving a tool at the backpack and hoping the suit picks the right piece.
///
///     Which material a piece wants is decided by what hurt it, not by a roll: plating
///     that took a beating is patched with new plating, plating that was cooked by lasers
///     or ion has its loom re-run with cable. The fight tells the engineer what to bring,
///     and a suit that has been through both needs both.
///
///     Deliberately no welder anywhere in here. A torch is how you cut somebody out of a
///     MOD; the same tool putting one back together made the two jobs indistinguishable at
///     the moment of the click, and the wrong one would fire on a mistake that costs a
///     chestplate. Repair costs materials, breaching costs time, and nothing does both.
/// </summary>
public sealed partial class SharedModsuitSystem
{
    private static readonly ProtoId<TagPrototype> CableTag = "CableCoil";
    private static readonly ProtoId<StackPrototype> PlasteelStack = "Plasteel";

    private void InitializeRepair()
    {
        SubscribeLocalEvent<ModsuitPartComponent, InteractUsingEvent>(OnRepairInteract);
        SubscribeLocalEvent<ModsuitPartComponent, ModsuitRepairDoAfterEvent>(OnRepairDoAfter);
    }

    private void OnRepairInteract(Entity<ModsuitPartComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var plasteel = IsPlasteel(args.Used);
        var cable = _tag.HasTag(args.Used, CableTag);

        if (!plasteel && !cable)
            return;

        if (ent.Comp.Control is not { } control || !TryComp<ModsuitControlComponent>(control, out var controlComp))
            return;

        var fault = GetFault(ent);

        // Intact plating is not a failed repair, it is no repair at all: no popup, no
        // buzz, and the click stays unhandled so it can still mean something else.
        if (fault == ChassisPartFault.None)
            return;

        args.Handled = true;

        var suit = new Entity<ModsuitControlComponent>(control, controlComp);
        var offered = plasteel ? ChassisPartFault.Structural : ChassisPartFault.Electrical;

        if (fault != offered)
        {
            // Say what the piece does need, so an engineer holding the wrong thing learns
            // something instead of being told "no".
            PopupFail(suit, args.User, fault == ChassisPartFault.Electrical
                ? "modsuit-repair-needs-cable"
                : "modsuit-repair-needs-plasteel");

            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.RepairDelay,
            new ModsuitRepairDoAfterEvent(),
            ent,
            ent,
            args.Used)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        });
    }

    private void OnRepairDoAfter(Entity<ModsuitPartComponent> ent, ref ModsuitRepairDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (ent.Comp.Control is not { } control || !TryComp<ModsuitControlComponent>(control, out var controlComp))
            return;

        var suit = new Entity<ModsuitControlComponent>(control, controlComp);
        var fault = GetFault(ent);

        // Both repairs are paid for in stock, one sheet or one coil per pass. Taken at the
        // end rather than up front so an interrupted repair costs nothing.
        if (args.Used is not { } used
            || !TryComp<StackComponent>(used, out var stack)
            || !_stack.TryUse((used, stack), 1))
        {
            PopupFail(suit, args.User, fault == ChassisPartFault.Electrical
                ? "modsuit-repair-no-cable"
                : "modsuit-repair-no-plasteel");

            return;
        }

        var amount = ent.Comp.MaxIntegrity * ent.Comp.RepairFraction;
        ChangeIntegrity(ent, amount);

        // Work the wear back down in step with the condition, so a piece that has been
        // both dented and burnt eventually stops asking for the material already spent on
        // it.
        var drain = Math.Min(amount, fault == ChassisPartFault.Electrical
            ? ent.Comp.ElectricalWear
            : ent.Comp.StructuralWear);

        if (fault == ChassisPartFault.Electrical)
            ent.Comp.ElectricalWear -= drain;
        else
            ent.Comp.StructuralWear -= drain;

        Dirty(ent);

        _popup.PopupClient(Loc.GetString("modsuit-repair-done", ("part", Name(ent))), ent, args.User);

        UpdateUi(suit);
    }

    /// <summary>
    ///     Whether this is a stack of plasteel. Checked by stack type rather than by tag
    ///     so that anything the game already counts as plasteel counts here too.
    /// </summary>
    private bool IsPlasteel(EntityUid uid)
    {
        return TryComp<StackComponent>(uid, out var stack) && stack.StackTypeId == PlasteelStack;
    }
}
