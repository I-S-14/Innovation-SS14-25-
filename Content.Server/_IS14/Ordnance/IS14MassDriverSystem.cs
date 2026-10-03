// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Content.Server.Power.EntitySystems;
using Content.Shared._IS14.Ordnance;
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Throwing;
using Robust.Server.GameObjects;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Ordnance;

/// <summary>
/// Fires the mass driver: everything loose standing on the plate is thrown the way the plate faces.
/// </summary>
public sealed class IS14MassDriverSystem : EntitySystem
{
    [Dependency] private readonly ThrowingSystem _throwing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14MassDriverComponent, SignalReceivedEvent>(OnSignal);
    }

    private void OnSignal(Entity<IS14MassDriverComponent> ent, ref SignalReceivedEvent args)
    {
        if (args.Port == ent.Comp.TriggerPort)
            Launch(ent);
    }

    /// <summary>
    /// Throws the payload. Returns how many things went flying, so the console can report an
    /// empty plate instead of pretending it launched something.
    /// </summary>
    public int Launch(Entity<IS14MassDriverComponent> ent)
    {
        if (!this.IsPowered(ent.Owner, EntityManager) || _timing.CurTime < ent.Comp.NextReady)
            return 0;

        ent.Comp.NextReady = _timing.CurTime + ent.Comp.Cooldown;

        var direction = _xform.GetWorldRotation(ent.Owner).ToWorldVec();
        var launched = 0;

        foreach (var payload in GetPayload(ent.Owner))
        {
            if (launched >= ent.Comp.MaxItems)
                break;

            _throwing.TryThrow(payload, direction * ent.Comp.Range, ent.Comp.Speed, ent.Owner, 0f);
            launched++;
        }

        _audio.PlayPvs(ent.Comp.LaunchSound, ent.Owner);
        _appearance.SetData(ent.Owner, IS14MassDriverVisuals.Firing, true);

        return launched;
    }

    /// <summary>Loose entities sharing the plate's tile. Anchored things stay where they are.</summary>
    private List<EntityUid> GetPayload(EntityUid driver)
    {
        var payload = new List<EntityUid>();
        // Half a tile around the plate's centre: what is standing on it, and nothing from next door.
        var found = _lookup.GetEntitiesInRange(_xform.GetMapCoordinates(driver), 0.45f);

        foreach (var uid in found)
        {
            if (uid == driver || Transform(uid).Anchored)
                continue;

            // Anything with a loose body flies — items, crates and, yes, people.
            if (!TryComp<PhysicsComponent>(uid, out var physics) || physics.BodyType == BodyType.Static)
                continue;

            payload.Add(uid);
        }

        return payload;
    }
}
