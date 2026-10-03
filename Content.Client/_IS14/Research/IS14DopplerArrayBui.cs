// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Research;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Research;

public sealed class IS14DopplerArrayBui : BoundUserInterface
{
    [ViewVariables]
    private IS14DopplerArrayWindow? _window;

    public IS14DopplerArrayBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<IS14DopplerArrayWindow>();
        _window.OnPrint += number => SendMessage(new IS14DopplerPrintRecordMessage(number));
        _window.OnDelete += number => SendMessage(new IS14DopplerDeleteRecordMessage(number));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is IS14DopplerArrayUiState castState)
            _window?.UpdateState(castState);
    }
}
