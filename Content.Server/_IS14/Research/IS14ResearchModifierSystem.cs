// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Station.Systems;
using Content.Shared._IS14.Research;
using Content.Shared.Lathe;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.Research;

/// <summary>
/// Station-wide numbers that research moves. This is what makes a technology worth buying when
/// it unlocks no recipe at all: faster printing, cheaper printing, better instrument yields.
/// </summary>
public sealed class IS14ResearchModifierSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly StationSystem _station = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14ResearchModifiersComponent, IS14ResearchModifiersChangedEvent>(OnModifiersChanged);
        // ComponentStartup rather than MapInit: upstream's LatheSystem already owns that one,
        // and the engine allows a single subscription per component and event.
        SubscribeLocalEvent<LatheComponent, ComponentStartup>(OnLatheStartup);
    }

    /// <summary>
    /// Current value of a modifier: the prototype's default plus everything researched, clamped.
    /// </summary>
    public float GetModifier(EntityUid? station, ProtoId<IS14ResearchModifierPrototype> modifier)
    {
        if (!_proto.TryIndex(modifier, out IS14ResearchModifierPrototype? proto))
            return 1f;

        var value = proto.Default;

        if (station != null
            && TryComp<IS14ResearchModifiersComponent>(station, out var comp)
            && comp.Deltas.TryGetValue(modifier, out var delta))
        {
            value += delta;
        }

        return Math.Clamp(value, proto.Minimum, proto.Maximum);
    }

    /// <summary>
    /// Payout multiplier for one research data type: the global knob times the per-type one.
    /// Both are ordinary modifier prototypes, so a technology can improve either.
    /// </summary>
    public float GetPayoutMultiplier(EntityUid? station, ProtoId<ResearchPointTypePrototype> pointType)
    {
        var multiplier = GetModifier(station, "ResearchPayout");

        foreach (var proto in _proto.EnumeratePrototypes<IS14ResearchModifierPrototype>())
        {
            if (proto.PointType == pointType)
                multiplier *= GetModifier(station, proto.ID);
        }

        return multiplier;
    }

    public void AddModifier(EntityUid station, ProtoId<IS14ResearchModifierPrototype> modifier, float delta)
    {
        if (delta == 0f)
            return;

        var comp = EnsureComp<IS14ResearchModifiersComponent>(station);

        comp.Deltas.TryGetValue(modifier, out var current);
        comp.Deltas[modifier] = current + delta;

        var ev = new IS14ResearchModifiersChangedEvent(station);
        RaiseLocalEvent(station, ref ev);
    }

    private void OnModifiersChanged(Entity<IS14ResearchModifiersComponent> ent, ref IS14ResearchModifiersChangedEvent args)
    {
        RetuneLathes(ent.Owner);
    }

    private void OnLatheStartup(Entity<LatheComponent> ent, ref ComponentStartup args)
    {
        // A lathe built after the research happened still has to benefit from it.
        Retune(ent.Owner, ent.Comp);
    }

    /// <summary>Applies the station's current tuning to every lathe on it.</summary>
    private void RetuneLathes(EntityUid station)
    {
        var query = EntityQueryEnumerator<LatheComponent>();
        while (query.MoveNext(out var uid, out var lathe))
        {
            if (_station.GetOwningStation(uid) != station)
                continue;

            Retune(uid, lathe);
        }
    }

    private void Retune(EntityUid uid, LatheComponent lathe)
    {
        var station = _station.GetOwningStation(uid);

        // Remember what the prototype shipped with, so repeated retuning does not compound.
        var tuning = EnsureComp<IS14LatheTuningComponent>(uid);

        if (!tuning.Captured)
        {
            tuning.BaseTimeMultiplier = lathe.TimeMultiplier;
            tuning.BaseMaterialMultiplier = lathe.MaterialUseMultiplier;
            tuning.Captured = true;
        }

        var speed = GetModifier(station, "LatheSpeed");
        var material = GetModifier(station, "LatheMaterialEfficiency");

        // On top of the station-wide tuning, a modifier can be scoped to one workshop — the
        // medical fab, the ammo bench — so research can favour a department instead of
        // everything at once.
        var prototypeId = MetaData(uid).EntityPrototype?.ID;

        foreach (var proto in _proto.EnumeratePrototypes<IS14ResearchModifierPrototype>())
        {
            if (proto.Knob is not { } knob
                || proto.Lathes.Count == 0
                || !IS14PrototypeKin.MatchesAny(_proto, prototypeId, proto.Lathes))
            {
                continue;
            }

            var scoped = GetModifier(station, proto.ID);

            if (knob == IS14LatheKnob.Speed)
                speed *= scoped;
            else
                material *= scoped;
        }

        lathe.TimeMultiplier = tuning.BaseTimeMultiplier * speed;
        lathe.MaterialUseMultiplier = tuning.BaseMaterialMultiplier * material;
        Dirty(uid, lathe);
    }
}

/// <summary>Remembers a lathe's prototype values so research tuning stays reversible.</summary>
[RegisterComponent]
public sealed partial class IS14LatheTuningComponent : Component
{
    [DataField]
    public bool Captured;

    [DataField]
    public float BaseTimeMultiplier = 1f;

    [DataField]
    public float BaseMaterialMultiplier = 1f;
}
