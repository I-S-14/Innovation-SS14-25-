// SPDX-License-Identifier: MIT
using Content.Goobstation.Maths.FixedPoint;

namespace Content.Shared.Silicons.Bots;

public sealed partial class MedibotTreatment
{
    // Retained Goob treatment bounds; null means no bound. Source: 5327c414c5.
    [DataField]
    public FixedPoint2? MinDamage;

    [DataField]
    public FixedPoint2? MaxDamage;

    public bool IsValid(FixedPoint2 damage)
    {
        return (MaxDamage == null || damage < MaxDamage) && (MinDamage == null || damage > MinDamage);
    }
}
