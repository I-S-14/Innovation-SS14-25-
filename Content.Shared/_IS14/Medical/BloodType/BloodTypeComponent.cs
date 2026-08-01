// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Medical.BloodType;

/// <summary>
/// A mob's blood group, and what its immune system has learned the hard way.
/// </summary>
/// <remarks>
/// Entirely optional. Without it a mob's group is derived from its DNA, which keeps the
/// group stable for the round, survives cloning, and costs no prototype edits anywhere.
/// The component exists for the cases where a specific answer is wanted — a scripted NPC,
/// an admin spawn, or an antagonist whose blood is supposed to be strange — and for
/// remembering sensitisation, which nothing can derive.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class BloodTypeComponent : Component
{
    /// <summary>
    /// The group, overriding whatever the DNA would have rolled. Null asks for the roll.
    /// </summary>
    [DataField("bloodType"), AutoNetworkedField]
    public ProtoId<BloodTypePrototype>? Type;

    /// <summary>
    /// Antigens this body has met before and now makes antibodies against.
    /// </summary>
    /// <remarks>
    /// Only matters for antigens that are not <see cref="BloodAntigenPrototype.Preformed"/>:
    /// the first Rh-positive unit into an Rh-negative patient goes in quietly and lands here,
    /// and the second one is rejected. The patient has no way to know, which is the point —
    /// somebody skipped the test an hour ago and the bill arrives now.
    /// </remarks>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<BloodAntigenPrototype>> Sensitized = new();
}
