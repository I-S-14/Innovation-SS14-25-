// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Ordnance;

[Serializable, NetSerializable]
public enum IS14LaunchConsoleUiKey : byte
{
    Key,
}

[Serializable, NetSerializable]
public sealed class IS14LaunchConsoleUiState : BoundUserInterfaceState
{
    /// <summary>Seconds the console waits before opening the doors.</summary>
    public int Countdown;

    /// <summary>Seconds left, or null when no launch is running.</summary>
    public int? SecondsLeft;

    /// <summary>How long before the shot the early signal goes out, and how long after the late one.</summary>
    public int LeadSeconds;

    /// <summary>Devices wired to each of the three outputs.</summary>
    public int PreLinks;

    public int LaunchLinks;

    public int PostLinks;
}

[Serializable, NetSerializable]
public sealed class IS14LaunchConsoleStartMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class IS14LaunchConsoleAbortMessage : BoundUserInterfaceMessage;

[Serializable, NetSerializable]
public sealed class IS14LaunchConsoleSetCountdownMessage : BoundUserInterfaceMessage
{
    public readonly int Seconds;

    public IS14LaunchConsoleSetCountdownMessage(int seconds)
    {
        Seconds = seconds;
    }
}
