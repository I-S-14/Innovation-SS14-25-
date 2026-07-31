// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Medical.Defibrillator;

/// <summary>Raised on the unit once the cell cover has been unscrewed.</summary>
[Serializable, NetSerializable]
public sealed partial class DefibCellDoAfterEvent : SimpleDoAfterEvent
{
}

/// <summary>
/// Raised on the paddles once the charge-up finishes. Deliberately the paddles' own
/// event rather than upstream's: the do-after has to belong to the doctor holding them,
/// not to a unit that may be round a corner by the time the shock lands.
/// </summary>
[Serializable, NetSerializable]
public sealed partial class DefibPaddlesZapDoAfterEvent : SimpleDoAfterEvent
{
}
