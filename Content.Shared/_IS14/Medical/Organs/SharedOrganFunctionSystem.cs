// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Body.Organ;

namespace Content.Shared._IS14.Medical.Organs;

/// <summary>
/// Turns organ integrity into function, and answers what a body can currently do.
/// </summary>
/// <remarks>
/// Shared rather than server-side because the analyser draws organ cards from the client's own
/// copy of the organs, and a readout that disagreed with the server about what "62%" means
/// would be worse than no readout. The arithmetic lives here once; the server owns the
/// recomputation of body-wide levels and networks the result.
/// </remarks>
public abstract class SharedOrganFunctionSystem : EntitySystem
{
    /// <summary>
    /// How well one organ is working, 0 to 1.
    /// </summary>
    /// <remarks>
    /// Flat at one down to the reserve, then straight to nothing at the floor, then scaled by
    /// whatever oxygen starvation has taken. A disabled organ is doing nothing whatever its
    /// integrity says — that is what disabled means.
    /// </remarks>
    public float GetEfficiency(IS14OrganFunctionComponent function, OrganComponent organ)
    {
        if (!organ.Enabled || organ.IntegrityCap <= 0)
            return 0f;

        var share = (organ.OrganIntegrity / organ.IntegrityCap).Float();
        var span = function.Reserve - function.Floor;

        var intact = span <= 0f
            ? share > function.Floor ? 1f : 0f
            : Math.Clamp((share - function.Floor) / span, 0f, 1f);

        return intact * (1f - Math.Clamp(function.HypoxicInjury, 0f, 1f));
    }

    /// <summary>How much of a faculty a body has, where 1 is a healthy person.</summary>
    /// <remarks>
    /// A faculty this body has no organ for at all reads as whole, not as missing. Species
    /// without a stomach are not starving, and a consumer asking about something that does not
    /// apply should get "nothing wrong here" rather than "everything is wrong".
    /// </remarks>
    public float GetLevel(EntityUid body, string faculty)
    {
        return TryComp<IS14FacultiesComponent>(body, out var comp) && comp.Levels.TryGetValue(faculty, out var level)
            ? level
            : 1f;
    }
}

/// <summary>Named faculties the code itself refers to. Everything else is data.</summary>
public static class IS14Faculties
{
    public const string Pump = "Pump";
    public const string Respiration = "Respiration";
    public const string Cognition = "Cognition";
    public const string Vision = "Vision";
    public const string Hearing = "Hearing";
    public const string Speech = "Speech";
    public const string Filtration = "Filtration";
    public const string Detox = "Detox";
    public const string Digestion = "Digestion";
}
