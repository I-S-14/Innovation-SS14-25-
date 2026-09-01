// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Server.Chat.Systems;
using Content.Server.Electrocution;
using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modsuit.Systems;
using Content.Shared._IS14.Modular.Systems;
using Content.Shared.Chat;
using Content.Shared.Wires;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._IS14.Modsuit;

/// <summary>
///     The bite on the shock wire.
///
///     Arming is loud on purpose: the controller announces itself, so a hacker who set it
///     off by guessing knows what they have just done and everyone in earshot knows there
///     is a live suit in the room. Nothing about this is a silent trap — the cost of
///     surprise is paid by the person who touches the wire, not by the room.
///
///     Server-side because electrocution is: the damage, the paralysis and the insulation
///     check all live on the server, and predicting a discharge would leak it early.
/// </summary>
public sealed class ModsuitShockSystem : EntitySystem
{
    [Dependency] private readonly ChassisPowerSystem _power = default!;
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly ElectrocutionSystem _electrocution = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedModsuitLockSystem _lock = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    /// <summary>How long one arming holds the shell live.</summary>
    public static readonly TimeSpan ArmDuration = TimeSpan.FromSeconds(30);

    /// <summary>What one discharge costs the core.</summary>
    private const float ShockCost = 100f;

    /// <summary>Damage one discharge does, before insulation is taken into account.</summary>
    private const int ShockDamage = 25;

    /// <summary>How long the victim spends on the floor.</summary>
    private static readonly TimeSpan ShockStun = TimeSpan.FromSeconds(4);

    private static readonly SoundSpecifier ArmSound =
        new SoundPathSpecifier("/Audio/Machines/beep.ogg");

    private static readonly SoundSpecifier RejectSound =
        new SoundPathSpecifier("/Audio/Machines/buzz-sigh.ogg");

    private static readonly SoundSpecifier ShockSound =
        new SoundPathSpecifier("/Audio/Effects/Lightning/lightningshock.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModsuitSabotageComponent, BoundUIOpenedEvent>(OnUiOpened);
    }

    /// <summary>
    ///     Puts the shell live for half a minute, if the core can pay for it. Returns
    ///     whether the suit is now armed.
    /// </summary>
    public bool TryArm(Entity<ModsuitSabotageComponent> ent)
    {
        var live = _lock.IsElectrified(ent);

        // Nothing to hold the shell live with. Arming is free, but the standing draw is
        // not, and neither is a discharge — a flat suit says so rather than pretending.
        if (!live && _power.GetCharge(ent).Current < ShockCost)
        {
            _audio.PlayPvs(RejectSound, ent);
            Announce(ent, "modsuit-shock-voice-nopower");
            return false;
        }

        _lock.Electrify(ent, ArmDuration);

        // Re-arming a live shell only pushes the clock out; announcing it again would
        // just be the suit shouting at a hacker who already knows.
        if (live)
            return true;

        _audio.PlayPvs(ArmSound, ent);
        Announce(ent, "modsuit-shock-voice-armed");
        return true;
    }

    /// <summary>
    ///     Puts the shell through somebody. Does nothing on a suit that is not live, so
    ///     callers can fire it without checking first.
    /// </summary>
    public void Zap(Entity<ModsuitSabotageComponent> ent, EntityUid victim)
    {
        if (!_lock.IsElectrified(ent))
            return;

        // Every discharge comes out of the core. A suit that has been leaned on enough
        // times runs itself dry, which is the way past a defence nobody can reach.
        if (!_power.TryUseCharge(ent, ShockCost))
        {
            _audio.PlayPvs(RejectSound, ent);
            Announce(ent, "modsuit-shock-voice-nopower");
            _lock.ClearElectrification(ent);
            return;
        }

        // Shut the panel on them. Cutting the wire with the interface already open used to
        // leave the hacker knocked down but still looking at the wire list, which made the
        // defence a speed bump rather than a reason to stop.
        _ui.CloseUi(ent.Owner, WiresUiKey.Key, victim);

        _audio.PlayPvs(ShockSound, ent);
        Announce(ent, "modsuit-shock-voice-discharge");

        // Insulated gloves are checked inside: the counter to a live suit is the same
        // pair of gloves that counters a live grille, and nothing here should teach a
        // different lesson.
        _electrocution.TryDoElectrocution(victim, ent, ShockDamage, ShockStun, refresh: true);
    }

    private void OnUiOpened(Entity<ModsuitSabotageComponent> ent, ref BoundUIOpenedEvent args)
    {
        if (!Equals(args.UiKey, WiresUiKey.Key) || !_lock.IsElectrified(ent))
            return;

        Zap(ent, args.Actor);
    }

    /// <summary>
    ///     The controller talking. Hidden from the chat log — this is a machine noise with
    ///     words in it, not a conversation somebody should have to scroll past.
    /// </summary>
    private void Announce(EntityUid uid, string locId)
    {
        _chat.TrySendInGameICMessage(uid, Loc.GetString(locId), InGameICChatType.Speak, hideChat: true);
    }
}
