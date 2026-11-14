using System.Linq;
using Content.Shared.Body.Prototypes;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Verbs;
using Content.Shared.Weapons.Melee.Events;

namespace Content.Shared.Weapons.Melee.Metabolizer;

public sealed partial class BonusDamageOnMetabolismSystem : EntitySystem
{

    [SubscribeLocalEvent]
    private void OnGetVerb(Entity<BonusDamageOnMetabolismComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var allMetabolizers = ProtoMan.EnumeratePrototypes<MetabolizerTypePrototype>().OrderBy(x => x.LocalizedName);

        byte index = 0;
        foreach (var metabolizer in allMetabolizers)
        {
            if (ent.Comp.ExcludedMetabolizers.Contains(metabolizer))
                continue;

            var currIndex = index;
            var verb = new Verb
            {
                Priority = currIndex,
                Category = new VerbCategory("verb-categories-select-target", "/Textures/Interface/VerbIcons/group.svg.192dpi.png"),
                Disabled = ent.Comp.SelectedMetabolizer == metabolizer,
                Act = () =>
                {
                    ent.Comp.SelectedMetabolizer = metabolizer;
                    Dirty(ent);
                },
                Text = metabolizer.LocalizedName,
            };
            args.Verbs.Add(verb);
            index++;
        }
    }

}
