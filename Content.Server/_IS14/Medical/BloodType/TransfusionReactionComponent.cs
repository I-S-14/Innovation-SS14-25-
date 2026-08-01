// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Server._IS14.Medical.BloodType;

/// <summary>
/// Bookkeeping for how loudly a body is currently complaining about the wrong blood.
/// </summary>
/// <remarks>
/// A drip ticks every couple of seconds and a bad bag stays bad, so without a cooldown the
/// patient would be buried in identical popups. The damage keeps coming either way — this
/// only rations the shouting.
/// </remarks>
[RegisterComponent]
public sealed partial class TransfusionReactionComponent : Component
{
    [DataField]
    public TimeSpan WarningInterval = TimeSpan.FromSeconds(8);

    [ViewVariables]
    public TimeSpan NextWarning;
}
