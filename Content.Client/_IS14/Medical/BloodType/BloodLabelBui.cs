// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Medical.BloodType;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Medical.BloodType;

/// <summary>
/// NOTE: named "…Bui" instead of "…BoundUserInterface" on purpose — the engine resolves BUI
/// types by FullName suffix (LooseGetType), so conventional names risk hijacking upstream lookups.
/// </summary>
[UsedImplicitly]
public sealed class BloodLabelBui : BoundUserInterface
{
    [ViewVariables]
    private BloodLabelWindow? _window;

    public BloodLabelBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BloodLabelWindow>();
        _window.OnWrite += type => SendMessage(new BloodLabelWriteMessage(type));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is BloodLabelUiState label)
            _window?.UpdateState(label);
    }
}
