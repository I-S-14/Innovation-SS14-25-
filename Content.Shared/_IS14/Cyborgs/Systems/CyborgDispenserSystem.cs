// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Placeable;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Timing;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Cyborgs.Systems;

public sealed class CyborgDispenserSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgDispenserComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<CyborgDispenserComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<CyborgDispenserComponent, CyborgDispenserSelectMessage>(OnSelectMessage);
    }

    private void OnUseInHand(Entity<CyborgDispenserComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Options.Count < 2)
            return;

        args.Handled = true;

        // A radial menu like the RCD's where the tool has one, cycling otherwise.
        if (_ui.HasUi(ent.Owner, CyborgDispenserUiKey.Key))
        {
            _ui.TryToggleUi(ent.Owner, CyborgDispenserUiKey.Key, args.User);
            return;
        }

        Select(ent, (ent.Comp.Selected + 1) % ent.Comp.Options.Count, args.User);
    }

    private void OnSelectMessage(Entity<CyborgDispenserComponent> ent, ref CyborgDispenserSelectMessage args)
    {
        if (args.Index < 0 || args.Index >= ent.Comp.Options.Count)
            return;

        Select(ent, args.Index, args.Actor);
    }

    private void Select(Entity<CyborgDispenserComponent> ent, int index, EntityUid user)
    {
        ent.Comp.Selected = index;
        Dirty(ent);

        if (_proto.TryIndex(ent.Comp.Options[index], out var proto))
            _popup.PopupClient(Loc.GetString("cyborg-dispenser-selected", ("item", proto.Name)), ent, user);
    }

    private void OnAfterInteract(Entity<CyborgDispenserComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || ent.Comp.Options.Count == 0)
            return;

        // Print onto the floor or a table, not into someone's face.
        if (args.Target is { } target && !HasComp<PlaceableSurfaceComponent>(target))
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

        _audio.PlayPredicted(ent.Comp.DispenseSound, ent, args.User);

        if (_net.IsServer)
            Spawn(ent.Comp.Options[ent.Comp.Selected % ent.Comp.Options.Count], args.ClickLocation);
    }
}
