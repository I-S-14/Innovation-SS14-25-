// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Ordnance;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Ordnance;

public sealed class IS14LaunchConsoleBui : BoundUserInterface
{
    [ViewVariables]
    private IS14LaunchConsoleWindow? _window;

    public IS14LaunchConsoleBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<IS14LaunchConsoleWindow>();
        _window.OnLaunch += () => SendMessage(new IS14LaunchConsoleStartMessage());
        _window.OnAbort += () => SendMessage(new IS14LaunchConsoleAbortMessage());
        _window.OnCountdownChanged += seconds => SendMessage(new IS14LaunchConsoleSetCountdownMessage(seconds));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is IS14LaunchConsoleUiState castState)
            _window?.UpdateState(castState);
    }
}
