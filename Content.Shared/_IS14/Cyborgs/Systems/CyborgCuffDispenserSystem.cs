// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Cuffs;
using Content.Shared.Cuffs.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands.Components;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Cyborgs.Systems;

public sealed class CyborgCuffDispenserSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedCuffableSystem _cuffable = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgCuffDispenserComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<CyborgCuffDispenserComponent, CyborgCuffDoAfterEvent>(OnDoAfter);
    }

    private void OnAfterInteract(Entity<CyborgCuffDispenserComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target ||
            !TryComp<CuffableComponent>(target, out var cuffable))
            return;

        args.Handled = true;

        if (!CanCuff(target, cuffable, args.User))
            return;

        var doAfter = new DoAfterArgs(EntityManager, args.User, ent.Comp.CuffTime, new CyborgCuffDoAfterEvent(),
            ent, target, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            DistanceThreshold = 1f,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;

        _audio.PlayPredicted(ent.Comp.StartSound, ent, args.User);
        _popup.PopupClient(Loc.GetString("handcuff-component-start-cuffing-target-message",
            ("targetName", Identity.Name(target, EntityManager, args.User))), args.User, args.User);
        _popup.PopupEntity(Loc.GetString("handcuff-component-start-cuffing-by-other-message",
            ("otherName", Identity.Name(args.User, EntityManager, target))), target, target);
    }

    private void OnDoAfter(Entity<CyborgCuffDispenserComponent> ent, ref CyborgCuffDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target ||
            !TryComp<CuffableComponent>(target, out var cuffable))
            return;

        args.Handled = true;

        if (!CanCuff(target, cuffable, args.User))
            return;

        _audio.PlayPredicted(ent.Comp.EndSound, ent, args.User);

        // The fresh pair only exists on the server, the cuffs are networked from there.
        if (_net.IsClient)
            return;

        var cuffs = Spawn(ent.Comp.Cuffs, Transform(target).Coordinates);
        if (!_cuffable.TryAddNewCuffs(target, args.User, cuffs, cuffable))
        {
            QueueDel(cuffs);
            return;
        }

        _popup.PopupEntity(Loc.GetString("handcuff-component-cuff-other-success-message", ("otherName", target)),
            args.User, args.User);
    }

    private bool CanCuff(EntityUid target, CuffableComponent cuffable, EntityUid user)
    {
        if (TryComp<HandsComponent>(target, out var hands) && cuffable.CuffedHandCount < hands.Count)
            return true;

        _popup.PopupClient(Loc.GetString(hands == null
                ? "handcuff-component-target-has-no-hands-error"
                : "handcuff-component-target-has-no-free-hands-error",
            ("targetName", Identity.Name(target, EntityManager, user))), user, user);
        return false;
    }
}

[Serializable, NetSerializable]
public sealed partial class CyborgCuffDoAfterEvent : SimpleDoAfterEvent;
