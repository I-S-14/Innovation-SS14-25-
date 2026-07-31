// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Containers;
using Content.Shared.Containers;
using Content.Shared.Interaction;
using Robust.Shared.Containers;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Containers;

/// <summary>
/// Racks that present what is in them rather than hiding it: clicks go through to the
/// contents, and the contents' appearance is copied onto the rack for its own sprite to
/// draw. Neither half knows what is being racked.
/// </summary>
public sealed class ContainedRelaySystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    /// How often relayed appearance is re-read. Anything that changes fast enough to
    /// need better than this should be networking its own visuals, not going through a
    /// rack.
    /// </summary>
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(1);

    private TimeSpan _nextRefresh;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContainedInteractionRelayComponent, InteractHandEvent>(OnInteractHand);

        // Nothing to show until ContainerFill has racked whatever a mapper asked for.
        SubscribeLocalEvent<ContainedAppearanceRelayComponent, MapInitEvent>(OnAppearanceMapInit,
            after: new[] { typeof(ContainerFillSystem) });

        SubscribeLocalEvent<ContainedAppearanceRelayComponent, EntInsertedIntoContainerMessage>(OnEntInserted);
        SubscribeLocalEvent<ContainedAppearanceRelayComponent, EntRemovedFromContainerMessage>(OnEntRemoved);
    }

    // ── Interaction ───────────────────────────────────────────────────────────

    private void OnInteractHand(EntityUid uid, ContainedInteractionRelayComponent component, InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (GetContents(uid, component.SlotId) is not { } contents)
            return;

        args.Handled = _interaction.InteractHand(args.User, contents);
    }

    // ── Appearance ────────────────────────────────────────────────────────────

    private void OnAppearanceMapInit(EntityUid uid, ContainedAppearanceRelayComponent component, MapInitEvent args)
    {
        RelayAppearance((uid, component));
    }

    private void OnEntInserted(EntityUid uid, ContainedAppearanceRelayComponent component, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == component.SlotId)
            RelayAppearance((uid, component));
    }

    private void OnEntRemoved(EntityUid uid, ContainedAppearanceRelayComponent component, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == component.SlotId)
            RelayAppearance((uid, component));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextRefresh)
            return;

        _nextRefresh = _timing.CurTime + RefreshInterval;

        var query = EntityQueryEnumerator<ContainedAppearanceRelayComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            RelayAppearance((uid, component));
        }
    }

    /// <summary>
    /// Copies the racked entity's appearance data onto the rack wholesale. Copying
    /// everything rather than a configured list keeps the rack ignorant of what it holds
    /// — the keys mean whatever the thing inside meant by them, and the rack's own
    /// visualizer is the only place that has to recognise them.
    /// </summary>
    private void RelayAppearance(Entity<ContainedAppearanceRelayComponent> ent)
    {
        if (!TryComp<AppearanceComponent>(ent, out var appearance))
            return;

        var contents = GetContents(ent.Owner, ent.Comp.SlotId);

        // Appended rather than copied: a copy would wipe the rack's own keys, including
        // the one being set right below it.
        if (contents != null && HasComp<AppearanceComponent>(contents))
            _appearance.AppendData(contents.Value, (ent.Owner, appearance));

        _appearance.SetData(ent, ContainedRelayVisuals.HasContents, contents != null, appearance);
    }

    /// <summary>First thing in the named slot, or null if there is nothing there.</summary>
    private EntityUid? GetContents(EntityUid uid, string slotId)
    {
        if (!_container.TryGetContainer(uid, slotId, out var container))
            return null;

        return container.ContainedEntities.Count > 0 ? container.ContainedEntities[0] : null;
    }
}
