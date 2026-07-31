// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Client._IS14.Medical.Defibrillator;

/// <summary>
/// Which RSI states a defibrillator mount uses for the charge gauge of the unit it
/// holds. The wall station and the crash cart share one sheet, so they share this too.
/// </summary>
[RegisterComponent]
public sealed partial class DefibMountVisualsComponent : Component
{
    /// <summary>One state per charge step, quietest first.</summary>
    [DataField]
    public List<string> ChargeStates = new()
    {
        "charge25",
        "charge50",
        "charge75",
        "charge100",
    };
}
