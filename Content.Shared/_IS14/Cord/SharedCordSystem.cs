// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._IS14.Cord;

/// <summary>
/// Attaching and detaching cords. Deliberately tiny — a cord has no behaviour of its
/// own, it is a thing other systems hang between two entities and then forget about.
/// </summary>
public sealed class SharedCordSystem : EntitySystem
{
    /// <summary>Hangs the cord between its owner and <paramref name="anchor"/>.</summary>
    public void Attach(Entity<CordComponent?> ent, EntityUid anchor, float? slackLength = null)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        ent.Comp.Anchor = anchor;

        if (slackLength is { } length)
            ent.Comp.SlackLength = length;

        Dirty(ent);
    }

    /// <summary>Coils the cord up. Nothing is drawn until it is attached again.</summary>
    public void Detach(Entity<CordComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false) || ent.Comp.Anchor == null)
            return;

        ent.Comp.Anchor = null;
        ent.Comp.Energized = false;
        Dirty(ent);
    }

    /// <summary>
    /// Dyes the cord. For cords whose contents are visible through them — a transfusion
    /// line, a fuel hose — so the colour is a readout rather than decoration.
    /// </summary>
    public void SetColor(Entity<CordComponent?> ent, Color color)
    {
        if (!Resolve(ent, ref ent.Comp, false) || ent.Comp.Color == color)
            return;

        ent.Comp.Color = color;
        Dirty(ent);
    }

    /// <summary>Flags the cord as carrying something, for effects that care.</summary>
    public void SetEnergized(Entity<CordComponent?> ent, bool energized)
    {
        if (!Resolve(ent, ref ent.Comp, false) || ent.Comp.Energized == energized)
            return;

        ent.Comp.Energized = energized;
        Dirty(ent);
    }
}
