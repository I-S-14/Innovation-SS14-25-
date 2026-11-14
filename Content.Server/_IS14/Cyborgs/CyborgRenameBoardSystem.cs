// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Server.Administration;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Interaction.Events;
using Content.Shared.Popups;
using Robust.Shared.Player;

namespace Content.Server._IS14.Cyborgs;

/// <summary>
/// Writing a name onto the cyborg rename board, tg's attack_self prompt.
/// </summary>
public sealed class CyborgRenameBoardSystem : EntitySystem
{
    [Dependency] private readonly QuickDialogSystem _quickDialog = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgRenameBoardComponent, UseInHandEvent>(OnUseInHand);
    }

    private void OnUseInHand(Entity<CyborgRenameBoardComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled || !TryComp<ActorComponent>(args.User, out var actor))
            return;

        args.Handled = true;

        var user = args.User;
        _quickDialog.OpenDialog<string>(actor.PlayerSession,
            Loc.GetString("cyborg-rename-board-title"),
            Loc.GetString("cyborg-rename-board-prompt"),
            name =>
            {
                if (TerminatingOrDeleted(ent))
                    return;

                name = name.Trim();
                if (name.Length > ent.Comp.MaxLength)
                    name = name[..ent.Comp.MaxLength];

                ent.Comp.NewName = string.IsNullOrWhiteSpace(name) ? null : name;
                Dirty(ent);

                if (ent.Comp.NewName != null)
                    _popup.PopupEntity(Loc.GetString("cyborg-rename-board-set", ("name", ent.Comp.NewName)), ent, user);
            });
    }
}
