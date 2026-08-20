// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Medical.Organs;

/// <summary>
/// An organ that does something: what it contributes, how much damage it shrugs off, and how
/// badly it minds being starved of blood.
/// </summary>
/// <remarks>
/// Attached from <see cref="IS14OrganFunctionPrototype"/> by slot id rather than written into
/// every species' organ prototypes — there are a dozen species inheriting from the human
/// organs and none of them should have to be edited to gain a heart that pumps. An organ
/// prototype that declares this component itself wins, which is how a cybernetic or an alien
/// organ says it works differently.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IS14OrganFunctionComponent : Component
{
    /// <summary>Which faculties this organ feeds, and by how much at full efficiency.</summary>
    [DataField, AutoNetworkedField]
    public Dictionary<string, float> Contributions = new();

    /// <summary>
    /// Share of integrity above which the organ works perfectly.
    /// </summary>
    /// <remarks>
    /// Functional reserve, and the single most important number in the system. Real organs do
    /// not lose function in proportion to damage — you can give away half a liver and notice
    /// nothing — and without that, every graze anywhere would raise the whole crew's pulse and
    /// hand them heart disease for being alive. Between "scratched" and "failing" there has to
    /// be a wide band where the scanner shows a problem and the patient feels none.
    /// </remarks>
    [DataField]
    public float Reserve = 0.6f;

    /// <summary>Share of integrity below which the organ does nothing at all.</summary>
    [DataField]
    public float Floor = 0.05f;

    /// <summary>
    /// How badly this organ minds an oxygen shortfall, relative to the brain.
    /// </summary>
    /// <remarks>
    /// Zero means it does not care, which is the default: an organ opts into being harmed by
    /// poor circulation rather than out of it.
    /// </remarks>
    [DataField]
    public float PerfusionSensitivity;

    /// <summary>
    /// How much of this organ's function is currently lost to oxygen starvation, 0 to 1.
    /// </summary>
    /// <remarks>
    /// Deliberately *not* expressed as organ integrity. Upstream integrity modifiers are
    /// absolute values that get summed, so two sources holding the same organ down cancel each
    /// other out into a healthier organ than either intended — a heart with ischaemia would be
    /// cured by also being starved. Keeping hypoxia on our own field composes correctly, is
    /// trivially reversible, and lets the analyser separate "this organ is torn" from "this
    /// organ is suffocating", which is the more useful thing for a doctor to know anyway.
    /// </remarks>
    [ViewVariables, AutoNetworkedField]
    public float HypoxicInjury;

    /// <summary>
    /// Most of the function hypoxia is allowed to take.
    /// </summary>
    /// <remarks>
    /// Never all of it. Dying of blood loss should be dying of blood loss, not of an organ
    /// that quietly fell off along the way — destroying an organ is the job of a wound, a
    /// disease, or a surgeon.
    /// </remarks>
    [DataField]
    public float InjuryCap = 0.85f;
}
