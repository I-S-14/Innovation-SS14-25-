// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions.Components;
using Content.Shared.Actions.Events;
using Content.Shared.Popups;
using Robust.Shared.Timing;

namespace Content.Shared.Actions;

/// <summary>
/// Handles action priming, confirmation and automatic unpriming.
/// </summary>
public sealed partial class ConfirmableActionSystem : EntitySystem
{
    [Dependency] private readonly SharedActionsSystem _actions = default!; // Goobstation
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedPopupSystem _popup = default!;

    [Dependency] private EntityQuery<ActionComponent> _actionQuery = default!;

    /// <inheritdoc/>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // handle automatic unpriming
        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<ConfirmableActionComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.NextUnprime is not {} time)
                continue;

            if (now >= time)
                Unprime((uid, comp));
        }
    }

    [SubscribeLocalEvent]
    private void OnAttempt(Entity<ConfirmableActionComponent> ent, ref ActionAttemptEvent args)
    {
        if (!ent.Comp.ShouldCancel) // Goobstation
            return;

        if (args.Cancelled)
            return;

        // Check if we should be confirming based on the action's toggle status.
        if (_actionQuery.TryComp(ent, out var action))
        {
            if (action.Toggled && !ent.Comp.ConfirmWhenToggled)
                return;
            if (!action.Toggled && !ent.Comp.ConfirmWhenUntoggled)
                return;
        }

        // if not primed, prime it and cancel the action
        if (ent.Comp.NextConfirm is not { } confirm)
        {
            Prime(ent, args.User);
            args.Cancelled = true;
            return;
        }

        // primed but the delay isnt over, cancel the action
        if (_timing.CurTime < confirm)
        {
            args.Cancelled = true;
            return;
        }

        // primed and delay has passed, let the action go through
        Unprime(ent);
    }

    public void Prime(Entity<ConfirmableActionComponent> ent, EntityUid user) // Goob edit
    {
        var (uid, comp) = ent;
        comp.NextConfirm = _timing.CurTime + comp.ConfirmDelay;
        comp.NextUnprime = comp.NextConfirm + comp.PrimeTime;
        Dirty(uid, comp);

        // Goobstation - Confirmable action with changed icon - Start
        if (!string.IsNullOrEmpty(comp.Popup))
            _popup.PopupClient(Loc.GetString(comp.Popup), user, user, comp.PopupFontType);

        _actions.SetToggled(ent.Owner, true);
        // Goobstation - Confirmable action with changed icon - End
    }

    public void Unprime(Entity<ConfirmableActionComponent> ent) // Goob edit
    {
        var (uid, comp) = ent;
        comp.NextConfirm = null;
        comp.NextUnprime = null;

        _actions.SetToggled(ent.Owner, false); // Goobstation - Confirmable action with changed icon

        Dirty(uid, comp);
    }
}
