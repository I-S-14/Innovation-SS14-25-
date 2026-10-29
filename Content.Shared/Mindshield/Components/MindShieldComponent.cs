// SPDX-License-Identifier: AGPL-3.0-or-later
using Robust.Shared.GameStates;

namespace Content.Shared.Mindshield.Components;

/// <summary>
/// This component, on a clothing item, on an implant or on an entity, prevents "mind control". This means that you won't be convertable to the revolution, for instance.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState, Access(typeof(SharedRevolutionarySystem))] //Goobstation - AutoGenerateComponentState
public sealed partial class MindShieldComponent : Component
{
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<SecurityIconPrototype> MindShieldStatusIcon = "MindShieldIcon";

    // Goobstation - revs update
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public ProtoId<SecurityIconPrototype> MindShieldBrokenStatusIcon = "MindShieldBrokenIcon";

    // Goobstation - uses to show headrevs with disabled ability
    [DataField, AutoNetworkedField]
    public bool Broken = false;
}
