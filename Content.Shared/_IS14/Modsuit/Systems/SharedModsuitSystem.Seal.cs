// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modular.Components;
using Content.Shared.DoAfter;
using Content.Shared.Interaction.Components;

namespace Content.Shared._IS14.Modsuit.Systems;

/// <summary>
///     Sealing: the step between "wearing the suit" and "surviving vacuum in it".
///     Parts seal one at a time with a delay each, so suiting up before a spacewalk
///     is a decision with a cost rather than a button press.
/// </summary>
public sealed partial class SharedModsuitSystem
{
    /// <summary>
    ///     Starts sealing or unsealing every deployed part in sequence.
    /// </summary>
    public bool TryToggleSeal(Entity<ModsuitControlComponent> ent, EntityUid? user = null)
    {
        if (ent.Comp.Sealing)
        {
            if (user != null)
                PopupFail(ent, user.Value, "modsuit-busy-sealing");

            return false;
        }

        if (ent.Comp.Wearer == null)
        {
            if (user != null)
                PopupFail(ent, user.Value, "modsuit-not-worn");

            return false;
        }

        var sealingUp = !IsSealed(ent);

        // Sealing needs power; coming back out of a sealed suit never should,
        // or a flat battery would trap the wearer.
        if (sealingUp && !HasWorkingCore(ent))
        {
            if (user != null)
                PopupFail(ent, user.Value, "modsuit-no-core");

            return false;
        }

        var queue = new List<EntityUid>();
        foreach (var part in ent.Comp.Parts.Values)
        {
            if (!TryComp<ModsuitPartComponent>(part, out var comp) || !comp.Deployed)
                continue;

            // A ruptured piece has nothing left to close; the rest of the suit still
            // seals around it, which is what makes losing one piece survivable.
            if (sealingUp && IsPartRuptured((part, comp)))
                continue;

            if (comp.Sealed != sealingUp)
                queue.Add(part);
        }

        if (queue.Count == 0)
        {
            if (user != null)
                PopupFail(ent, user.Value, sealingUp ? "modsuit-nothing-to-seal" : "modsuit-nothing-to-unseal");

            return false;
        }

        ent.Comp.Sealing = true;
        ent.Comp.SealingUp = sealingUp;
        ent.Comp.SealQueue = queue;
        Dirty(ent);

        StartNextSealStep(ent, user);
        return true;
    }

    /// <summary>
    ///     Seals or unseals a single part on its own, leaving the rest of the suit as it is.
    ///     This is what makes it possible to pop the helmet to eat without depressurising.
    /// </summary>
    public bool TrySealPart(Entity<ModsuitControlComponent> ent, EntityUid part, bool sealUp, EntityUid? user = null)
    {
        if (ent.Comp.Sealing)
        {
            if (user != null)
                PopupFail(ent, user.Value, "modsuit-busy-sealing");

            return false;
        }

        if (!TryComp<ModsuitPartComponent>(part, out var comp) || !comp.Deployed || comp.Sealed == sealUp)
            return false;

        if (sealUp && IsPartRuptured((part, comp)))
        {
            if (user != null)
                PopupFail(ent, user.Value, "modsuit-part-cannot-seal");

            return false;
        }

        if (sealUp && !HasWorkingCore(ent))
        {
            if (user != null)
                PopupFail(ent, user.Value, "modsuit-no-core");

            return false;
        }

        ent.Comp.Sealing = true;
        ent.Comp.SealingUp = sealUp;
        ent.Comp.SealQueue = new List<EntityUid> { part };
        Dirty(ent);

        StartNextSealStep(ent, user);
        return true;
    }

    private void StartNextSealStep(Entity<ModsuitControlComponent> ent, EntityUid? user)
    {
        if (ent.Comp.SealQueue.Count == 0)
        {
            FinishSealing(ent);
            return;
        }

        var part = ent.Comp.SealQueue[0];
        var doAfterUser = user ?? ent.Comp.Wearer;

        if (doAfterUser == null)
        {
            FinishSealing(ent);
            return;
        }

        var ev = new ModsuitSealDoAfterEvent(GetNetEntity(part), ent.Comp.SealingUp);

        var args = new DoAfterArgs(EntityManager, doAfterUser.Value, ent.Comp.SealTimePerPart, ev, ent, ent)
        {
            // Deliberately not BreakOnMove: you seal up while running for the airlock,
            // not while standing still hoping nothing shoots you.
            BreakOnMove = false,
            BreakOnDamage = true,
            NeedHand = false,
            BlockDuplicate = true,
            CancelDuplicate = true,
        };

        if (!_doAfter.TryStartDoAfter(args))
            FinishSealing(ent);
    }

    private void OnSealDoAfter(Entity<ModsuitControlComponent> ent, ref ModsuitSealDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
        {
            // A broken sequence leaves the suit part-sealed on purpose: the player has to
            // deal with the state they created rather than it silently snapping back.
            FinishSealing(ent);
            return;
        }

        args.Handled = true;

        var part = GetEntity(args.Part);

        if (TryComp<ModsuitPartComponent>(part, out var partComp))
            SetPartSealed(ent, (part, partComp), args.SealingUp, args.User);

        ent.Comp.SealQueue.Remove(part);
        Dirty(ent);

        StartNextSealStep(ent, ent.Comp.Wearer);
    }

    private void FinishSealing(Entity<ModsuitControlComponent> ent)
    {
        ent.Comp.Sealing = false;
        ent.Comp.SealQueue.Clear();
        Dirty(ent);

        // The suit runs whenever any part is sealed; it does not demand the full set.
        SetActive(ent, IsAnyPartSealed(ent));

        if (ent.Comp.Wearer is { } wearer)
            _audio.PlayPredicted(ent.Comp.SealCompleteSound, ent, wearer);
    }

    /// <summary>
    ///     Applies or removes a part's sealed state and the components that go with it.
    /// </summary>
    public void SetPartSealed(
        Entity<ModsuitControlComponent> ent,
        Entity<ModsuitPartComponent> part,
        bool value,
        EntityUid? user = null)
    {
        if (part.Comp.Sealed == value)
            return;

        part.Comp.Sealed = value;
        Dirty(part);

        // Pressure and temperature protection only exist while sealed, which is what
        // separates "deployed" from "spaceworthy".
        if (value)
        {
            EntityManager.AddComponents(part, part.Comp.SealedComponents);

            // A closed piece cannot be pulled off the body — by the wearer or by anyone
            // standing over them. An open one can, and that is the way out of a MOD.
            EnsureComp<UnremoveableComponent>(part).DeleteOnDrop = false;
        }
        else
        {
            EntityManager.RemoveComponents(part, part.Comp.SealedComponents);
            RemComp<UnremoveableComponent>(part);
        }

        // Air rushing in or venting out, once per part — the sound that makes the
        // sealing sequence feel like a sequence rather than a single toggle.
        // Predicted, not PlayPvs: this runs inside a do-after, which the client replays
        // on every prediction pass, and PlayPvs would fire a fresh source each time.
        _audio.PlayPredicted(value ? part.Comp.SealSound : part.Comp.UnsealSound, part, user ?? ent.Comp.Wearer);

        if (ent.Comp.Wearer is { } wearer)
        {
            var popup = value ? part.Comp.SealPopup : part.Comp.UnsealPopup;
            if (popup != null)
                _popup.PopupClient(Loc.GetString(popup), ent, wearer);
        }

        var ev = new ModsuitPartSealedEvent(ent, value);
        RaiseLocalEvent(part, ref ev);

        RefreshChassis(ent);
    }

    public bool IsSealed(Entity<ModsuitControlComponent> ent)
    {
        var anyDeployed = false;

        foreach (var part in ent.Comp.Parts.Values)
        {
            if (!TryComp<ModsuitPartComponent>(part, out var comp) || !comp.Deployed)
                continue;

            anyDeployed = true;

            if (!comp.Sealed)
                return false;
        }

        return anyDeployed;
    }

    public bool IsAnyPartSealed(Entity<ModsuitControlComponent> ent)
    {
        foreach (var part in ent.Comp.Parts.Values)
        {
            if (TryComp<ModsuitPartComponent>(part, out var comp) && comp.Sealed)
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Sealing needs a core with something left in it. A flat suit closing up would
    ///     be a coffin: the seal draws power to hold, and the wearer would be locked into
    ///     a shell that cannot even open itself.
    /// </summary>
    private bool HasWorkingCore(Entity<ModsuitControlComponent> ent)
    {
        if (!HasComp<ChassisPowerComponent>(ent))
            return true;

        var (current, max) = _power.GetCharge(ent);
        return max > 0f && current > 0f;
    }

    #region State

    /// <summary>
    ///     Switches the suit on. Only meaningful once something is sealed.
    /// </summary>
    public void SetActive(Entity<ModsuitControlComponent> ent, bool active)
    {
        if (!TryComp<ModularChassisComponent>(ent, out var chassis))
            return;

        _chassis.SetActive((ent, chassis), active);
    }

    /// <summary>
    ///     Shuts the suit down and unseals it, without the usual per-part delay.
    ///     Used when power runs out or the suit is forcibly removed.
    /// </summary>
    public void Deactivate(Entity<ModsuitControlComponent> ent)
    {
        ent.Comp.Sealing = false;
        ent.Comp.SealQueue.Clear();

        foreach (var part in ent.Comp.Parts.Values)
        {
            if (TryComp<ModsuitPartComponent>(part, out var comp) && comp.Sealed)
                SetPartSealed(ent, (part, comp), false);
        }

        Dirty(ent);
        SetActive(ent, false);
    }

    #endregion
}
