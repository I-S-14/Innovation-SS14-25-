// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Medical.IvDrip;

/// <summary>
/// A bag that hangs on a drip stand. Everything about holding fluid is the ordinary
/// solution container; this only marks the thing as the right shape to hang, and says
/// which of its solutions the stand should be pushing around.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class IvPackComponent : Component
{
    /// <summary>Solution the stand draws from and fills.</summary>
    [DataField]
    public string SolutionName = "beaker";
}
