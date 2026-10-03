// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Examine;
using Robust.Shared.Map;

namespace Content.Client._IS14.Research;

/// <summary>
/// Pops a floating arrow in front of a directional machine when it is examined, so its facing is
/// readable without counting pixels.
/// </summary>
/// <remarks>
/// Same trick the TEG circulators use for their in- and outlet ports: a short-lived effect
/// parented to the machine, which inherits its rotation and therefore always points where the
/// machine looks. Generic on purpose — any machine that cares about its facing can take the
/// component.
/// </remarks>
public sealed class IS14FacingArrowSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14FacingArrowComponent, ClientExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<IS14FacingArrowComponent> ent, ref ClientExaminedEvent args)
    {
        Spawn(ent.Comp.Arrow, new EntityCoordinates(ent.Owner, 0, 0));
    }
}
