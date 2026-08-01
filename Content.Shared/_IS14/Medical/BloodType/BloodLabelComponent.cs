// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Medical.BloodType;

/// <summary>
/// A group written on the outside of a container.
/// </summary>
/// <remarks>
/// Deliberately a separate fact from what is actually inside. A label is written once, when
/// the bag is filled or tested, and stays written — so a bag somebody has since topped up
/// from a second donor lies about itself, exactly like a real one would. Reading the
/// contents costs a test; reading the label is free and only as good as whoever wrote it.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BloodLabelComponent : Component
{
    [DataField, AutoNetworkedField]
    public ProtoId<BloodTypePrototype>? Type;
}
