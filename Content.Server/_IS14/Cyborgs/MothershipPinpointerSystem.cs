// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Server.Pinpointer;
using Content.Shared._IS14.Pinpointer;
using Content.Shared.Interaction;
using Content.Shared.Pinpointer;
using Content.Shared.Verbs;
using Content.Shared.Xenoborgs.Components;
using Robust.Server.GameObjects;

namespace Content.Server._IS14.Cyborgs;

/// <summary>Chooses an explicit xenoborg target while retaining the normal pinpointer's arrow and distance logic.</summary>
public sealed class MothershipPinpointerSystem : EntitySystem
{
    [Dependency] private readonly SharedPinpointerSystem _pinpointers = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    private float _refresh;

    public override void Initialize()
    {
        SubscribeLocalEvent<MothershipPinpointerComponent, ActivateInWorldEvent>(OnActivate,
            before: new[] { typeof(PinpointerSystem) });
        SubscribeLocalEvent<MothershipPinpointerComponent, GetVerbsEvent<AlternativeVerb>>(OnVerbs);
        SubscribeLocalEvent<MothershipPinpointerComponent, MapInitEvent>(OnMapInit);
        Subs.BuiEvents<MothershipPinpointerComponent>(MothershipPinpointerUiKey.Key, subs =>
        {
            subs.Event<BoundUIOpenedEvent>(OnOpened);
            subs.Event<MothershipPinpointerSelectMessage>(OnSelect);
        });
    }

    private void OnMapInit(Entity<MothershipPinpointerComponent> ent, ref MapInitEvent args) => Refresh(ent);

    private void OnActivate(Entity<MothershipPinpointerComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled || !args.Complex)
            return;
        args.Handled = true;
        _ui.TryOpenUi(ent.Owner, MothershipPinpointerUiKey.Key, args.User);
    }

    private void OnVerbs(Entity<MothershipPinpointerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;
        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("mothership-pinpointer-select"),
            Act = () => _ui.TryOpenUi(ent.Owner, MothershipPinpointerUiKey.Key, user)
        });
    }

    private void OnOpened(Entity<MothershipPinpointerComponent> ent, ref BoundUIOpenedEvent args) => Refresh(ent);

    private void OnSelect(Entity<MothershipPinpointerComponent> ent, ref MothershipPinpointerSelectMessage args)
    {
        if (args.Target is not { } net)
        {
            if (TryComp<PinpointerComponent>(ent, out var pointer) && pointer.IsActive)
                _pinpointers.TogglePinpointer(ent.Owner);
            Refresh(ent);
            return;
        }
        if (TryGetEntity(net, out var target))
            Select(ent, target.Value);
    }

    public bool Select(Entity<MothershipPinpointerComponent> ent, EntityUid target)
    {
        if (!IsTarget(target))
            return false;
        ent.Comp.AutoCore = false;
        ent.Comp.Selected = target;
        Dirty(ent);
        _pinpointers.SetTargets(ent.Owner, new() { target });
        if (TryComp<PinpointerComponent>(ent, out var pointer) && !pointer.IsActive)
            _pinpointers.TogglePinpointer(ent.Owner);
        Refresh(ent);
        return true;
    }

    private bool IsTarget(EntityUid target) => !TerminatingOrDeleted(target) &&
        (HasComp<MothershipCoreComponent>(target) || HasComp<XenoborgComponent>(target));

    private void Refresh(Entity<MothershipPinpointerComponent> ent)
    {
        if (!TryComp<PinpointerComponent>(ent, out var pinpointer))
            return;
        if (ent.Comp.Selected is { } lost && !IsTarget(lost))
        {
            ent.Comp.Selected = null;
            Dirty(ent);
        }
        if (ent.Comp.AutoCore && (ent.Comp.Selected is not { } selected || !IsTarget(selected)))
        {
            EntityUid? nearest = null;
            var best = float.MaxValue;
            var origin = _transform.GetMapCoordinates(ent.Owner);
            var cores = EntityQueryEnumerator<MothershipCoreComponent>();
            while (cores.MoveNext(out var core, out _))
            {
                var position = _transform.GetMapCoordinates(core);
                var distance = origin.MapId == position.MapId
                    ? (origin.Position - position.Position).LengthSquared() : float.MaxValue;
                if (nearest != null && distance >= best)
                    continue;
                nearest = core;
                best = distance;
            }
            if (ent.Comp.Selected != nearest)
            {
                ent.Comp.Selected = nearest;
                Dirty(ent);
            }
        }
        var target = ent.Comp.Selected;
        _pinpointers.SetTargets(ent.Owner, target is { } uid && IsTarget(uid) ? new() { uid } : new());
        if (pinpointer.Targets.Count == 0)
            _pinpointers.SetDistance(ent.Owner, Distance.Unknown);
        _pinpointers.SetTargetName(ent.Owner, target is { } named && IsTarget(named)
            ? Name(named) : Loc.GetString("mothership-pinpointer-unavailable"));
        _appearance.SetData(ent.Owner, PinpointerVisuals.IsActive, pinpointer.IsActive);
        _appearance.SetData(ent.Owner, PinpointerVisuals.TargetDistance, pinpointer.DistanceToTarget);

        if (!_ui.IsUiOpen(ent.Owner, MothershipPinpointerUiKey.Key))
            return;
        var targets = new List<(NetEntity Entity, string Name)>();
        var query = EntityQueryEnumerator<XenoborgComponent>();
        while (query.MoveNext(out var entity, out _))
        {
            if (!IsTarget(entity))
                continue;
            var name = HasComp<MothershipCoreComponent>(entity)
                ? Loc.GetString("mothership-pinpointer-core", ("name", Name(entity))) : Name(entity);
            targets.Add((GetNetEntity(entity), name));
        }
        targets.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));
        _ui.SetUiState(ent.Owner, MothershipPinpointerUiKey.Key,
            new MothershipPinpointerState(targets,
                target is { } current && IsTarget(current) ? GetNetEntity(current) : null, pinpointer.IsActive));
    }

    public override void Update(float frameTime)
    {
        _refresh += frameTime;
        if (_refresh < 1f)
            return;
        _refresh = 0;
        var query = EntityQueryEnumerator<MothershipPinpointerComponent>();
        while (query.MoveNext(out var uid, out var component))
            Refresh((uid, component));
    }
}
