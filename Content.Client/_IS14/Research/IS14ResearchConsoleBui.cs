// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Research;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Research;

public sealed class IS14ResearchConsoleBui : BoundUserInterface
{
    [ViewVariables]
    private IS14ResearchConsoleWindow? _window;

    public IS14ResearchConsoleBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<IS14ResearchConsoleWindow>();
        _window.OnResearchPressed += id => SendMessage(new IS14ResearchConsoleUnlockMessage(id));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is IS14ResearchConsoleUiState castState)
            _window?.UpdateState(castState);
    }
}
