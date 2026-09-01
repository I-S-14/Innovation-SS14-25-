// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Shrugs off the knockdown and stun a shock baton is good for, at the cost of a
///     bite out of the core each time.
///
///     This is not <see cref="ModuleGrantComponentsComponent"/> with a marker on the end
///     of it, because the effect has to be paid for: the wearer needs a way back to the
///     suit that is absorbing the hit, and a granted component knows nothing about the
///     module that granted it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class ModuleStunAbsorberComponent : Component
{
    /// <summary>
    ///     Charge spent per absorbed hit. A flat cost rather than a draw: standing
    ///     around wearing it is free, being tased is not.
    /// </summary>
    [DataField]
    public float Cost = 5f;

    /// <summary>
    ///     Wearer the absorber is currently attached to, so it comes back off the same
    ///     one even if the suit changed hands in between.
    /// </summary>
    [ViewVariables]
    public EntityUid? GrantedTo;
}

/// <summary>
///     Put on whoever is wearing a suit with a running absorber. Carries the way back to
///     the module so the hit can be charged for.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class StunAbsorbedComponent : Component
{
    [ViewVariables]
    public EntityUid Module;
}
