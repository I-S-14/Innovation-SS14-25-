// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modular;
using Content.Shared._IS14.Modular.Components;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Emag.Components;
using Content.Shared.Emag.Systems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
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
    }

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

        SetLocked(ent, !ent.Comp.Locked);

        _popup.PopupClient(
            Loc.GetString(ent.Comp.Locked ? "modsuit-lock-engaged" : "modsuit-lock-released"),
            ent,
            args.User);

        _audio.PlayPredicted(LockSound, ent, args.User);
    }

    private void OnEmagged(Entity<ModsuitLockComponent> ent, ref GotEmaggedEvent args)
    {
        if (!_emag.CompareFlag(args.Type, EmagType.Access) || ent.Comp.AccessWiped)
            return;

        WipeAccess(ent);
        args.Handled = true;
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
    }

    public void SetMalfunctioning(EntityUid uid, bool malfunctioning)
    {
        if (!TryComp<ChassisPowerComponent>(uid, out var power))
            return;

        power.Malfunctioning = malfunctioning;
        Dirty(uid, power);
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
    }

    public void ClearElectrification(Entity<ModsuitSabotageComponent> ent)
    {
        ent.Comp.PermanentlyElectrified = false;
        ent.Comp.ElectrifiedUntil = null;
        Dirty(ent);
    }

    #endregion
}
