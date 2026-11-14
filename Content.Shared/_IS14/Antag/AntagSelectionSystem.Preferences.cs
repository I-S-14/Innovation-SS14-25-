using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.Antag;

public abstract partial class AntagSelectionSystem
{
    private bool MatchesGoobPreferences(AntagSpecifierPrototype definition,
        List<ProtoId<AntagPrototype>> preferences,
        bool allowFallback = true)
    {
        if (PrefsContain(preferences, definition.PrefRoles))
            return true;

        // Selection runs primary preferences over the actual candidate pool first,
        // then retries unselected players with fallbacks. Eligibility itself accepts both.
        return allowFallback && PrefsContain(preferences, definition.FallbackRoles);
    }
}
