// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Jittering;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Speech.EntitySystems;
using Content.Shared.StatusEffect;
using Content.Shared.Stunnable;
using Content.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._IS14.Cyborgs.Systems;

public sealed class CyborgHarmAlarmSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedJitteringSystem _jitter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedStutteringSystem _stutter = default!;
    [Dependency] private readonly SharedStunSystem _stun = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;

    private readonly HashSet<Entity<StatusEffectsComponent>> _targets = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgHarmAlarmComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<CyborgHarmAlarmComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        if (TryComp<UseDelayComponent>(ent, out var delay) && _useDelay.IsDelayed((ent.Owner, delay)))
            return;

        if (!_powerCell.TryUseCharge(args.User, ent.Comp.ChargeCost, args.User, predicted: true))
        {
            _popup.PopupClient(Loc.GetString("cyborg-tools-no-power"), ent, args.User);
            return;
        }

        if (delay != null)
            _useDelay.TryResetDelay((ent.Owner, delay));

        _audio.PlayPredicted(ent.Comp.Sound, args.User, args.User);

        // Status effects on other players aren't predicted.
        if (_net.IsClient)
            return;

        var emagged = TryComp<CyborgComponent>(args.User, out var cyborg) && cyborg.EmagToolsUnlocked;

        _targets.Clear();
        _lookup.GetEntitiesInRange(Transform(args.User).Coordinates, ent.Comp.Range, _targets);
        foreach (var target in _targets)
        {
            // Other cyborgs don't have ears to hurt.
            if (target.Owner == args.User || HasComp<CyborgComponent>(target))
                continue;

            _jitter.DoJitter(target, ent.Comp.Duration, true);
            _stutter.DoStutter(target, ent.Comp.Duration, true);

            if (emagged)
                _stun.TryKnockdown(target.Owner, ent.Comp.EmaggedKnockdown);
        }
    }
}
