// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modular;
using Content.Shared._IS14.Modular.Components;
using Content.Shared._IS14.Modular.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Emp;
using Content.Shared.Forensics.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Wires;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._IS14.Modsuit.Systems;

/// <summary>
///     The ID lock and the sabotage states the wires drive. Kept apart from the suit
///     system proper because none of it is about wearing a suit — it is about breaking
///     into one.
/// </summary>
public sealed class SharedModsuitLockSystem : EntitySystem
{
    [Dependency] private readonly AccessReaderSystem _access = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly SharedModularChassisSystem _chassis = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private static readonly SoundSpecifier LockSound =
        new SoundPathSpecifier("/Audio/_IS14/Modsuit/module_click.ogg");

    private static readonly SoundSpecifier DenySound =
        new SoundPathSpecifier("/Audio/_IS14/Modsuit/fail.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModsuitLockComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<ModsuitLockComponent, GotEmaggedEvent>(OnEmagged);

        // The panel is the thing the lock actually protects.
        SubscribeLocalEvent<ModsuitLockComponent, ChassisInstallModuleAttemptEvent>(OnInstallAttempt);

        SubscribeLocalEvent<ModuleDnaLockComponent, ModuleUsedEvent>(OnDnaLockUsed);
        SubscribeLocalEvent<ModuleDnaLockComponent, GotEmaggedEvent>(OnDnaLockEmagged);
        SubscribeLocalEvent<ModuleDnaLockComponent, EmpPulseEvent>(OnDnaLockEmp);
    }

    #region DNA lock

    /// <summary>
    ///     The imprinted DNA of whichever DNA lock module is installed, or null if there
    ///     is none, or it is blank, or it has been burned out.
    /// </summary>
    public string? GetDnaLock(EntityUid chassis)
    {
        if (!TryComp<ModularChassisComponent>(chassis, out var comp))
            return null;

        foreach (var module in _chassis.GetModuleEntities((chassis, comp)))
        {
            if (TryComp<ModuleDnaLockComponent>(module, out var dna) && !dna.Broken)
                return dna.Dna;
        }

        return null;
    }

    /// <summary>
    ///     Whether this person is locked out of the suit. Nobody at all is locked out of
    ///     a blank lock, and the owner is never locked out of their own.
    /// </summary>
    public bool IsDnaBlocked(EntityUid chassis, EntityUid? user)
    {
        if (GetDnaLock(chassis) is not { } imprint)
            return false;

        if (user == null)
            return true;

        return !TryComp<DnaComponent>(user.Value, out var dna) || dna.DNA != imprint;
    }

    /// <summary>
    ///     Pressing the module imprints it on whoever pressed it; pressing it again as
    ///     the owner wipes it. Somebody else's imprint cannot be pressed over — that is
    ///     the entire point of the lock.
    /// </summary>
    private void OnDnaLockUsed(Entity<ModuleDnaLockComponent> ent, ref ModuleUsedEvent args)
    {
        if (args.Handled || args.User is not { } user)
            return;

        args.Handled = true;

        if (ent.Comp.Broken)
        {
            Deny(args.Chassis, user, "modsuit-dna-lock-broken");
            return;
        }

        if (!TryComp<DnaComponent>(user, out var dna) || dna.DNA == null)
        {
            Deny(args.Chassis, user, "modsuit-dna-lock-no-dna");
            return;
        }

        if (ent.Comp.Dna == null)
        {
            ent.Comp.Dna = dna.DNA;
            Dirty(ent);
            Announce(args.Chassis);
            _audio.PlayPredicted(LockSound, args.Chassis, user);
            _popup.PopupClient(Loc.GetString("modsuit-dna-lock-set"), args.Chassis, user);
            return;
        }

        if (ent.Comp.Dna != dna.DNA)
        {
            Deny(args.Chassis, user, "modsuit-dna-lock-denied");
            return;
        }

        ent.Comp.Dna = null;
        Dirty(ent);
        Announce(args.Chassis);
        _audio.PlayPredicted(LockSound, args.Chassis, user);
        _popup.PopupClient(Loc.GetString("modsuit-dna-lock-cleared"), args.Chassis, user);
    }

    /// <summary>
    ///     An emag does not open the lock, it destroys it. The module is dead weight
    ///     afterwards and has to be replaced.
    /// </summary>
    private void OnDnaLockEmagged(Entity<ModuleDnaLockComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Access) || ent.Comp.Broken)
            return;

        ent.Comp.Broken = true;
        ent.Comp.Dna = null;
        Dirty(ent);

        if (TryComp<ChassisModuleComponent>(ent, out var module) && module.Chassis is { } chassis)
            Announce(chassis);

        args.Handled = true;
    }

    private void OnDnaLockEmp(Entity<ModuleDnaLockComponent> ent, ref EmpPulseEvent args)
    {
        if (ent.Comp.Dna == null)
            return;

        args.Affected = true;
        WipeDnaLock(ent);
    }

    /// <summary>
    ///     Wipes the imprint without breaking the hardware. This is what an EMP does, and
    ///     it is the reason an EMP grenade is a way into somebody's MOD.
    /// </summary>
    public void WipeDnaLock(Entity<ModuleDnaLockComponent> ent)
    {
        if (ent.Comp.Dna == null)
            return;

        ent.Comp.Dna = null;
        Dirty(ent);

        if (TryComp<ChassisModuleComponent>(ent, out var module) && module.Chassis is { } chassis)
            Announce(chassis);
    }

    private void Deny(EntityUid chassis, EntityUid user, string locId)
    {
        _audio.PlayPredicted(DenySound, chassis, user);
        _popup.PopupClient(Loc.GetString(locId), chassis, user);
    }

    #endregion

    #region Lock

    public bool IsLocked(EntityUid uid)
    {
        return TryComp<ModsuitLockComponent>(uid, out var comp) && comp.Locked;
    }

    public void SetLocked(Entity<ModsuitLockComponent> ent, bool locked)
    {
        if (ent.Comp.Locked == locked)
            return;

        ent.Comp.Locked = locked;
        Dirty(ent);
        Announce(ent);
    }

    /// <summary>
    ///     Wiping access is one-way: the suit stops caring who opens it.
    /// </summary>
    public void WipeAccess(Entity<ModsuitLockComponent> ent)
    {
        // The reader itself is engine-owned and off limits, so the wipe lives on our
        // own flag: every check below short-circuits on it.
        ent.Comp.AccessWiped = true;
        ent.Comp.Locked = false;
        Dirty(ent);
        Announce(ent);
    }

    /// <summary>
    ///     Swiping an ID that satisfies the reader flips the lock.
    /// </summary>
    private void OnInteractUsing(Entity<ModsuitLockComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || ent.Comp.AccessWiped)
            return;

        // Only cards; tools are the chassis system's business.
        if (!HasComp<IdCardComponent>(args.Used) && !HasComp<AccessComponent>(args.Used))
            return;

        args.Handled = true;

        if (!_access.IsAllowed(args.User, ent))
        {
            _popup.PopupClient(Loc.GetString("modsuit-lock-denied"), ent, args.User);
            _audio.PlayPredicted(DenySound, ent, args.User);
            return;
        }

        // An authorised card on an open panel is an override, not a toggle: the suit
        // belongs to a department, and the department can get its own hardware back off
        // whoever is standing in it. A closed panel is just the everyday lock.
        if (IsPanelOpen(ent))
        {
            var release = new ModsuitForceReleaseEvent(args.User);
            RaiseLocalEvent(ent, ref release);

            _popup.PopupClient(Loc.GetString("modsuit-lock-override"), ent, args.User);
            _audio.PlayPredicted(LockSound, ent, args.User);
            return;
        }

        SetLocked(ent, !ent.Comp.Locked);

        _popup.PopupClient(
            Loc.GetString(ent.Comp.Locked ? "modsuit-lock-engaged" : "modsuit-lock-released"),
            ent,
            args.User);

        _audio.PlayPredicted(LockSound, ent, args.User);
    }

    private void OnEmagged(Entity<ModsuitLockComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Access))
            return;

        // Both locks at once. The DNA lock sits in a module container where an emag
        // could never be pointed at it directly, and one emag opening everything is the
        // point of an emag — the module's own defence is that it is expensive to reach.
        BreakDnaLock(ent.Owner);

        if (ent.Comp.AccessWiped)
            return;

        WipeAccess(ent);
        args.Handled = true;
    }

    /// <summary>
    ///     Burns out whichever DNA lock the chassis is carrying, if any.
    /// </summary>
    private void BreakDnaLock(EntityUid chassis)
    {
        if (!TryComp<ModularChassisComponent>(chassis, out var comp))
            return;

        foreach (var module in _chassis.GetModuleEntities((chassis, comp)))
        {
            if (!TryComp<ModuleDnaLockComponent>(module, out var dna) || dna.Broken)
                continue;

            dna.Broken = true;
            dna.Dna = null;
            Dirty(module, dna);
            Announce(chassis);
        }
    }

    /// <summary>
    ///     A locked suit will not accept hardware, which is what makes the lock worth
    ///     bothering with at all.
    /// </summary>
    private void OnInstallAttempt(Entity<ModsuitLockComponent> ent, ref ChassisInstallModuleAttemptEvent args)
    {
        if (!ent.Comp.Locked)
            return;

        args.Cancelled = true;

        if (args.User is { } user)
        {
            _popup.PopupClient(Loc.GetString("modsuit-lock-denied"), ent, user);
            _audio.PlayPredicted(DenySound, ent, user);
        }
    }

    /// <summary>
    ///     Whether the maintenance panel is open. The panel is what separates "swipe to
    ///     lock" from "swipe to force it open" — you have to have got a screwdriver into
    ///     it first, which means the wearer was already in no position to object.
    /// </summary>
    private bool IsPanelOpen(EntityUid uid)
    {
        return TryComp<WiresPanelComponent>(uid, out var panel) && panel.Open;
    }

    #endregion

    #region Sabotage

    public bool IsInterfaceBroken(EntityUid uid)
    {
        return TryComp<ModsuitSabotageComponent>(uid, out var comp) && comp.InterfaceBroken;
    }

    public void SetInterfaceBroken(Entity<ModsuitSabotageComponent> ent, bool broken)
    {
        if (ent.Comp.InterfaceBroken == broken)
            return;

        ent.Comp.InterfaceBroken = broken;
        Dirty(ent);
        Announce(ent);
    }

    public void SetMalfunctioning(EntityUid uid, bool malfunctioning)
    {
        if (!TryComp<ChassisPowerComponent>(uid, out var power))
            return;

        power.Malfunctioning = malfunctioning;
        Dirty(uid, power);
        Announce(uid);
    }

    /// <summary>
    ///     Whether somebody has already taken the emergency release out of the picture.
    /// </summary>
    public bool IsReleaseCut(EntityUid uid)
    {
        return TryComp<ModsuitSabotageComponent>(uid, out var comp) && comp.ReleaseCut;
    }

    public void SetReleaseCut(Entity<ModsuitSabotageComponent> ent, bool cut)
    {
        if (ent.Comp.ReleaseCut == cut)
            return;

        ent.Comp.ReleaseCut = cut;
        Dirty(ent);
        Announce(ent);
    }

    public bool IsElectrified(Entity<ModsuitSabotageComponent> ent)
    {
        return ent.Comp.PermanentlyElectrified
               || ent.Comp.ElectrifiedUntil is { } until && _timing.CurTime < until;
    }

    public void Electrify(Entity<ModsuitSabotageComponent> ent, TimeSpan? duration)
    {
        if (duration == null)
        {
            ent.Comp.PermanentlyElectrified = true;
        }
        else
        {
            ent.Comp.ElectrifiedUntil = _timing.CurTime + duration.Value;
        }

        Dirty(ent);
        Announce(ent);
    }

    public void ClearElectrification(Entity<ModsuitSabotageComponent> ent)
    {
        ent.Comp.PermanentlyElectrified = false;
        ent.Comp.ElectrifiedUntil = null;
        Dirty(ent);
        Announce(ent);
    }

    /// <summary>
    ///     Tells the suit its security state moved. Nothing here can depend on the suit
    ///     system directly — that one already depends on this one — so the panel is told
    ///     through an event rather than reached for.
    /// </summary>
    private void Announce(EntityUid uid)
    {
        var ev = new ModsuitSecurityChangedEvent();
        RaiseLocalEvent(uid, ref ev);
    }

    #endregion
}
