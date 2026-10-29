// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.Effects.Systems;
using Content.Shared.Gravity;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Events;
using Content.Shared.Popups;
using Content.Shared.Standing;
using Robust.Shared.Containers;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Serialization;

namespace Content.Shared.Movement.Systems;

public abstract partial class SharedJetpackSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] protected readonly SharedAppearanceSystem Appearance = default!;
    [Dependency] protected readonly SharedContainerSystem Container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly ActionContainerSystem _actionContainer = default!;
    [Dependency] private readonly StandingStateSystem _standing = default!; // Goobstation
    [Dependency] private MovementSpeedModifierSystem _movementSpeedModifier = default!;
    [Dependency] protected SharedAppearanceSystem Appearance = default!;
    [Dependency] private SharedContainerSystem Container = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedPhysicsSystem _physics = default!;
    [Dependency] private ActionContainerSystem _actionContainer = default!;
    [Dependency] private SharedParticleEmitterSystem _particleEmitter = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<JetpackComponent, GetItemActionsEvent>(OnJetpackGetAction);
        SubscribeLocalEvent<JetpackComponent, DroppedEvent>(OnJetpackDropped);
        SubscribeLocalEvent<JetpackComponent, ToggleJetpackEvent>(OnJetpackToggle);

        SubscribeLocalEvent<JetpackUserComponent, RefreshWeightlessModifiersEvent>(OnJetpackUserWeightlessMovement);
        SubscribeLocalEvent<JetpackUserComponent, CanWeightlessMoveEvent>(OnJetpackUserCanWeightless);
        SubscribeLocalEvent<JetpackUserComponent, EntParentChangedMessage>(OnJetpackUserEntParentChanged);
        SubscribeLocalEvent<JetpackComponent, EntGotInsertedIntoContainerMessage>(OnJetpackMoved);

        SubscribeLocalEvent<GravityChangedEvent>(OnJetpackUserGravityChanged);
        SubscribeLocalEvent<JetpackComponent, MapInitEvent>(OnMapInit);

        SubscribeLocalEvent<JetpackUserComponent, DownedEvent>(OnDowned); // Goobstation
    }

    private void OnDowned(Entity<JetpackUserComponent> ent, ref DownedEvent args) // Goobstation
    {
        if (!TryComp<JetpackComponent>(ent.Comp.Jetpack, out var jetpack))
            return;

        SetEnabled(ent.Comp.Jetpack, jetpack, false, ent);

        _popup.PopupClient(Loc.GetString("jetpack-downed"), ent, ent);
    }

    [SubscribeLocalEvent]
    private void OnJetpackUserWeightlessMovement(Entity<JetpackUserComponent> ent, ref RefreshWeightlessModifiersEvent args)
    {
        // Yes, this bulldozes the values but primarily for backwards compat atm.
        args.WeightlessAcceleration = ent.Comp.WeightlessAcceleration;
        args.WeightlessModifier = ent.Comp.WeightlessModifier;
        args.WeightlessFriction = ent.Comp.WeightlessFriction;
        args.WeightlessFrictionNoInput = ent.Comp.WeightlessFrictionNoInput;
    }

    [SubscribeLocalEvent]
    private void OnMapInit(Entity<JetpackComponent> ent, ref MapInitEvent args)
    {
        _actionContainer.EnsureAction(ent.Owner, ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
        Dirty(ent);
    }

    [SubscribeLocalEvent]
    private void OnJetpackUserGravityChanged(ref GravityChangedEvent ev)
    {
        var gridUid = ev.ChangedGridIndex;
        var query = EntityQueryEnumerator<JetpackUserComponent, TransformComponent>();
        while (query.MoveNext(out var uid, out var user, out var transform))
        {
            if (transform.GridUid != gridUid || !ev.HasGravity ||
                !_jetpackQuery.TryGetComponent(user.Jetpack, out var jetpack))
                continue;

            _popup.PopupEntity(Loc.GetString("jetpack-to-grid"), uid, uid);
            SetEnabled((user.Jetpack, jetpack), false, uid);
        }
    }

    [SubscribeLocalEvent]
    private void OnJetpackDropped(Entity<JetpackComponent> ent, ref DroppedEvent args)
    {
        SetEnabled(ent.AsNullable(), false, args.User);
    }

    [SubscribeLocalEvent]
    private void OnJetpackMoved(Entity<JetpackComponent> ent, ref EntGotInsertedIntoContainerMessage args)
    {
        if (args.Container.Owner != ent.Comp.JetpackUser)
            SetEnabled(ent.AsNullable(), false, ent.Comp.JetpackUser);
    }

    [SubscribeLocalEvent]
    private void OnJetpackUserCanWeightless(Entity<JetpackUserComponent> ent, ref CanWeightlessMoveEvent args)
    {
        args.CanMove = true;
    }

    [SubscribeLocalEvent]
    private void OnJetpackUserEntParentChanged(Entity<JetpackUserComponent> ent, ref EntParentChangedMessage args)
    {
        if (!_jetpackQuery.TryGetComponent(ent.Comp.Jetpack, out var jetpack) ||
            CanEnableOnGrid(args.Transform.GridUid))
            return;

        SetEnabled((ent.Comp.Jetpack, jetpack), false, ent.Owner);
        _popup.PopupEntity(Loc.GetString("jetpack-to-grid"), ent.Owner, ent.Owner);
    }

    private void SetupUser(Entity<JetpackComponent> ent, EntityUid user)
    {
        EnsureComp<JetpackUserComponent>(user, out var userComp);
        ent.Comp.JetpackUser = user;

        if (TryComp<PhysicsComponent>(user, out var physics))
            _physics.SetBodyStatus(user, physics, BodyStatus.InAir);

        userComp.Jetpack = ent.Owner;
        userComp.WeightlessAcceleration = ent.Comp.Acceleration;
        userComp.WeightlessModifier = ent.Comp.WeightlessModifier;
        userComp.WeightlessFriction = ent.Comp.Friction;
        userComp.WeightlessFrictionNoInput = ent.Comp.Friction;

        _movementSpeedModifier.RefreshWeightlessModifiers(user);
    }

    private void RemoveUser(Entity<JetpackComponent> ent, EntityUid user)
    {
        if (!RemComp<JetpackUserComponent>(user))
            return;

        ent.Comp.JetpackUser = null;

        if (TryComp<PhysicsComponent>(user, out var physics))
            _physics.SetBodyStatus(user, physics, BodyStatus.OnGround);

        _movementSpeedModifier.RefreshWeightlessModifiers(user);
    }

    [SubscribeLocalEvent]
    private void OnJetpackToggle(Entity<JetpackComponent> ent, ref ToggleJetpackEvent args)
    {
        if (args.Handled)
            return;

        if (!CanEnableOnGrid(Transform(ent.Owner).GridUid))
        {
            _popup.PopupEntity(Loc.GetString("jetpack-no-station"), ent.Owner, args.Performer);
            return;
        }

        if (_standing.IsDown(args.Performer)) // Goobstation
        {
            _popup.PopupClient(Loc.GetString("jetpack-is-down"), uid, args.Performer);

            return;
        }

        SetEnabled(uid, component, !IsEnabled(uid));
    }

    private bool CanEnableOnGrid(EntityUid? gridUid)
    {
        // No and no again! Do not attempt to activate the jetpack on a grid with gravity disabled. You will not be the first or the last to try this.
        // https://discord.com/channels/310555209753690112/310555209753690112/1270067921682694234
        return gridUid == null || !HasComp<GravityComponent>(gridUid);
    }

    [SubscribeLocalEvent]
    private void OnJetpackGetAction(Entity<JetpackComponent> ent, ref GetItemActionsEvent args)
    {
        args.AddAction(ref ent.Comp.ToggleActionEntity, ent.Comp.ToggleAction);
    }

    private bool IsEnabled(EntityUid uid)
    {
        return HasComp<ActiveJetpackComponent>(uid);
    }

    public void SetEnabled(Entity<JetpackComponent?> ent, bool enabled, EntityUid? user = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (IsEnabled(ent.Owner) == enabled ||
            enabled && !CanEnable((ent.Owner, ent.Comp)))
            return;

        if (user == null)
        {
            if (!Container.TryGetContainingContainer((ent.Owner, null, null), out var container))
                return;

            user = container.Owner;
        }

        if (enabled)
        {
            if (TryComp<JetpackUserComponent>(user, out var userComp) &&
                userComp.Jetpack != ent.Owner &&
                _jetpackQuery.TryGetComponent(userComp.Jetpack, out var oldJetpack))
                SetEnabled((userComp.Jetpack, oldJetpack), false, user);

            SetupUser((ent.Owner, ent.Comp), user.Value);
            EnsureComp<ActiveJetpackComponent>(ent.Owner);
        }
        else
        {
            RemoveUser((ent.Owner, ent.Comp), user.Value);
            RemComp<ActiveJetpackComponent>(ent.Owner);
        }

        _particleEmitter.SetEnabled(ent.Owner, enabled);

        // Goob edit - jetpack state might have changed by the time "enabled" is any relevant
        Appearance.SetData(uid, JetpackVisuals.Enabled, HasComp<ActiveJetpackComponent>(uid));
        Dirty(uid, component);
    }

    protected virtual bool CanEnable(Entity<JetpackComponent> ent)
    {
        return true;
    }
}

[Serializable, NetSerializable]
public enum JetpackVisuals : byte
{
    Enabled,
    Layer
}
