// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared.Gatherable.Components;

/// <summary>
/// Destroys a gatherable entity when colliding with it.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(GatherableSystem))]
public sealed partial class GatheringProjectileComponent : Component
{
    /// <summary>
    /// How many more times we can gather.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Amount = 1;

    /// <summary>
    /// Goobstation
    /// The probability that the given projectile will actually be gathering
    /// </summary>
    [DataField]
    public float Probability = 1f;
}