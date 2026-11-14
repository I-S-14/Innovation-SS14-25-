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
        if (!Resolve(uid, ref comp, false)
            || comp.GetOrNull(SatiationSystem.Hunger) is not { } satiation
            || !ProtoMan.TryIndex(satiation.Prototype, out var prototype))
            return HungerThreshold.Dead;

        var value = food ?? GetHunger(uid, comp);
        var result = HungerThreshold.Dead;
        var closest = int.MaxValue;
        foreach (var threshold in Enum.GetValues<HungerThreshold>())
        {
            if (prototype.Thresholds.TryGetValue(threshold.ToString(), out var limit) && limit >= value && limit < closest)
            {
                result = threshold;
                closest = limit;
            }
        }
        return result;
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

    public ThirstThreshold GetThirstThreshold(EntityUid uid, SatiationComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false)
            || comp.GetOrNull(SatiationSystem.Thirst) is not { } satiation
            || !ProtoMan.TryIndex(satiation.Prototype, out var prototype))
            return ThirstThreshold.Dead;

        var value = GetThirst(uid, comp);
        var result = ThirstThreshold.Dead;
        var closest = int.MaxValue;
        foreach (var threshold in Enum.GetValues<ThirstThreshold>())
        {
            // Wizards spells this key "Overhydrated", while the retained Goob enum uses "OverHydrated".
            if (!prototype.Thresholds.TryGetValue(threshold.ToString(), out var limit)
                && (threshold != ThirstThreshold.OverHydrated
                    || !prototype.Thresholds.TryGetValue("Overhydrated", out limit)))
                continue;

            if (limit >= value && limit < closest)
            {
                result = threshold;
                closest = limit;
            }
        }
        return result;
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

public enum ThirstThreshold : byte
{
    Dead = 0,
    Parched = 1 << 0,
    Thirsty = 1 << 1,
    Okay = 1 << 2,
    OverHydrated = 1 << 3,
}
