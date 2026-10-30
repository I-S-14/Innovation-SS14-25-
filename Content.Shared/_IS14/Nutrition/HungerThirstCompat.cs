// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Nutrition.Components;

namespace Content.Shared.Nutrition.EntitySystems;

/// <summary>
///     Wizden folded hunger and thirst into <see cref="SatiationSystem"/>. Goob/IS14 code was written against the old
///     HungerSystem/ThirstSystem; these thin facades keep that code working on top of satiation.
/// </summary>
public sealed class HungerSystem : EntitySystem
{
    [Dependency] private SatiationSystem _satiation = default!;

    // Old HungerComponent defaults; satiation types define their own bounds, these are only used for threshold checks.
    private static readonly (HungerThreshold Threshold, float Value)[] Thresholds =
    [
        (HungerThreshold.Overfed, 200f),
        (HungerThreshold.Okay, 150f),
        (HungerThreshold.Peckish, 100f),
        (HungerThreshold.Starving, 50f),
        (HungerThreshold.Dead, 0f),
    ];

    public float GetHunger(EntityUid uid, SatiationComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return 0f;

        return _satiation.GetValueOrNull((uid, comp), SatiationSystem.Hunger) ?? 0f;
    }

    public float GetHunger(Entity<SatiationComponent> ent)
    {
        return _satiation.GetValueOrNull(ent, SatiationSystem.Hunger) ?? 0f;
    }

    public void ModifyHunger(EntityUid uid, float amount, SatiationComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;

        _satiation.ModifyValue((uid, comp), SatiationSystem.Hunger, amount);
    }

    public void SetHunger(EntityUid uid, float amount, SatiationComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;

        _satiation.SetValue((uid, comp), SatiationSystem.Hunger, amount);
    }

    public HungerThreshold GetHungerThreshold(EntityUid uid, float? food = null, SatiationComponent? comp = null)
    {
        var value = food ?? GetHunger(uid, comp);
        foreach (var (threshold, min) in Thresholds)
        {
            if (value > min)
                return threshold;
        }

        return HungerThreshold.Dead;
    }
}

/// <inheritdoc cref="HungerSystem"/>
public sealed class ThirstSystem : EntitySystem
{
    [Dependency] private SatiationSystem _satiation = default!;

    public float GetThirst(EntityUid uid, SatiationComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return 0f;

        return _satiation.GetValueOrNull((uid, comp), SatiationSystem.Thirst) ?? 0f;
    }

    public void ModifyThirst(EntityUid uid, SatiationComponent? comp, float amount)
    {
        if (!Resolve(uid, ref comp, false))
            return;

        _satiation.ModifyValue((uid, comp), SatiationSystem.Thirst, amount);
    }

    public void SetThirst(EntityUid uid, SatiationComponent? comp, float amount)
    {
        if (!Resolve(uid, ref comp, false))
            return;

        _satiation.SetValue((uid, comp), SatiationSystem.Thirst, amount);
    }
}

/// <summary>
///     Old hunger buckets, kept for Goob code that compares against them.
/// </summary>
public enum HungerThreshold : byte
{
    Overfed = 1 << 3,
    Okay = 1 << 2,
    Peckish = 1 << 1,
    Starving = 1 << 0,
    Dead = 0,
}
