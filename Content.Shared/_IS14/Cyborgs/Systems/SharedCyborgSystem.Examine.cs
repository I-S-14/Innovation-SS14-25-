// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Linq;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Examine;

namespace Content.Shared._IS14.Cyborgs.Systems;

public abstract partial class SharedCyborgSystem
{
    private void InitializeExamine()
    {
        SubscribeLocalEvent<CyborgComponent, ExaminedEvent>(OnExamined);
    }

    // The model and the upgrade boards, readable from up close like tg's borg examine.
    private void OnExamined(Entity<CyborgComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        using (args.PushGroup(nameof(SharedCyborgSystem)))
        {
            args.PushMarkup(_proto.TryIndex(ent.Comp.Model, out var model)
                ? Loc.GetString("cyborg-examine-model", ("model", Loc.GetString(model.Name)))
                : Loc.GetString("cyborg-examine-no-model"));

            var boards = ent.Comp.UpgradeContainer.ContainedEntities.Select(b => Name(b)).ToList();
            if (boards.Count > 0)
                args.PushMarkup(Loc.GetString("cyborg-examine-upgrades", ("upgrades", string.Join(", ", boards))));

            if (ent.Comp.BrokenSlots > 0)
                args.PushMarkup(Loc.GetString("cyborg-examine-broken-slots", ("count", ent.Comp.BrokenSlots)));
        }
    }
}
