// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modular;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Stacks;
using Content.Shared.Tag;
using Content.Shared.Tools;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Modsuit.Systems;

/// <summary>
///     Servicing the shell. Work happens on the piece itself: deploy it, then put a welder
///     or a coil of cable to the slot it is sitting in. Clicking an occupied equipment slot
///     with something in hand is already an interaction with whatever is worn there, so a
///     dented gauntlet is repaired by working on the gauntlet — not by waving a tool at the
///     backpack and hoping the suit picks the right piece.
///
///     Which tool a piece wants is decided by what hurt it, not by a roll: plating that took
///     a beating gets worked back out with a welder, plating that was cooked by lasers or ion
///     has its loom re-run with cable. The fight tells the engineer what to bring, and a suit
///     that has been through both needs both.
/// </summary>
public sealed partial class SharedModsuitSystem
{
    private static readonly ProtoId<ToolQualityPrototype> WeldingQuality = "Welding";
    private static readonly ProtoId<TagPrototype> CableTag = "CableCoil";

    private void InitializeRepair()
    {
        SubscribeLocalEvent<ModsuitPartComponent, InteractUsingEvent>(OnRepairInteract);
        SubscribeLocalEvent<ModsuitPartComponent, ModsuitRepairDoAfterEvent>(OnRepairDoAfter);
    }

    private void OnRepairInteract(Entity<ModsuitPartComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var welder = _tool.HasQuality(args.Used, WeldingQuality);
        var cable = _tag.HasTag(args.Used, CableTag);

        if (!welder && !cable)
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
        var offered = welder ? ChassisPartFault.Structural : ChassisPartFault.Electrical;

        if (fault != offered)
        {
            // Say what the piece does need, so an engineer holding the wrong tool learns
            // something instead of being told "no".
            PopupFail(suit, args.User, fault == ChassisPartFault.Electrical
                ? "modsuit-repair-needs-cable"
                : "modsuit-repair-needs-welder");

            return;
        }

        var ev = new ModsuitRepairDoAfterEvent();

        if (welder)
        {
            _tool.UseTool(
                args.Used,
                args.User,
                ent,
                (float) ent.Comp.RepairDelay.TotalSeconds,
                [WeldingQuality],
                ev,
                ent.Comp.RepairFuel);

            return;
        }

        _doAfter.TryStartDoAfter(new DoAfterArgs(
            EntityManager,
            args.User,
            ent.Comp.RepairDelay,
            ev,
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

        // Cable is spent per round; the welder already burned its fuel up front.
        if (fault == ChassisPartFault.Electrical)
        {
            if (args.Used is not { } used
                || !TryComp<StackComponent>(used, out var stack)
                || !_stack.TryUse((used, stack), 1))
            {
                PopupFail(suit, args.User, "modsuit-repair-no-cable");
                return;
            }
        }

        var amount = ent.Comp.MaxIntegrity * ent.Comp.RepairFraction;
        ChangeIntegrity(ent, amount);

        // Work the wear back down in step with the condition, so a piece that has been
        // both dented and burnt eventually stops asking for the tool already used on it.
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
}
