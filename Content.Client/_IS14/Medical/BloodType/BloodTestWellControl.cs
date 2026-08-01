// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Medical.BloodType;

/// <summary>
/// One well of a blood typing card: a drop of the sample meeting one antibody.
/// </summary>
/// <remarks>
/// Drawn rather than assembled from textures because the whole point is the moment in the
/// middle — the blood sitting there looking like nothing has happened, and then either
/// breaking into clumps or not. A sprite would have to pick one frame of that.
///
/// Soaking and reacting are separate on purpose. A card is printed with empty wells long
/// before anybody bleeds on it, the blood arrives all at once, and the answer takes another
/// ten seconds — three states that one progress value could not tell apart.
/// </remarks>
public sealed class BloodTestWellControl : Control
{
    /// <summary>Number of clumps drawn once a positive well has settled.</summary>
    private const int Clumps = 7;

    /// <summary>Which surface this well is drawn on. Everything else is the same either way.</summary>
    public BloodTestPalette Palette = BloodTestPalette.Screen;

    /// <summary>Whether this well is going to come out positive.</summary>
    public bool Positive;

    /// <summary>0 for a dry printed well, 1 for a pad soaked through with blood.</summary>
    public float Filled;

    /// <summary>0 the moment the blood lands, 1 once the reaction has settled.</summary>
    public float Reaction;

    protected override void Draw(DrawingHandleScreen handle)
    {
        base.Draw(handle);

        var center = new Vector2(PixelWidth, PixelHeight) / 2f;
        var radius = MathF.Min(PixelWidth, PixelHeight) / 2f - 2f;

        if (radius <= 0f)
            return;

        // The well itself is always there, wet or dry — it is printed on the card.
        handle.DrawCircle(center, radius, Palette.Plate);

        var filled = Math.Clamp(Filled, 0f, 1f);
        var reaction = Math.Clamp(Reaction, 0f, 1f);

        if (filled > 0f)
            DrawSample(handle, center, radius * 0.78f, filled, reaction);

        // The rim brightens while the well is working and dims once it has settled, so a card
        // halfway through a run reads at a glance as "still going".
        var working = filled > 0f && reaction < 1f;
        handle.DrawCircle(center, radius, working ? Palette.RimLit : Palette.Rim, false);
    }

    private void DrawSample(DrawingHandleScreen handle, Vector2 center, float radius, float filled, float reaction)
    {
        // Blood darkens a little as it stands, whether or not it is going to clump.
        var body = Color.InterpolateBetween(Palette.Blood, Palette.Clump, reaction * 0.35f);

        handle.DrawCircle(center, radius * filled, body);

        if (!Positive || reaction <= 0f)
            return;

        // Agglutination: the blood breaks up. Positions are a golden-angle spiral so they look
        // scattered without being random — a well redrawn every frame has to hold still.
        for (var i = 0; i < Clumps; i++)
        {
            var angle = i * 2.39996f;
            var distance = radius * 0.55f * MathF.Sqrt((i + 0.5f) / Clumps);
            var offset = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * distance;

            handle.DrawCircle(center + offset, radius * 0.24f * reaction, Palette.Clump);
        }
    }
}
