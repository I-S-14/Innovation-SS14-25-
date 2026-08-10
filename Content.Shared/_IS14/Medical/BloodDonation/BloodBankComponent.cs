// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared._IS14.Medical.BloodDonation;

/// <summary>
/// Marks a storage unit as the place a donation couch sends full bags to.
/// </summary>
/// <remarks>
/// Nothing but a marker: the shelf space, the whitelist and the interface are the ordinary
/// storage component, and this only says which cupboard in the room is the one medbay
/// means. Deliberately holds bags rather than a pooled tank of blood — pooling would mix
/// every donor into one solution, and by the compatibility rule a mixture carries the
/// union of everyone's antigens, so a full bank would be blood nobody on the station could
/// receive.
/// </remarks>
[RegisterComponent]
public sealed partial class BloodBankComponent : Component;
