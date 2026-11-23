// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared._IS14.Cyborgs.Systems;
using Content.Shared.Eye;
using Content.Shared.Examine;
using Content.Shared.Mind;
using Content.Shared.Mind.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.PowerCell;
using Content.Shared.Xenoborgs.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Player;
using System.Numerics;

namespace Content.Server._IS14.Cyborgs;

/// <summary>
/// The core keeps its mind and cyborg HUD. Like the abductor console, only its eye and movement input are relayed.
/// </summary>
public sealed class MothershipEyeSystem : EntitySystem
{
    [Dependency] private readonly SharedEyeSystem _eyes = default!;
    [Dependency] private readonly SharedMoverController _mover = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PowerCellSystem _power = default!;
    [Dependency] private readonly SharedCyborgSystem _cyborg = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;

    private readonly Dictionary<EntityUid, MapCoordinates> _followPositions = new();
    private float _uiTimer;

    public override void Initialize()
    {
        Subs.BuiEvents<MothershipEyeComponent>(MothershipEyeUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<MothershipEyeMessage>(OnMessage);
        });
        SubscribeLocalEvent<MothershipEyeComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MothershipEyeComponent, MindRemovedMessage>(OnMindRemoved);
    }

    private void OnOpened(Entity<MothershipEyeComponent> ent, ref BoundUIOpenedEvent args) => UpdateUi(ent);

    private void OnMessage(Entity<MothershipEyeComponent> ent, ref MothershipEyeMessage args)
    {
        // This is a private console belonging to the core, not a way for nearby players to move its eye.
        if (args.Actor != ent.Owner)
            return;

        switch (args.Command)
        {
            case MothershipEyeCommand.Return:
                Return(ent);
                break;
            case MothershipEyeCommand.Ship:
                Start(ent);
                break;
            case MothershipEyeCommand.Jump:
            case MothershipEyeCommand.Follow:
                if (args.Target is { } target && TryGetEntity(target, out var borg))
                    Start(ent, borg, args.Command == MothershipEyeCommand.Follow);
                break;
        }
        UpdateUi(ent);
    }

    private bool IsUsableBorg(EntityUid uid) =>
        HasComp<XenoborgComponent>(uid) && !HasComp<MothershipCoreComponent>(uid) &&
        HasComp<CyborgComponent>(uid) && !_mobState.IsIncapacitated(uid) && _power.HasDrawCharge(uid) &&
        !TerminatingOrDeleted(uid);

    /// <summary>Start at the core or jump to a live xenoborg. No station cameras or arbitrary targets are accepted.</summary>
    public bool Start(Entity<MothershipEyeComponent> ent, EntityUid? borg = null, bool follow = false)
    {
        if (!TryComp<CyborgComponent>(ent, out var cyborg) || !_cyborg.CanActivate((ent, cyborg)) ||
            !HasComp<ActorComponent>(ent) || Transform(ent).GridUid == null ||
            borg is { } target && !IsUsableBorg(target))
            return false;

        Return(ent);
        var eye = Spawn("IS14MothershipEye", Transform(borg ?? ent.Owner).Coordinates);
        ent.Comp.Eye = eye;
        ent.Comp.Following = follow ? borg : null;
        if (ent.Comp.Following is { } following)
            _followPositions[ent.Owner] = _transform.GetMapCoordinates(following);
        Dirty(ent);
        var coreEye = Comp<EyeComponent>(ent);
        ent.Comp.AddedVisibilityMask = (int) VisibilityFlags.Ghost & ~coreEye.VisibilityMask;
        _eyes.SetVisibilityMask(ent.Owner, coreEye.VisibilityMask | ent.Comp.AddedVisibilityMask, coreEye);
        _eyes.SetTarget(ent.Owner, eye);
        // Camera coverage is masked by the overlay, not the remote eye's wall occlusion.
        _eyes.SetDrawFov(ent.Owner, false);
        _mover.SetRelay(ent.Owner, eye);
        return true;
    }

    public void Return(Entity<MothershipEyeComponent> ent)
    {
        var eye = ent.Comp.Eye;
        ent.Comp.Eye = null;
        ent.Comp.Following = null;
        _followPositions.Remove(ent.Owner);
        RemComp<RelayInputMoverComponent>(ent);
        if (TryComp<EyeComponent>(ent, out var coreEye))
        {
            _eyes.SetVisibilityMask(ent.Owner, coreEye.VisibilityMask & ~ent.Comp.AddedVisibilityMask, coreEye);
            _eyes.SetTarget(ent.Owner, null);
            _eyes.SetDrawFov(ent.Owner, false);
        }
        ent.Comp.AddedVisibilityMask = 0;
        Dirty(ent);
        if (eye is { } oldEye && !TerminatingOrDeleted(oldEye))
            QueueDel(oldEye);
    }

    /// <summary>The ship and the union of the xenoborgs' unobstructed view areas form the eye's network.</summary>
    public bool InCoverage(Entity<MothershipEyeComponent> ent, EntityUid eye)
    {
        var position = _transform.GetMapCoordinates(eye);
        var homeGrid = Transform(ent).GridUid;
        if (homeGrid != null && _map.TryFindGridAt(position, out var grid, out _) && grid == homeGrid)
            return true;

        var query = EntityQueryEnumerator<XenoborgComponent, CyborgComponent>();
        while (query.MoveNext(out var borg, out _, out _))
        {
            if (IsUsableBorg(borg) && _examine.InRangeUnOccluded(borg, position, ent.Comp.ViewRange))
                return true;
        }
        return false;
    }

    public override void Update(float frameTime)
    {
        _uiTimer += frameTime;
        var refreshUi = _uiTimer >= 1f;
        if (refreshUi)
            _uiTimer = 0;

        var query = EntityQueryEnumerator<MothershipEyeComponent, CyborgComponent>();
        while (query.MoveNext(out var uid, out var component, out var cyborg))
        {
            Entity<MothershipEyeComponent> ent = (uid, component);
            if (component.Eye is { } eye)
            {
                if (TerminatingOrDeleted(eye) || !HasComp<ActorComponent>(uid) ||
                    !_cyborg.CanActivate((uid, cyborg)))
                {
                    Return(ent);
                    UpdateUi(ent);
                    continue;
                }

                if (component.Following is { } following)
                {
                    if (!IsUsableBorg(following))
                    {
                        Return(ent);
                        UpdateUi(ent);
                        continue;
                    }
                    var next = _transform.GetMapCoordinates(following);
                    if (_followPositions.TryGetValue(uid, out var previous))
                    {
                        var current = _transform.GetMapCoordinates(eye);
                        var offset = current.MapId == previous.MapId ? current.Position - previous.Position : Vector2.Zero;
                        _transform.SetMapCoordinates(eye, new MapCoordinates(next.Position + offset, next.MapId));
                    }
                    _followPositions[uid] = next;
                }

                if (!InCoverage(ent, eye))
                {
                    Return(ent);
                    UpdateUi(ent);
                }
            }

            if (refreshUi && _ui.IsUiOpen(uid, MothershipEyeUiKey.Key))
                UpdateUi(ent);
        }
    }

    private void UpdateUi(Entity<MothershipEyeComponent> ent)
    {
        var borgs = new List<(NetEntity Entity, string Name)>();
        var query = EntityQueryEnumerator<XenoborgComponent, CyborgComponent>();
        while (query.MoveNext(out var borg, out _, out _))
        {
            if (IsUsableBorg(borg))
                borgs.Add((GetNetEntity(borg), Name(borg)));
        }
        borgs.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        _ui.SetUiState(ent.Owner, MothershipEyeUiKey.Key,
            new MothershipEyeState(borgs, ent.Comp.Eye != null,
                ent.Comp.Following is { } following && Exists(following) ? Name(following) : null));
    }

    private void OnMindRemoved(Entity<MothershipEyeComponent> ent, ref MindRemovedMessage args) => Return(ent);
    private void OnShutdown(Entity<MothershipEyeComponent> ent, ref ComponentShutdown args) => Return(ent);
}
