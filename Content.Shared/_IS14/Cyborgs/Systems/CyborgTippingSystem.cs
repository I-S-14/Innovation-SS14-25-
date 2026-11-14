// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Alert;
using Content.Shared.Buckle;
using Content.Shared.Buckle.Components;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.Administration.Logs;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Content.Shared.Movement.Events;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._IS14.Cyborgs.Systems;

/// <summary>
/// Port of tgstation's /datum/component/tippable as set up for cyborgs.
/// </summary>
public sealed class CyborgTippingSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly AlertsSystem _alerts = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedBuckleSystem _buckle = default!;
    [Dependency] private readonly SharedCyborgSystem _cyborg = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    private const string HatSlot = "head";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgTippableComponent, DisarmAttemptEvent>(OnDisarmAttempt);
        SubscribeLocalEvent<CyborgTippableComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
        SubscribeLocalEvent<CyborgTippableComponent, CyborgTipDoAfterEvent>(OnTipDoAfter);
        SubscribeLocalEvent<CyborgTippableComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<CyborgTippableComponent, EmoteEvent>(OnEmote);
        SubscribeLocalEvent<CyborgTippableComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<CyborgTippableComponent, AttackAttemptEvent>(OnAttackAttempt);
        SubscribeLocalEvent<CyborgTippableComponent, CyborgTippedAlertEvent>(OnTippedAlert);
        SubscribeLocalEvent<CyborgTippableComponent, MoveInputEvent>(OnMoveInput);
    }

    // tg tips a cyborg with a right click in combat mode: here that is a shove.
    private void OnDisarmAttempt(Entity<CyborgTippableComponent> ent, ref DisarmAttemptEvent args)
    {
        if (args.Cancelled || args.TargetUid != ent.Owner || args.DisarmerUid == ent.Owner)
            return;

        args.Cancelled = true;
        StartDoAfter(ent, args.DisarmerUid);
    }

    private void OnGetVerbs(Entity<CyborgTippableComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.User == ent.Owner || !HasComp<HandsComponent>(args.User))
            return;

        var user = args.User;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString(ent.Comp.Tipped ? "cyborg-untip-verb" : "cyborg-tip-verb"),
            Act = () => StartDoAfter(ent, user),
            Priority = -1,
        });
    }

    private void StartDoAfter(Entity<CyborgTippableComponent> ent, EntityUid user)
    {
        var tipping = !ent.Comp.Tipped;
        var doAfter = new DoAfterArgs(EntityManager, user, tipping ? ent.Comp.TipTime : ent.Comp.UntipTime,
            new CyborgTipDoAfterEvent(tipping), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 1.5f,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        var name = Identity.Name(ent, EntityManager, user);
        _popup.PopupClient(Loc.GetString(tipping ? "cyborg-tip-start-user" : "cyborg-untip-start-user",
            ("target", name)), ent, user);
        _popup.PopupEntity(Loc.GetString(tipping ? "cyborg-tip-start-target" : "cyborg-untip-start-target",
            ("user", Identity.Name(user, EntityManager, ent))), ent, ent,
            tipping ? PopupType.MediumCaution : PopupType.Medium);
    }

    // Once the wait is over, trying to drive off starts rolling back over, like tg's resist.
    private void OnMoveInput(Entity<CyborgTippableComponent> ent, ref MoveInputEvent args)
    {
        if (ent.Comp.Tipped && args.State && args.HasDirectionalMovement)
            TrySelfRight(ent);
    }

    private void OnTippedAlert(Entity<CyborgTippableComponent> ent, ref CyborgTippedAlertEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TrySelfRight(ent);
    }

    /// <summary>
    /// The tipped cyborg tries to roll back over by itself: only after <see cref="CyborgTippableComponent.SelfRightTime"/>.
    /// </summary>
    public bool TrySelfRight(Entity<CyborgTippableComponent> ent)
    {
        if (!ent.Comp.Tipped)
            return false;

        if (_timing.CurTime < ent.Comp.SelfRightAt)
            return false;

        // Tipped cyborgs can't interact, which would otherwise stop their own do-after.
        var doAfter = new DoAfterArgs(EntityManager, ent, ent.Comp.SelfRightDoAfter,
            new CyborgTipDoAfterEvent(false), ent, ent)
        {
            BreakOnDamage = true,
            RequireCanInteract = false,
        };

        // Held movement keys keep asking: the duplicate block lets only the first one start.
        if (!_doAfter.TryStartDoAfter(doAfter))
            return false;

        _popup.PopupClient(Loc.GetString("cyborg-self-right-start"), ent, ent);
        return true;
    }

    private void OnTipDoAfter(Entity<CyborgTippableComponent> ent, ref CyborgTipDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Tipping == ent.Comp.Tipped)
            return;

        args.Handled = true;
        // A cyborg rolling itself back over counts as getting up on its own.
        SetTipped(ent, args.Tipping, args.User == ent.Owner ? null : args.User);
    }

    public void SetTipped(Entity<CyborgTippableComponent> ent, bool tipped, EntityUid? user = null)
    {
        if (ent.Comp.Tipped == tipped)
            return;

        ent.Comp.Tipped = tipped;
        ent.Comp.SelfRightAt = _timing.CurTime + ent.Comp.SelfRightTime;
        ent.Comp.AutoRightAt = _timing.CurTime + ent.Comp.AutoRightTime;
        Dirty(ent);
        UpdateAlert(ent);
        _movementSpeed.RefreshMovementSpeedModifiers(ent);

        var name = Identity.Name(ent, EntityManager);
        if (tipped)
        {
            _audio.PlayPredicted(ent.Comp.TipSound, ent, user);

            // Upside down nothing stays in the claws, the hat falls off and riders slide off, like tg.
            if (TryComp<CyborgComponent>(ent, out var cyborg))
                _cyborg.StoreAllTools((ent, cyborg));
            _inventory.TryUnequip(ent, HatSlot, silent: true, force: true, predicted: true);
            if (TryComp<StrapComponent>(ent, out var strap))
            {
                foreach (var rider in new List<EntityUid>(strap.BuckledEntities))
                {
                    _buckle.Unbuckle(rider, user);
                }
            }

            _popup.PopupPredicted(Loc.GetString("cyborg-tipped", ("target", name)), ent, user, PopupType.MediumCaution);
            if (user != null)
                _adminLog.Add(LogType.Action, LogImpact.Low,
                    $"{ToPrettyString(user.Value):player} tipped over {ToPrettyString(ent):target}");
        }
        else
        {
            _popup.PopupPredicted(Loc.GetString(user == null ? "cyborg-righted-self" : "cyborg-righted",
                ("target", name)), ent, user);
        }
    }

    private void OnRefreshSpeed(Entity<CyborgTippableComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        // Still turns on the spot, it just can't go anywhere.
        if (ent.Comp.Tipped)
            args.ModifySpeed(0f, 0f);
    }

    // tg's roleplay emotes: calling for help gets a tipped cyborg up sooner.
    private void OnEmote(Entity<CyborgTippableComponent> ent, ref EmoteEvent args)
    {
        if (!ent.Comp.Tipped || !ent.Comp.HelpEmotes.Contains(args.Emote.ID))
            return;

        var left = ent.Comp.SelfRightAt - _timing.CurTime;
        if (left <= TimeSpan.Zero)
            return;

        ent.Comp.SelfRightAt = _timing.CurTime + left * ent.Comp.HelpEmoteFactor;
        Dirty(ent);
        UpdateAlert(ent);
    }

    private void UpdateAlert(Entity<CyborgTippableComponent> ent)
    {
        if (!ent.Comp.Tipped)
        {
            _alerts.ClearAlert(ent.Owner, ent.Comp.TippedAlert);
            return;
        }

        // The cooldown runs out when the cyborg may get up by itself.
        var cooldown = ent.Comp.SelfRightAt > _timing.CurTime
            ? (_timing.CurTime, ent.Comp.SelfRightAt)
            : ((TimeSpan, TimeSpan)?) null;
        _alerts.ShowAlert(ent.Owner, ent.Comp.TippedAlert, cooldown: cooldown);
    }

    private void OnInteractionAttempt(Entity<CyborgTippableComponent> ent, ref InteractionAttemptEvent args)
    {
        if (ent.Comp.Tipped)
            args.Cancelled = true;
    }

    private void OnAttackAttempt(Entity<CyborgTippableComponent> ent, ref AttackAttemptEvent args)
    {
        if (ent.Comp.Tipped)
            args.Cancel();
    }

    public bool IsTipped(EntityUid uid)
    {
        return CompOrNull<CyborgTippableComponent>(uid)?.Tipped == true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<CyborgTippableComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Tipped && _timing.CurTime >= comp.AutoRightAt)
                SetTipped((uid, comp), false);
        }
    }
}

[Serializable, NetSerializable]
public sealed partial class CyborgTipDoAfterEvent : SimpleDoAfterEvent
{
    [DataField]
    public bool Tipping;

    public CyborgTipDoAfterEvent(bool tipping)
    {
        Tipping = tipping;
    }

    private CyborgTipDoAfterEvent()
    {
    }
}
