// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Medical.BloodType;

[Serializable, NetSerializable]
public enum BloodLabelUiKey : byte
{
    Key,
}

/// <summary>
/// What the marker is allowed to write on this container, and what is on it now.
/// </summary>
/// <remarks>
/// The options are narrowed to the species in the bag, never to the group. Which species is
/// in there is already free — the contents are printed on the bag when you examine it — while
/// the group is the entire thing a test is for, and an option list that knew it would hand it
/// over for nothing.
/// </remarks>
[Serializable, NetSerializable]
public sealed class BloodLabelUiState : BoundUserInterfaceState
{
    public readonly List<ProtoId<BloodTypePrototype>> Options;

    public readonly ProtoId<BloodTypePrototype>? Current;

    public BloodLabelUiState(List<ProtoId<BloodTypePrototype>> options, ProtoId<BloodTypePrototype>? current)
    {
        Options = options;
        Current = current;
    }
}

/// <summary>Somebody writing on the bag. Null wipes it.</summary>
[Serializable, NetSerializable]
public sealed class BloodLabelWriteMessage : BoundUserInterfaceMessage
{
    public readonly ProtoId<BloodTypePrototype>? Type;

    public BloodLabelWriteMessage(ProtoId<BloodTypePrototype>? type)
    {
        Type = type;
    }
}
