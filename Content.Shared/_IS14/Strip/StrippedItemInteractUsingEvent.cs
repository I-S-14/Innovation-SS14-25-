// Licensed under IS14's EULA, see EULA.txt for more information.

namespace Content.Shared._IS14.Strip;

/// <summary>
///     Raised on an item in somebody's inventory when a stripper clicks its slot while
///     holding something — "use this on that" instead of "take that off them".
/// </summary>
/// <remarks>
///     Handle it when the item is a piece of hardware the held thing has business with: a
///     screwdriver on a worn MOD belongs to the suit's panel, not to a person's back.
///     Handling it cancels the ordinary strip entirely; leaving it alone is what every
///     other item in the game does, so the strip window behaves exactly as before.
/// </remarks>
[ByRefEvent]
public record struct StrippedItemInteractUsingEvent(
    EntityUid User,
    EntityUid Used,
    EntityUid Target,
    string Slot,
    bool Handled = false);
