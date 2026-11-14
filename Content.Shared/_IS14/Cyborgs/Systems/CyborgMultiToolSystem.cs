// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Network;

namespace Content.Shared._IS14.Cyborgs.Systems;

public sealed class CyborgMultiToolSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgMultiToolComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CyborgMultiToolComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<CyborgMultiToolComponent, ExaminedEvent>(OnExamined);
    }

    private void OnMapInit(Entity<CyborgMultiToolComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Modes.Count == 0)
            return;

        ent.Comp.Current = Math.Clamp(ent.Comp.Current, 0, ent.Comp.Modes.Count - 1);
        EntityManager.AddComponents(ent.Owner, ent.Comp.Modes[ent.Comp.Current].Components);
        Dirty(ent);
    }

    private void OnUseInHand(Entity<CyborgMultiToolComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || ent.Comp.Modes.Count < 2)
            return;

        args.Handled = true;

        // Adding and removing components is server only, the client follows the networked mode.
        if (_net.IsClient)
            return;

        EntityManager.RemoveComponents(ent.Owner, ent.Comp.Modes[ent.Comp.Current].Components);
        ent.Comp.Current = (ent.Comp.Current + 1) % ent.Comp.Modes.Count;
        var mode = ent.Comp.Modes[ent.Comp.Current];
        EntityManager.AddComponents(ent.Owner, mode.Components);
        Dirty(ent);

        _popup.PopupEntity(Loc.GetString("cyborg-multitool-mode", ("mode", Loc.GetString(mode.Name))), args.User,
            args.User);
    }

    private void OnExamined(Entity<CyborgMultiToolComponent> ent, ref ExaminedEvent args)
    {
        if (ent.Comp.Current < ent.Comp.Modes.Count)
            args.PushMarkup(Loc.GetString("cyborg-multitool-mode",
                ("mode", Loc.GetString(ent.Comp.Modes[ent.Comp.Current].Name))));
    }
}
