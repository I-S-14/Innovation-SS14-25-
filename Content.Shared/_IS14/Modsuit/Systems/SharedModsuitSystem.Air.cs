// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modular.Components;
using Content.Shared.Atmos.Components;
using Content.Shared.Nutrition.Components;

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

    /// <summary>
    ///     Puts the faceplate up or down over the wearer's mouth.
    ///
    ///     A closed helmet is a closed helmet: you cannot eat or drink through it, the way
    ///     you cannot through any other sealed headgear in the game. The eating apparatus
    ///     module is the exception, and it is the only one — which is what makes it worth
    ///     a slot.
    ///
    ///     Deliberately not part of the helmet's sealedComponents: two owners of the same
    ///     component would fight, and the module can come and go without the helmet ever
    ///     being unsealed. One place decides, and it is this one.
    /// </summary>
    private void RefreshFace(Entity<ModsuitControlComponent> ent)
    {
        if (!ent.Comp.Parts.TryGetValue(HeadSlot, out var helmet) || TerminatingOrDeleted(helmet))
            return;

        var closed = TryComp<ModsuitPartComponent>(helmet, out var part) && part.Sealed;
        var blocked = closed && !HasFaceOpening(ent);

        if (blocked == HasComp<IngestionBlockerComponent>(helmet))
            return;

        if (blocked)
            EnsureComp<IngestionBlockerComponent>(helmet);
        else
            RemComp<IngestionBlockerComponent>(helmet);
    }

    /// <summary>
    ///     Whether any running module opens the faceplate. Installed is not enough — an
    ///     apparatus on a suit with no power is a shut iris like any other.
    /// </summary>
    private bool HasFaceOpening(Entity<ModsuitControlComponent> ent)
    {
        if (!TryComp<ModularChassisComponent>(ent, out var chassis))
            return false;

        foreach (var module in _chassis.GetModuleEntities((ent, chassis)))
        {
            if (!HasComp<ModuleFaceOpeningComponent>(module))
                continue;

            if (TryComp<ChassisModuleComponent>(module, out var comp) && comp.Enabled)
                return true;
        }

        return false;
    }

    /// <summary>Slot key of the piece that covers the face.</summary>
    private const string HeadSlot = "head";
}
