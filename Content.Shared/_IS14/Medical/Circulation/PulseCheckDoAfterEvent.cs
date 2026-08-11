// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Medical.Circulation;

/// <summary>Holding two fingers against somebody's neck for a couple of seconds.</summary>
[Serializable, NetSerializable]
public sealed partial class PulseCheckDoAfterEvent : SimpleDoAfterEvent;
