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

        // The sequence belongs to the body inside the suit, whoever set it off. It used to
        // take the caller for the first step and the wearer for every step after, and a
        // sequence started by somebody else — the release wire — then moved the do-after
        // from one entity to another halfway through. The second step lands inside the
        // do-after system's own update loop, where putting ActiveDoAfterComponent on a
        // fresh entity invalidates the query it is walking, and the server dies mid-tick.
        var doAfterUser = ent.Comp.Wearer ?? user;

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
    ///     What the release wire drives: a suit that is open or live is shut down and let
    ///     go of, a suit that is folded away is deployed and sealed around whoever has it
    ///     on. Both directions of the same switch, which is what makes the wire worth
    ///     finding for reasons other than escape.
    /// </summary>
    public void ToggleForceSeal(Entity<ModsuitControlComponent> ent, EntityUid? user = null)
    {
        if (AnyPartDeployed(ent))
        {
            var release = new ModsuitForceReleaseEvent(user);
            RaiseLocalEvent(ent, ref release);
            return;
        }

        DeployAll(ent, user);
        TryToggleSeal(ent, user);
    }

    /// <summary>
    ///     The suit coming apart because it lost power, rather than because somebody asked
    ///     it to. Parts give way one at a time on a timer instead of a do-after: nobody is
    ///     performing this, so there is nothing to interrupt and nothing to stand still
    ///     for — but it is still heard happening, one seal at a time.
    /// </summary>
    public void StartBlowout(Entity<ModsuitControlComponent> ent)
    {
        // Whatever the wearer was in the middle of is over.
        ent.Comp.Sealing = false;
        ent.Comp.SealQueue.Clear();
        Dirty(ent);

        var queue = new List<EntityUid>();

        foreach (var part in ent.Comp.Parts.Values)
        {
            if (TryComp<ModsuitPartComponent>(part, out var comp) && comp.Sealed)
                queue.Add(part);
        }

        if (queue.Count == 0)
        {
            SetActive(ent, false);
            return;
        }

        ent.Comp.BlowoutQueue = queue;

        // The first one goes immediately: the suit does not pause politely before it
        // starts failing.
        ent.Comp.BlowoutNext = _timing.CurTime;
    }

    /// <summary>
    ///     Walks whichever suits are in the middle of blowing open. Server-side, like the
    ///     drain loop that starts it.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (!_net.IsServer)
            return;

        var now = _timing.CurTime;

        ExpireSealArming(now);

        var query = EntityQueryEnumerator<ModsuitControlComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.BlowoutNext is not { } next || now < next)
                continue;

            var ent = new Entity<ModsuitControlComponent>(uid, comp);

            // Anything that got unsealed by other means on the way is skipped rather than
            // waited on, so a suit half taken apart by hand still finishes falling open.
            while (comp.BlowoutQueue.Count > 0)
            {
                var part = comp.BlowoutQueue[0];
                comp.BlowoutQueue.RemoveAt(0);

                if (!TryComp<ModsuitPartComponent>(part, out var partComp) || !partComp.Sealed)
                    continue;

                SetPartSealed(ent, (part, partComp), false);
                break;
            }

            if (comp.BlowoutQueue.Count > 0)
            {
                comp.BlowoutNext = now + comp.BlowoutInterval;
                continue;
            }

            comp.BlowoutNext = null;
            SetActive(ent, false);
        }
    }

    /// <summary>
    ///     Shuts the suit down and unseals it, without the usual per-part delay.
    ///     Used when the suit is forcibly opened or taken off.
    /// </summary>
    public void Deactivate(Entity<ModsuitControlComponent> ent)
    {
        ent.Comp.Sealing = false;
        ent.Comp.SealQueue.Clear();
        ent.Comp.BlowoutQueue.Clear();
        ent.Comp.BlowoutNext = null;

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
