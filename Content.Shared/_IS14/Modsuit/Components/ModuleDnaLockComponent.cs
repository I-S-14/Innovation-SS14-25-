// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Modsuit.Components;

/// <summary>
///     A lock keyed to a person rather than to a card.
///
///     The ID lock protects the hardware — a stolen suit cannot be stripped for parts.
///     This protects the suit itself: an imprinted MOD will not open up for anybody but
///     its owner, so taking one off a corpse gets you a rucksack.
///
///     It is deliberately the fragile lock. An EMP wipes the imprint, an emag burns it
///     out, and the wearer can always be talked out of the suit the ordinary ways. What
///     it stops is the cheap steal.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedModsuitLockSystem))]
public sealed partial class ModuleDnaLockComponent : Component
{
    /// <summary>
    ///     DNA of whoever imprinted the lock, or null while it is blank.
    /// </summary>
    [DataField, AutoNetworkedField]
    public string? Dna;

    /// <summary>
    ///     Burned out by an emag: the lock is permanently blank and cannot be set again.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Broken;
}
