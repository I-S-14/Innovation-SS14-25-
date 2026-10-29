// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Mindshield;
using Content.Shared.Overlays;
using Content.Shared.StatusIcon.Components;
using Content.Shared.Mindshield.Components;
using Robust.Shared.Prototypes;

namespace Content.Client.Overlays;

public sealed partial class ShowMindShieldIconsSystem : EquipmentHudSystem<ShowMindShieldIconsComponent>
{
    [Dependency] private MindShieldSystem _mindShield = default!;

    [SubscribeLocalEvent]
    private void OnGetStatusIconsEvent(Entity<StatusIconComponent> ent, ref GetStatusIconsEvent args)
    {
        // Is active checks for our ability to display status icons
        if (!IsActive)
            return;

        var statusIcon = component.MindShieldStatusIcon; // Goobstation - check if mindshield is broken

        if (component.Broken)
            statusIcon = component.MindShieldBrokenStatusIcon; // Goobstation - check if mindshield is broken

        if (_prototype.Resolve(statusIcon, out var iconPrototype)) // Goobstation
            ev.StatusIcons.Add(iconPrototype);
    }
}
