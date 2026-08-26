// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Components;
using Content.Shared.Atmos.Components;

namespace Content.Shared._IS14.Modsuit.Systems;

/// <summary>
///     Breathing out of the suit.
///
///     A MOD is a pressure vessel, not a mask: the helmet only becomes something you can
///     draw air through once every piece of the suit is closed. That is the whole of the
///     rule here — the bottle itself belongs to the atmospheric module and exists
///     independently, and internals connect through the ordinary
///     <see cref="BreathToolComponent"/> machinery once the helmet qualifies as one.
/// </summary>
public sealed partial class SharedModsuitSystem
{
    /// <summary>
    ///     Every piece deployed and sealed. Deliberately stricter than
    ///     <see cref="IsSealed"/>, which only asks that whatever is deployed is closed:
    ///     a sealed helmet over a folded chestplate holds no pressure at all.
    /// </summary>
    public bool IsFullySealed(Entity<ModsuitControlComponent> ent)
    {
        if (ent.Comp.Parts.Count == 0)
            return false;

        foreach (var part in ent.Comp.Parts.Values)
        {
            if (!TryComp<ModsuitPartComponent>(part, out var comp))
                return false;

            if (!comp.Deployed || !comp.Sealed || IsPartRuptured((part, comp)))
                return false;
        }

        return true;
    }

    /// <summary>
    ///     Hands the helmet its breath tool, or takes it back. Called whenever anything
    ///     that can change airtightness changes — sealing, folding, plating giving way.
    /// </summary>
    private void RefreshBreathing(Entity<ModsuitControlComponent> ent)
    {
        if (!ent.Comp.Parts.TryGetValue(HeadSlot, out var helmet) || TerminatingOrDeleted(helmet))
            return;

        var shouldBreathe = IsFullySealed(ent);

        if (shouldBreathe == HasComp<BreathToolComponent>(helmet))
            return;

        // Both directions are handled by the component's own lifetime: adding it connects
        // through LungSystem's init hook, and its shutdown disconnects internals, so the
        // wearer is not left drinking out of a suit that has been opened up.
        if (shouldBreathe)
            EnsureComp<BreathToolComponent>(helmet);
        else
            RemComp<BreathToolComponent>(helmet);
    }

    /// <summary>Slot key of the piece that covers the face.</summary>
    private const string HeadSlot = "head";
}
