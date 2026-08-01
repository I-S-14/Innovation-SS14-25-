// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Medical.BloodType;

/// <summary>
/// Ships a container with its blood already typed.
/// </summary>
/// <remarks>
/// Groups live on the reagent, and YAML cannot write reagent data — so a bag that is
/// supposed to spawn full of a known group needs somebody to stamp it on map init. That is
/// all this is, and it is what makes pre-typed stock possible at all: synthetic universal
/// blood, a donor crate, a xenobiology sample of something that should never go in a person.
/// </remarks>
[RegisterComponent]
public sealed partial class PresetBloodTypeComponent : Component
{
    /// <summary>Named <c>bloodType</c> in YAML: <c>type</c> is already the component's own key.</summary>
    [DataField("bloodType", required: true)]
    public ProtoId<BloodTypePrototype> Type;

    /// <summary>Which solution on this entity holds the blood.</summary>
    [DataField]
    public string Solution = "beaker";

    /// <summary>Whether the container is also labelled with what is in it.</summary>
    [DataField]
    public bool Label = true;
}
