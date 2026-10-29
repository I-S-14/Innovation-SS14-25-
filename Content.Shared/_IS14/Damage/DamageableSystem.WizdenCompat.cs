// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Damage.Components;
using Content.Shared.Damage.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Shared.Damage.Systems;

/// <summary>
///     We keep the Goob/Woundmed damage pipeline, but code merged from space-wizards calls their newer
///     DamageableSystem API. These wrappers map it onto our component so that code works unchanged.
/// </summary>
public sealed partial class DamageableSystem
{
    /// <summary>
    ///     Gets the damages currently sustained by an entity.
    /// </summary>
    public DamageSpecifier GetAllDamage(Entity<DamageableComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return new();

        return new DamageSpecifier(ent.Comp.Damage);
    }

    /// <summary>
    ///     Gets the total amount of damage currently sustained by an entity.
    /// </summary>
    public FixedPoint2 GetTotalDamage(Entity<DamageableComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return FixedPoint2.Zero;

        return ent.Comp.TotalDamage;
    }

    /// <summary>
    ///     Gets the total amount of damage currently sustained by an entity, indexed by damage group.
    /// </summary>
    public IReadOnlyDictionary<ProtoId<DamageGroupPrototype>, FixedPoint2> GetDamagePerGroup(Entity<DamageableComponent?> ent)
    {
        var result = new Dictionary<ProtoId<DamageGroupPrototype>, FixedPoint2>();
        if (!Resolve(ent, ref ent.Comp, false))
            return result;

        foreach (var (group, value) in ent.Comp.DamagePerGroup)
        {
            result[group] = value;
        }

        return result;
    }

    /// <summary>
    ///     Damage types with a non-zero value, limited to the given groups.
    /// </summary>
    public Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2> GetDamages(
        Dictionary<ProtoId<DamageGroupPrototype>, FixedPoint2> damagePerGroup,
        DamageSpecifier damage)
    {
        var damageTypes = new Dictionary<ProtoId<DamageTypePrototype>, FixedPoint2>();

        foreach (var (groupId, _) in damagePerGroup)
        {
            var group = _prototypeManager.Index(groupId);
            foreach (var type in group.DamageTypes)
            {
                if (!damage.DamageDict.TryGetValue(type, out var value) || value == 0)
                    continue;

                damageTypes[type] = value;
            }
        }

        return damageTypes;
    }
}
