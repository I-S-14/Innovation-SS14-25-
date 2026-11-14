// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Mindshield;
using Content.Shared.Overlays;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Mindshield.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Overlays;

public sealed partial class ShowMindShieldIconsSystem : EquipmentHudSystem<ShowMindShieldIconsComponent>
{
    //IS14-change start: retain fork fields lost during merge.
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    //IS14-change end

    [Dependency] private MindShieldSystem _mindShield = default!;

    [SubscribeLocalEvent]
    private void OnGetStatusIconsEvent(Entity<StatusIconComponent> ent, ref GetStatusIconsEvent args)
    {
        // Is active checks for our ability to display status icons
        if (!IsActive)
            return;

        _mindShield.GetMindshieldStatus(ent.Owner, out var shielded, out var visible);
        if (!visible)
            return;

        ProtoId<Content.Shared.StatusIcon.StatusIconPrototype> icon = shielded ? "MindShieldIcon" : "MindShieldBrokenIcon";
        if (TryComp<MindShieldComponent>(ent, out var mindshield))
            icon = mindshield.Broken ? mindshield.MindShieldBrokenStatusIcon : mindshield.MindShieldStatusIcon;

        if (_prototype.Resolve(icon, out var prototype))
            args.StatusIcons.Add(prototype);
    }
}
