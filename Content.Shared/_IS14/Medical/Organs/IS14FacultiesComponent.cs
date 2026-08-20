// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Medical.Organs;

/// <summary>
/// What this body is currently able to do, as a level from 0 to 1 per faculty.
/// </summary>
/// <remarks>
/// Recomputed when an organ changes and at no other time. Every organic humanoid on the
/// station carries this and almost all of them are intact, so the common case has to be free.
/// <para>
/// A faculty appears here the first time the body has an organ feeding it and then stays,
/// dropping to zero rather than vanishing. That is the difference between "his heart is gone"
/// and "his species never had one" — without it, losing an organ would read as never having
/// needed it.
/// </para>
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IS14FacultiesComponent : Component
{
    [ViewVariables, AutoNetworkedField]
    public Dictionary<string, float> Levels = new();

    /// <summary>
    /// Whether any of this body's organs currently has oxygen damage to work off.
    /// </summary>
    /// <remarks>
    /// Purely so the perfusion loop can skip a healthy body without walking its organs. Almost
    /// everybody on the station is fine almost all of the time, and the cost of the feature has
    /// to be paid by the patients rather than by the crew.
    /// </remarks>
    [ViewVariables]
    public bool HypoxicDebt;
}
