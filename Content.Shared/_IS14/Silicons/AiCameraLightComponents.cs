// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Silicons;

/// <summary>
/// Retained Goob camera light receiver, separate from the camera activity sensor.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LightOnCollideComponent : Component
{
    [DataField]
    public string FixtureId = "aiLightSensor";

    public readonly HashSet<EntityUid> Colliders = new();
}

/// <summary>
/// Added to the AI's remote eye by the retained camera light action.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class LightOnCollideColliderComponent : Component
{
    [DataField]
    public string FixtureId = "aiLightTrigger";

    public readonly HashSet<EntityUid> LitCameras = new();
}
