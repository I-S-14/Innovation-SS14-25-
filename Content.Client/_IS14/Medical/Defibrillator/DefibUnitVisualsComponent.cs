// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Client._IS14.Medical.Defibrillator;

/// <summary>
/// Which RSI states a defibrillator unit uses for its charge gauge. Kept in the
/// prototype rather than in code so a resprite never has to touch the visualizer.
/// </summary>
[RegisterComponent]
public sealed partial class DefibUnitVisualsComponent : Component
{
    /// <summary>
    /// One state per charge step, quietest first. The list length is the number of steps
    /// the gauge can show, and index zero is the first step above empty.
    /// </summary>
    [DataField]
    public List<string> ChargeStates = new()
    {
        "defibunit-charge25",
        "defibunit-charge50",
        "defibunit-charge75",
        "defibunit-charge100",
    };
}
