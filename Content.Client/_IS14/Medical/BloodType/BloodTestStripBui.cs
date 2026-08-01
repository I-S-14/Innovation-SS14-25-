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
/// <remarks>
/// Carries no state. Everything the card shows — its wells, how far along the reaction is,
/// the name written on it, whether the field is unlocked — is on the networked component, so
/// the window reads that directly and stays right without anybody pushing updates at it.
/// </remarks>
[UsedImplicitly]
public sealed class BloodTestStripBui : BoundUserInterface
{
    [ViewVariables]
    private BloodStripWindow? _window;

    public BloodTestStripBui(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<BloodStripWindow>();
        _window.SetOwner(Owner);
        _window.OnPatientChanged += patient => SendMessage(new BloodStripSetPatientMessage(patient));
    }
}
