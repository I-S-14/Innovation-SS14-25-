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
public sealed class BloodTestKitBui : BoundUserInterface
{
    [ViewVariables]
    private BloodTestWindow? _window;

    public BloodTestKitBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BloodTestWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is BloodTestKitUiState kit)
            _window?.UpdateState(kit);
    }
}
