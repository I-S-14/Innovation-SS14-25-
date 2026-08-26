// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Components;
using Content.Shared.Cuffs.Components;
using Content.Shared._Shitmed.Targeting;
using Content.Shared.Interaction;
using Content.Shared.Inventory.Events;
using Content.Shared.Strip;
using Content.Shared.DoAfter;
using Content.Shared.Tools.Components;
using Content.Shared.UserInterface;

namespace Content.Shared._IS14.Modsuit.Systems;

/// <summary>
///     Getting somebody out of a suit they do not want to leave.
///
///     Everything else in this system assumes the wearer is the one operating the suit.
///     This part assumes the opposite, and the first problem it has to solve is reach: a
///     suit worn on somebody's back cannot be clicked at all, because the click lands on
///     the mob. So the wearer carries a marker that forwards tools to the suit, and the
///     suit counts as reachable — but only while its wearer is in no position to stop
///     anyone. Cuff first, then work. Nobody has to be beaten to be let out.
/// </summary>
public sealed partial class SharedModsuitSystem
{
    private void InitializeBreach()
    {
        SubscribeLocalEvent<ModsuitWearerComponent, InteractUsingEvent>(OnWearerInteractUsing);
        SubscribeLocalEvent<ModsuitControlComponent, AccessibleOverrideEvent>(OnSuitAccessible);
        SubscribeLocalEvent<ModsuitControlComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<ModsuitPartComponent, StrippedItemRemovedEvent>(OnPartStripped);
        SubscribeLocalEvent<ModsuitPartComponent, BeingUnequippedAttemptEvent>(OnPartUnequipAttempt);
        SubscribeLocalEvent<ModsuitControlComponent, ModsuitForceReleaseEvent>(OnForceRelease);
        SubscribeLocalEvent<ModsuitWearerComponent, ModsuitCutDoAfterEvent>(OnCutDoAfter);
    }

    /// <summary>
    ///     Lets go of whoever is inside: everything unseals, everything folds away, and
    ///     the suit can be taken off the back like any other bag. This is the outcome all
    ///     three ways in are aiming at, so they share one door.
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
    ///     Whether the wearer is in no position to stop somebody working on their suit.
    ///     Cuffed, or unable to act at all — stunned, crit, dead. Deliberately not
    ///     "damaged": you subdue somebody to get them out of a MOD, you do not beat them.
    /// </summary>
    public bool IsSubdued(EntityUid wearer)
    {
        if (TryComp<CuffableComponent>(wearer, out var cuffs) && cuffs.CuffedHandCount > 0)
            return true;

        return !_actionBlocker.CanInteract(wearer, null);
    }

    /// <summary>
    ///     Forwards a tool used on the wearer to the suit itself, so a screwdriver opens
    ///     its panel, a multitool reaches its wires, a crowbar takes its core and an ID
    ///     answers its lock — all of it through the ordinary interactions those tools
    ///     already have with a MOD lying on a table.
    /// </summary>
    private void OnWearerInteractUsing(Entity<ModsuitWearerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp<ModsuitControlComponent>(ent.Comp.Suit, out var control))
            return;

        // Only tools and cards. Everything else clicked at a person means something else
        // entirely, and stealing those interactions would be a bug, not a feature.
        if (!HasComp<ToolComponent>(args.Used) && !HasComp<Content.Shared.Access.Components.AccessComponent>(args.Used))
            return;

        if (!IsSubdued(ent.Owner))
        {
            _popup.PopupClient(Loc.GetString("modsuit-breach-not-subdued"), ent, args.User);
            args.Handled = true;
            return;
        }

        // A torch is aimed at the plating; everything else is aimed at the control unit.
        if (_tool.HasQuality(args.Used, CuttingQuality))
        {
            args.Handled = TryCut(ent, (ent.Comp.Suit, control), args.User, args.Used);
            return;
        }

        // Hand the interaction to the suit exactly as if it were sitting on a bench.
        args.Handled = _interaction.InteractUsing(
            args.User,
            args.Used,
            ent.Comp.Suit,
            Transform(ent.Comp.Suit).Coordinates,
            checkCanInteract: false);
    }

    /// <summary>
    ///     A worn suit is normally sealed inside its wearer's inventory, where nobody can
    ///     reach it — which is right until the wearer is face down in cuffs. Reachability
    ///     is what lets the panel's do-afters and the wire interface work on a body.
    /// </summary>
    private void OnSuitAccessible(Entity<ModsuitControlComponent> ent, ref AccessibleOverrideEvent args)
    {
        if (args.Handled || args.Target != ent.Owner || ent.Comp.Wearer is not { } wearer)
            return;

        if (args.User == wearer)
            return;

        if (!IsSubdued(wearer))
            return;

        args.Handled = true;
        args.Accessible = _interaction.IsAccessible(args.User, wearer);
    }

    /// <summary>
    ///     The readout stays the wearer's. Reaching the hardware is one thing; driving the
    ///     suit from its own control panel while somebody else is inside it would make
    ///     every other way in pointless.
    /// </summary>
    private void OnUiOpenAttempt(Entity<ModsuitControlComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        if (args.Cancelled || ent.Comp.Wearer is not { } wearer || args.User == wearer)
            return;

        args.Cancel();
        _popup.PopupClient(Loc.GetString("modsuit-breach-not-yours"), ent, args.User);
    }

    /// <summary>
    ///     A cutting torch taken to worn plating. The damage lands on the piece and only
    ///     on the piece: this is how somebody is got out without being touched, which is
    ///     the whole reason it exists. Slow, loud, and it ruins the armour — cutting a
    ///     prisoner out costs the department a chestplate.
    ///
    ///     Which piece gets cut is whichever covers the body part the cutter has targeted,
    ///     so the existing targeting doll is the aiming mechanism and nothing new has to
    ///     be learned.
    /// </summary>
    private bool TryCut(Entity<ModsuitWearerComponent> ent, Entity<ModsuitControlComponent> suit, EntityUid user, EntityUid used)
    {
        if (GetTargetedPart(suit, user) is not { } part)
        {
            _popup.PopupClient(Loc.GetString("modsuit-breach-nothing-there"), ent, user);
            return true;
        }

        if (part.Comp.Integrity <= 0f)
        {
            _popup.PopupClient(Loc.GetString("modsuit-breach-already-cut", ("part", Name(part))), ent, user);
            return true;
        }

        // The do-after hangs off the body rather than the plating: the plating is inside
        // an inventory slot, and a do-after against something in a slot spends its life
        // arguing about whether the user can reach it.
        _tool.UseTool(
            used,
            user,
            ent,
            CutDelay,
            CuttingQuality,
            new ModsuitCutDoAfterEvent(GetNetEntity(part)));

        return true;
    }

    private void OnCutDoAfter(Entity<ModsuitWearerComponent> ent, ref ModsuitCutDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var part = GetEntity(args.Part);

        if (!TryComp<ModsuitPartComponent>(part, out var comp) || !comp.Deployed)
            return;

        // A fraction of the piece's own rating, so heavy plating takes more passes than
        // light plating instead of everything taking the same flat number.
        ChangeIntegrity((part, comp), -comp.MaxIntegrity * CutFraction);

        _popup.PopupClient(Loc.GetString("modsuit-breach-cutting", ("part", Name(part))), ent, args.User);
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
    private const float CutDelay = 6f;

    /// <summary>Share of a piece's rating one pass takes off.</summary>
    private const float CutFraction = 0.25f;

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
