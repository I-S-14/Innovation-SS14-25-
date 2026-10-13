// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Buckle.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Shared._IS14.Cyborgs.Systems;

public sealed class CyborgBedDeployerSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgBedDeployerComponent, AfterInteractEvent>(OnAfterInteract);
    }

    private void OnAfterInteract(Entity<CyborgBedDeployerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        // A bed deleted or destroyed elsewhere frees the deployer.
        if (ent.Comp.Deployed is { } old && !Exists(old))
        {
            ent.Comp.Deployed = null;
            Dirty(ent);
        }

        // Clicking our own bed packs it back.
        if (args.Target is { } target && target == ent.Comp.Deployed)
        {
            args.Handled = true;

            if (TryComp<StrapComponent>(target, out var strap) && strap.BuckledEntities.Count > 0)
            {
                _popup.PopupClient(Loc.GetString("cyborg-bed-occupied"), target, args.User);
                return;
            }

            _popup.PopupClient(Loc.GetString("cyborg-bed-packed"), target, args.User);
            ent.Comp.Deployed = null;
            Dirty(ent);
            PredictedQueueDel(target);
            return;
        }

        // Deploy onto the floor only.
        if (args.Target != null)
            return;

        args.Handled = true;

        if (ent.Comp.Deployed != null)
        {
            _popup.PopupClient(Loc.GetString("cyborg-bed-already-deployed"), ent, args.User);
            return;
        }

        // Spawning is server only, the bed is networked to everyone.
        if (_net.IsClient)
            return;

        ent.Comp.Deployed = Spawn(ent.Comp.Bed, args.ClickLocation);
        Dirty(ent);
    }
}
