// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.Prototypes;

namespace Content.Server._IS14.Cyborgs;

/// <summary>
/// Like <see cref="Content.Server.Antag.Components.AntagSpawnerComponent"/>, but spawns a weighted random pick:
/// the derelict cyborg event drifts in an NT cyborg of any specialization or a syndicate one.
/// </summary>
[RegisterComponent]
public sealed partial class CyborgRandomAntagSpawnerComponent : Component
{
    [DataField(required: true)]
    public Dictionary<EntProtoId, float> Prototypes = new();
}
