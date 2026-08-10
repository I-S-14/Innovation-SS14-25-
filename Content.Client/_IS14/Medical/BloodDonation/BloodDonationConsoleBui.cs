// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Medical.BloodDonation;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Medical.BloodDonation;

public sealed class BloodDonationConsoleBui : BoundUserInterface
{
    [ViewVariables]
    private BloodDonationConsoleWindow? _window;

    public BloodDonationConsoleBui(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BloodDonationConsoleWindow>();
        _window.OnStop += () => SendMessage(new BloodDonationStopMessage());
        _window.OnPayout += () => SendMessage(new BloodDonationPayoutMessage());
        _window.OnSetRate += rate => SendMessage(new BloodDonationSetRateMessage(rate));
        _window.OnAutoStop += enabled => SendMessage(new BloodDonationAutoStopMessage(enabled));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is BloodDonationConsoleUiState s)
            _window?.UpdateState(s);
    }
}
