// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Robust.Shared.Prototypes;

namespace Content.Shared.Magic.Events;

[Virtual]
public partial class ProjectileSpellEvent : WorldTargetActionEvent // Goob edit
{
    /// <summary>
    /// What entity should be spawned.
    /// </summary>
    [DataField(required: true)]
    public EntProtoId Prototype;

    // Goobstation
    [DataField]
    public float Speed = 40f;

    // IS14-merge: restored member lost in the upstream merge
    /// <summary>
    /// How fast the projectile should travel
    /// </summary>
    [DataField]
    public float ProjectileSpeed = 25f;

    // IS14-merge: restored member lost in the upstream merge
    /// <summary>
    /// A coefficient to adjust velocity by when shooting the opposite way you're moving.
    /// </summary>
    /// <remarks>
    /// Useful for slow projectiles to prevent them from feeling sluggish.
    /// A value of 0.5 increases projectile speed by 50% if shot directly behind you.
    /// </remarks>
    [DataField]
    public float RearVelocityCompensation;
}
