// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chemistry.Reagent;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Medical.BloodType;

/// <summary>
/// The group a drop of blood belongs to, carried on the reagent itself.
/// </summary>
/// <remarks>
/// This rides <em>alongside</em> <see cref="DnaData"/> rather than replacing it. Forensics
/// looks for DnaData and does not care what else is in the list, so the detective's half of
/// blood keeps working untouched — and picks up a second, coarser signal for free, since a
/// puddle now also says which group it came from.
/// </remarks>
[ImplicitDataDefinitionForInheritors, Serializable, NetSerializable]
public sealed partial class BloodTypeData : ReagentData
{
    [DataField]
    public ProtoId<BloodTypePrototype> Type;

    public BloodTypeData()
    {
    }

    public BloodTypeData(ProtoId<BloodTypePrototype> type)
    {
        Type = type;
    }

    public override ReagentData Clone()
    {
        return new BloodTypeData(Type);
    }

    public override bool Equals(ReagentData? other)
    {
        return other is BloodTypeData data && data.Type == Type;
    }

    public override int GetHashCode()
    {
        return Type.Id.GetHashCode();
    }

    public override string ToString(string prototype)
    {
        return $"{prototype}:{Type.Id}";
    }
}
