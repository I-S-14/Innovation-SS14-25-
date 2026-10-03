// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Research;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Research;

public sealed class IS14WorkOrderBui : BoundUserInterface
{
    [ViewVariables]
    private IS14WorkOrderWindow? _window;

    public IS14WorkOrderBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<IS14WorkOrderWindow>();
        _window.OnOrderPressed += id => SendMessage(new IS14WorkOrderPlaceMessage(id));
        _window.OnCancelPressed += () => SendMessage(new IS14WorkOrderCancelMessage());
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is IS14WorkOrderUiState castState)
            _window?.UpdateState(castState);
    }
}
