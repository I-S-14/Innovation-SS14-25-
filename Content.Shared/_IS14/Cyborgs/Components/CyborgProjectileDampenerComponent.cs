// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// tgstation's hyperkinetic dampening projector: while on, projectiles flying near the cyborg
/// lose part of their speed and damage. Drains the power cell.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CyborgProjectileDampenerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;

    [DataField]
    public float Range = 3f;

    [DataField]
    public float DamageMultiplier = 0.5f;

    [DataField]
    public float SpeedMultiplier = 0.5f;

    [DataField]
    public float ChargePerSecond = 4f;
}

/// <summary>
/// Marks a projectile that already went through a dampening field, so it is only weakened once.
/// Networked so the client can draw the shimmer on it.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyborgDampenedProjectileComponent : Component;
