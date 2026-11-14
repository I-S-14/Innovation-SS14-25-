// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Item.ItemToggle;
using Content.Shared.Item.ItemToggle.Components;
using Content.Shared.Verbs;

namespace Content.Shared._IS14.Cyborgs.Systems;

public sealed class CyborgAltToggleSystem : EntitySystem
{
    [Dependency] private readonly ItemToggleSystem _toggle = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgAltToggleComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
    }

    private void OnGetVerbs(Entity<CyborgAltToggleComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !TryComp<ItemToggleComponent>(ent, out var toggle))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(toggle.Activated ? toggle.VerbToggleOff : toggle.VerbToggleOn),
            Act = () => _toggle.Toggle((ent.Owner, toggle), user),
            Priority = 2,
        });
    }
}
