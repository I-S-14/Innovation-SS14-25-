// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Medical.BloodType;

/// <summary>
/// A pocket analyser: press it to a patient or a bag and it reads the group off a drop.
/// </summary>
/// <remarks>
/// Reusable and unlimited on purpose. The thing being rationed is not the strips, it is the
/// four seconds — a doctor with a bleeding patient on the table knows the test exists and
/// chooses to skip it, and that choice is the entire mechanic. Charges would only add a way
/// to be stranded without one.
/// </remarks>
[RegisterComponent, NetworkedComponent]
public sealed partial class BloodTestKitComponent : Component
{
    /// <summary>How long a reading takes.</summary>
    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(4);

    /// <summary>Whether testing a container also writes the result on it.</summary>
    [DataField]
    public bool Labels = true;

    [DataField]
    public SoundSpecifier? Sound = new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg");
}

[Serializable, NetSerializable]
public sealed partial class BloodTestDoAfterEvent : SimpleDoAfterEvent;
