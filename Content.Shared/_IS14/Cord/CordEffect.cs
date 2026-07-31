// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._IS14.Cord;

/// <summary>
/// Colours a cord segment by segment. One class per thing a cord can be seen doing —
/// carrying current, leaking, glowing hot — named in YAML with <c>!type:</c>.
/// </summary>
/// <remarks>
/// Like <see cref="CordShape"/> these are shared between every cord using them and must
/// stay stateless. Working per segment rather than per cord is what lets an effect run
/// something along the cable instead of only flashing the whole thing at once.
/// </remarks>
[ImplicitDataDefinitionForInheritors]
public abstract partial class CordEffect
{
    /// <summary>Colour of one segment. Returning the base colour leaves it untouched.</summary>
    public abstract Color GetColor(in CordEffectArgs args);
}

/// <summary>Everything an effect is given to colour a segment with.</summary>
public readonly struct CordEffectArgs
{
    /// <summary>The cord's own colour, before this effect.</summary>
    public readonly Color Base;

    /// <summary>Which segment this is, counted from the anchored end.</summary>
    public readonly int Index;

    /// <summary>How many segments the cord was drawn with.</summary>
    public readonly int Count;

    /// <summary>Seconds, for effects that move.</summary>
    public readonly float Time;

    /// <summary>Whether the cord's owner has flagged it as doing something right now.</summary>
    public readonly bool Energized;

    public CordEffectArgs(Color baseColor, int index, int count, float time, bool energized)
    {
        Base = baseColor;
        Index = index;
        Count = count;
        Time = time;
        Energized = energized;
    }
}
