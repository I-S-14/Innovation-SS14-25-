// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Ordnance;

/// <summary>
/// A detonation timer: the part that gives a transfer valve a countdown.
/// </summary>
/// <remarks>
/// The timing itself is the engine's <c>TimerTrigger</c>, which the valve grows while this part is
/// fitted and loses when it is pulled back out — so a valve without a timer has no countdown to
/// speak of, and opening it opens it there and then. Keeping the delays on the part means a
/// different timer can carry different delays without touching the valve.
/// </remarks>
[RegisterComponent, NetworkedComponent]
public sealed partial class IS14OrdnanceTimerComponent : Component
{
    /// <summary>Delay the timer starts on.</summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(10);

    /// <summary>Delays it can be cycled to once fitted.</summary>
    [DataField]
    public List<TimeSpan> DelayOptions = new()
    {
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(15),
        TimeSpan.FromSeconds(30),
    };
}
