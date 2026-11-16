// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Client.Silicons.StationAi;
using Content.Shared._IS14.Cyborgs;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.PowerCell;
using Content.Shared.StationAi;
using Content.Shared.Xenoborgs.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Player;

namespace Content.Client._IS14.Cyborgs;

/// <summary>Shows the existing AI camera-static renderer only while the local core is using its remote eye.</summary>
public sealed class MothershipEyeOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly PowerCellSystem _power = default!;

    private StationAiOverlay? _overlay;
    private EntityUid? _eye;
    private readonly List<Entity<StationAiVisionComponent>> _sources = new();

    public override void Initialize()
    {
        SubscribeLocalEvent<MothershipEyeComponent, AfterAutoHandleStateEvent>(OnState);
        SubscribeLocalEvent<MothershipEyeComponent, LocalPlayerAttachedEvent>(OnAttached);
        SubscribeLocalEvent<MothershipEyeComponent, LocalPlayerDetachedEvent>(OnDetached);
        SubscribeLocalEvent<MothershipEyeComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnState(Entity<MothershipEyeComponent> ent, ref AfterAutoHandleStateEvent args) => Refresh(ent);
    private void OnAttached(Entity<MothershipEyeComponent> ent, ref LocalPlayerAttachedEvent args) => Refresh(ent);
    private void OnDetached(Entity<MothershipEyeComponent> ent, ref LocalPlayerDetachedEvent args) => Remove();
    private void OnShutdown(Entity<MothershipEyeComponent> ent, ref ComponentShutdown args)
    {
        if (_player.LocalEntity == ent.Owner)
            Remove();
    }

    private void Refresh(Entity<MothershipEyeComponent> ent)
    {
        if (_player.LocalEntity != ent.Owner)
            return;
        if (_eye == ent.Comp.Eye)
            return;
        Remove();
        if (ent.Comp.Eye is not { } eye)
            return;
        _eye = eye;
        _overlay = new StationAiOverlay(eye, () => GetSources(ent));
        if (!_overlays.AddOverlay(_overlay))
        {
            _overlay.Dispose();
            _overlay = null;
            _eye = null;
        }
    }

    private IReadOnlyList<Entity<StationAiVisionComponent>> GetSources(Entity<MothershipEyeComponent> core)
    {
        _sources.Clear();
        if (!Exists(core.Owner))
            return _sources;
        _sources.Add(StationAiVisionComponent.CreateViewSource(core.Owner, core.Comp.ViewRange));
        var query = EntityQueryEnumerator<XenoborgComponent, CyborgComponent>();
        while (query.MoveNext(out var uid, out _, out _))
        {
            if (uid == core.Owner || HasComp<MothershipCoreComponent>(uid) ||
                _mobState.IsIncapacitated(uid) || !_power.HasDrawCharge(uid))
                continue;
            _sources.Add(StationAiVisionComponent.CreateViewSource(uid, core.Comp.ViewRange));
        }
        return _sources;
    }

    private void Remove()
    {
        if (_overlay != null)
        {
            if (_overlays.TryGetOverlay<StationAiOverlay>(out var active) && ReferenceEquals(active, _overlay))
                _overlays.RemoveOverlay(_overlay);
            _overlay.Dispose();
        }
        _overlay = null;
        _eye = null;
        _sources.Clear();
    }

    public override void Shutdown()
    {
        Remove();
        base.Shutdown();
    }
}
