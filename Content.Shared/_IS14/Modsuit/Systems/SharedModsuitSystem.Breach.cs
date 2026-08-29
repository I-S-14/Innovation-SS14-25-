// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Strip;
using Content.Shared.Access.Components;
using Content.Shared.Cuffs.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Interaction;
using Content.Shared.Inventory.Events;
using Content.Shared.Strip;
using Content.Shared.DoAfter;
using Content.Shared.Tools.Components;
using Content.Shared.UserInterface;
using Content.Shared.Wires;

namespace Content.Shared._IS14.Modsuit.Systems;

/// <summary>
///     Getting somebody out of a suit they do not want to leave.
///
///     Everything else in this system assumes the wearer is the one operating the suit.
///     This part assumes the opposite, and the first problem it has to solve is reach: a
///     suit worn on somebody's back cannot be clicked at all, because the click lands on
///     the mob. So the work happens in the strip window, where every piece of the suit
///     already has its own slot to point at, and a tool clicked at one of those slots is
///     handed to the hardware behind it.
///
///     Nobody has to be restrained first. A wearer on their feet is squirming, not
///     immune: every job here simply takes several times as long, which is a cost the
///     person doing it pays in the open rather than a door in their face.
/// </summary>
public sealed partial class SharedModsuitSystem
{
    private void InitializeBreach()
    {
        SubscribeLocalEvent<ModsuitWearerComponent, InteractUsingEvent>(OnWearerInteractUsing);
        SubscribeLocalEvent<ModsuitControlComponent, StrippedItemInteractUsingEvent>(OnSuitStripInteract);
        SubscribeLocalEvent<ModsuitPartComponent, StrippedItemInteractUsingEvent>(OnPartStripInteract);
        SubscribeLocalEvent<ModsuitControlComponent, AccessibleOverrideEvent>(OnSuitAccessible);
        SubscribeLocalEvent<ModsuitControlComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<ModsuitControlComponent, ModsuitPanelDoAfterEvent>(OnPanelDoAfter);
        SubscribeLocalEvent<ModsuitPartComponent, StrippedItemRemovedEvent>(OnPartStripped);
        SubscribeLocalEvent<ModsuitPartComponent, BeingUnequippedAttemptEvent>(OnPartUnequipAttempt);
        SubscribeLocalEvent<ModsuitControlComponent, ModsuitForceReleaseEvent>(OnForceRelease);
        SubscribeLocalEvent<ModsuitWearerComponent, ModsuitCutDoAfterEvent>(OnCutDoAfter);
    }

    /// <summary>
    ///     Lets go of whoever is inside: everything unseals, everything folds away, and
    ///     the suit can be taken off the back like any other bag. This is the outcome all
    ///     the ways in are aiming at, so they share one door.
    /// </summary>
    private void OnForceRelease(Entity<ModsuitControlComponent> ent, ref ModsuitForceReleaseEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        // A seal in progress would otherwise refuse every retraction on the way out.
        ent.Comp.Sealing = false;
        ent.Comp.SealQueue.Clear();
        Dirty(ent);

        Deactivate(ent);
        RetractAll(ent, args.User);

        if (ent.Comp.Wearer is { } wearer)
            _popup.PopupClient(Loc.GetString("modsuit-breach-released"), ent, wearer);
    }

    /// <summary>
    ///     Whether the wearer is in no position to interfere with somebody working on
    ///     their suit. Cuffed, or unable to act at all — stunned, crit, dead. Deliberately
    ///     not "damaged": you subdue somebody to get them out of a MOD, you do not beat
    ///     them, and beating them only makes the job slower.
    /// </summary>
    public bool IsSubdued(EntityUid wearer)
    {
        if (TryComp<CuffableComponent>(wearer, out var cuffs) && cuffs.CuffedHandCount > 0)
            return true;

        return !_actionBlocker.CanInteract(wearer, null);
    }

    /// <summary>
    ///     How long a piece of breach work takes on this particular wearer. A restrained
    ///     one costs the listed time; one still on their feet costs several times that,
    ///     which is long enough that it cannot be done quietly in a hallway and short
    ///     enough that a team holding somebody down does not need handcuffs to start.
    /// </summary>
    private float BreachDelay(EntityUid? wearer, float seconds)
    {
        if (wearer is not { } uid || IsSubdued(uid))
            return seconds;

        return seconds * StandingPenalty;
    }

    /// <summary>
    ///     Warns the worker once, when they start, that the wearer moving about is what
    ///     they are paying for. Silent on a restrained wearer, where there is no penalty
    ///     to explain.
    /// </summary>
    private void WarnIfStanding(EntityUid target, EntityUid? wearer, EntityUid user)
    {
        if (wearer is not { } uid || IsSubdued(uid))
            return;

        _popup.PopupClient(Loc.GetString("modsuit-breach-struggling"), target, user);
    }

    /// <summary>
    ///     A cutting torch used on somebody in the world. Which piece it lands on is
    ///     whatever covers the body part the cutter has targeted, so the targeting doll
    ///     does the aiming and no new interface has to be learned.
    ///
    ///     Everything else the panel needs — screwdriver, cutters, multitool, crowbar, a
    ///     card — goes through the strip window instead, where the player points at the
    ///     piece of hardware they mean rather than at a person.
    /// </summary>
    private void OnWearerInteractUsing(Entity<ModsuitWearerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<ModsuitControlComponent>(ent.Comp.Suit, out var control))
            return;

        if (!_tool.HasQuality(args.Used, CuttingQuality))
            return;

        var suit = new Entity<ModsuitControlComponent>(ent.Comp.Suit, control);

        if (GetTargetedPart(suit, args.User) is not { } part)
        {
            _popup.PopupClient(Loc.GetString("modsuit-breach-nothing-there"), ent, args.User);
            args.Handled = true;
            return;
        }

        args.Handled = TryCut(ent, part, args.User, args.Used);
    }

    /// <summary>
    ///     A tool or a card clicked at the suit's own slot in the strip window. The suit
    ///     is handed the interaction exactly as if it were sitting on a bench — except for
    ///     the panel, whose delay this system owns so that a squirming wearer can be
    ///     charged for.
    /// </summary>
    private void OnSuitStripInteract(Entity<ModsuitControlComponent> ent, ref StrippedItemInteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // Everything else clicked at a slot means an ordinary strip, and stealing that
        // would be a bug rather than a feature.
        if (!HasComp<ToolComponent>(args.Used) && !HasComp<AccessComponent>(args.Used))
            return;

        args.Handled = true;

        if (TryComp<WiresPanelComponent>(ent, out var panel) && _tool.HasQuality(args.Used, panel.OpeningTool))
        {
            TryTogglePanel((ent, panel), args.User, args.Used);
            return;
        }

        _interaction.InteractUsing(
            args.User,
            args.Used,
            ent,
            Transform(ent).Coordinates,
            checkCanInteract: false);
    }

    /// <summary>
    ///     A torch clicked at one piece of plating in the strip window. Same job as the
    ///     targeting doll, aimed by hand: the slot the player clicked is the piece that
    ///     gets cut, with nothing to guess about which zone it covers.
    /// </summary>
    private void OnPartStripInteract(Entity<ModsuitPartComponent> ent, ref StrippedItemInteractUsingEvent args)
    {
        if (args.Handled || !ent.Comp.Deployed)
            return;

        if (!_tool.HasQuality(args.Used, CuttingQuality))
            return;

        if (ent.Comp.Control is not { } control
            || !TryComp<ModsuitControlComponent>(control, out var comp)
            || comp.Wearer != args.Target)
        {
            return;
        }

        args.Handled = TryCut(args.Target, ent, args.User, args.Used);
    }

    /// <summary>
    ///     A screwdriver taken to a worn panel. The wire system's own handler would do
    ///     this, but its delay comes straight out of the prototype with nowhere to add the
    ///     penalty, so the do-after is ours and only the toggle is handed back.
    /// </summary>
    private bool TryTogglePanel(Entity<WiresPanelComponent> ent, EntityUid user, EntityUid used)
    {
        if (!_wires.CanTogglePanel(ent, user))
            return false;

        var wearer = CompOrNull<ModsuitControlComponent>(ent)?.Wearer;

        if (!_tool.UseTool(
                used,
                user,
                ent.Owner,
                BreachDelay(wearer, (float) ent.Comp.OpenDelay.TotalSeconds),
                ent.Comp.OpeningTool,
                new ModsuitPanelDoAfterEvent()))
        {
            return false;
        }

        WarnIfStanding(ent, wearer, user);
        return true;
    }

    private void OnPanelDoAfter(Entity<ModsuitControlComponent> ent, ref ModsuitPanelDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (!TryComp<WiresPanelComponent>(ent, out var panel))
            return;

        if (!_wires.TogglePanel(ent, panel, !panel.Open, args.User))
            return;

        _audio.PlayPredicted(
            panel.Open ? panel.ScrewdriverOpenSound : panel.ScrewdriverCloseSound,
            ent,
            args.User);
    }

    /// <summary>
    ///     A worn suit is normally sealed inside its wearer's inventory, where nobody can
    ///     reach it. Reachability is what lets the panel's do-afters and the wire
    ///     interface work on a body; whether the body is co-operating is a question of
    ///     how long the work takes, not of whether it can be started.
    /// </summary>
    private void OnSuitAccessible(Entity<ModsuitControlComponent> ent, ref AccessibleOverrideEvent args)
    {
        if (args.Handled || args.Target != ent.Owner || ent.Comp.Wearer is not { } wearer)
            return;

        if (args.User == wearer)
            return;

        args.Handled = true;
        args.Accessible = _interaction.IsAccessible(args.User, wearer);
    }

    /// <summary>
    ///     The readout stays the wearer's. Reaching the hardware is one thing; driving the
    ///     suit from its own control panel while somebody else is inside it would make
    ///     every other way in pointless.
    ///
    ///     Refused silently. The panel simply does not open for a stranger, and a popup
    ///     saying so on every click was noise on the one interaction people try most.
    /// </summary>
    private void OnUiOpenAttempt(Entity<ModsuitControlComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        // A wrecked interface refuses everyone, wearer included.
        if (!CanUseInterface(ent, args.User))
        {
            args.Cancel();
            return;
        }

        if (ent.Comp.Wearer is not { } wearer || args.User == wearer)
            return;

        args.Cancel();
    }

    /// <summary>
    ///     A cutting torch taken to worn plating. The damage lands on the piece and only
    ///     on the piece: this is how somebody is got out without being touched, which is
    ///     the whole reason it exists. Slow, loud, and it ruins the armour — cutting a
    ///     prisoner out costs the department a chestplate.
    /// </summary>
    private bool TryCut(EntityUid wearer, Entity<ModsuitPartComponent> part, EntityUid user, EntityUid used)
    {
        if (IsPartRuptured(part))
        {
            _popup.PopupClient(Loc.GetString("modsuit-breach-already-cut", ("part", Name(part))), wearer, user);
            return true;
        }

        // The do-after hangs off the body rather than the plating: the plating is inside
        // an inventory slot, and a do-after against something in a slot spends its life
        // arguing about whether the user can reach it.
        if (!_tool.UseTool(
                used,
                user,
                wearer,
                BreachDelay(wearer, CutDelay),
                CuttingQuality,
                new ModsuitCutDoAfterEvent(GetNetEntity(part))))
        {
            return false;
        }

        WarnIfStanding(wearer, wearer, user);
        return true;
    }

    /// <summary>
    ///     One pass of the torch, and then the next one without asking. Cutting somebody
    ///     out is a single job the player commits to, the way a meal is: it runs until the
    ///     piece gives way, the torch goes out, or somebody moves.
    /// </summary>
    private void OnCutDoAfter(Entity<ModsuitWearerComponent> ent, ref ModsuitCutDoAfterEvent args)
    {
        args.Repeat = false;

        // Deliberately not gated on Handled. The do-after system resets that flag on the
        // wrapper it repeats, never on the event wrapped inside, so a second pass would
        // arrive already marked handled and stop the job one cut in.
        if (args.Cancelled)
            return;

        args.Handled = true;

        var part = GetEntity(args.Part);

        if (!TryComp<ModsuitPartComponent>(part, out var comp) || !comp.Deployed)
            return;

        // A fraction of the piece's own rating, so heavy plating takes more passes than
        // light plating instead of everything taking the same flat number.
        ChangeIntegrity((part, comp), -comp.MaxIntegrity * CutFraction);

        _popup.PopupClient(Loc.GetString("modsuit-breach-cutting", ("part", Name(part))), ent, args.User);

        // Stop at the seal rather than at nothing: the point of the torch is the hole,
        // and grinding a chestplate to scrap afterwards helps nobody.
        if (IsPartRuptured((part, comp)))
            return;

        // The do-after itself never re-checks the tool once it is running, so a welder
        // that has gone out or run dry has to be caught here or it would cut forever.
        args.Repeat = CanKeepCutting(args.Used, args.User);
    }

    /// <summary>
    ///     Whether the torch is still a torch: lit, fuelled, and not taken out of the hand
    ///     holding it.
    /// </summary>
    private bool CanKeepCutting(EntityUid? used, EntityUid user)
    {
        if (used is not { } tool || TerminatingOrDeleted(tool) || !_tool.HasQuality(tool, CuttingQuality))
            return false;

        var attempt = new ToolUseAttemptEvent(user, 0f);
        RaiseLocalEvent(tool, attempt);

        return !attempt.Cancelled;
    }

    /// <summary>
    ///     The piece covering whatever body part the user has selected on their targeting
    ///     doll, or the chest when they have no doll to speak of.
    /// </summary>
    private Entity<ModsuitPartComponent>? GetTargetedPart(Entity<ModsuitControlComponent> suit, EntityUid user)
    {
        var zone = CompOrNull<TargetingComponent>(user)?.Target ?? TargetBodyPart.Chest;

        foreach (var part in suit.Comp.Parts.Values)
        {
            if (!TryComp<ModsuitPartComponent>(part, out var comp) || !comp.Deployed)
                continue;

            foreach (var covered in comp.CoveredParts)
            {
                if ((covered & zone) != 0)
                    return (part, comp);
            }
        }

        return null;
    }

    /// <summary>Tool quality that cuts plating off a body.</summary>
    private const string CuttingQuality = "Welding";

    /// <summary>Seconds one pass with the torch takes.</summary>
    private const float CutDelay = 1.5f;

    /// <summary>Share of a piece's rating one pass takes off.</summary>
    private const float CutFraction = 0.125f;

    /// <summary>What breach work costs on a wearer who is still on their feet.</summary>
    private const float StandingPenalty = 3f;

    /// <summary>
    ///     Worn plating comes off in exactly one way: somebody else taking it off a person
    ///     who cannot stop them. The wearer peeling their own suit apart by hand would
    ///     leave the pieces loose in the world with the suit still expecting to own them,
    ///     and they already have a fold button for it.
    /// </summary>
    private void OnPartUnequipAttempt(Entity<ModsuitPartComponent> ent, ref BeingUnequippedAttemptEvent args)
    {
        if (!ent.Comp.Deployed)
            return;

        if (args.Unequipee == args.UnEquipTarget)
        {
            args.Cancel();
            _popup.PopupClient(Loc.GetString("modsuit-breach-fold-instead"), ent, args.Unequipee);
            return;
        }

        if (!IsSubdued(args.UnEquipTarget))
        {
            args.Cancel();
            _popup.PopupClient(Loc.GetString("modsuit-breach-not-subdued"), ent, args.Unequipee);
        }
    }

    /// <summary>
    ///     Plating pulled off a body in the strip menu folds back into its own suit rather
    ///     than coming away in the stripper's hands. The suit owns its plating; what a
    ///     stripper is after is the person inside, and once every piece is folded the suit
    ///     itself comes off the back like any other bag.
    /// </summary>
    private void OnPartStripped(Entity<ModsuitPartComponent> ent, ref StrippedItemRemovedEvent args)
    {
        if (ent.Comp.Control is not { } control || !TryComp<ModsuitControlComponent>(control, out var comp))
            return;

        // The suit does the moving, so the ordinary removal is cancelled outright.
        args.Handled = true;
        TryRetractPart((control, comp), ent, args.User);
    }
}
