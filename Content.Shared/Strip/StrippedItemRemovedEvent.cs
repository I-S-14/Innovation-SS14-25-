// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.Strip;

/// <summary>
///     Raised on an item a stripper is about to take out of somebody's inventory, before
///     the unequip happens.
/// </summary>
/// <remarks>
///     Handle it when the item belongs somewhere other than the stripper's hands and knows
///     how to get there itself — a powered suit's plating folds back into the suit it came
///     off, because the point of taking it off somebody is to get them out of it, not to
///     confiscate a boot. Handling it cancels the ordinary removal entirely.
/// </remarks>
[ByRefEvent]
public record struct StrippedItemRemovedEvent(EntityUid User, EntityUid Target, string Slot, bool Handled = false);
