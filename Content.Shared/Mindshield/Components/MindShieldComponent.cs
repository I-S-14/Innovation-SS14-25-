// SPDX-License-Identifier: AGPL-3.0-or-later
using Robust.Shared.GameStates;
using Content.Shared.Revolutionary;
using Content.Shared.StatusIcon;
using Robust.Shared.Prototypes;

namespace Content.Shared.Mindshield.Components;

/// <summary>
/// This component, on a clothing item, on an implant or on an entity, prevents "mind control". This means that you won't be convertable to the revolution, for instance.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(Content.Shared.Revolutionary.RevolutionarySystem))] //Goobstation - AutoGenerateComponentState
public sealed partial class MindShieldComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<StatusIconPrototype> MindShieldStatusIcon = "MindShieldIcon";

    // Goobstation - revs update
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<StatusIconPrototype> MindShieldBrokenStatusIcon = "MindShieldBrokenIcon";

    // Goobstation - uses to show headrevs with disabled ability
    [DataField, AutoNetworkedField]
    public bool Broken = false;
}
