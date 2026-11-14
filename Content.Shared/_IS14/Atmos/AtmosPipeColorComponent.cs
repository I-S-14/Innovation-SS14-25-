using JetBrains.Annotations;
using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Atmos;

// IS14: Wizards #30645, adapted to retain the Goob pipe-painting API and console notifications.
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class AtmosPipeColorComponent : Component
{
    [DataField, AutoNetworkedField]
    public Color Color { get; set; } = Color.White;

    [ViewVariables(VVAccess.ReadWrite), UsedImplicitly]
    public Color ColorVV
    {
        get => Color;
        set => IoCManager.Resolve<IEntityManager>().System<SharedAtmosPipeColorSystem>().SetColor((Owner, this), value);
    }
}

[ByRefEvent]
public record struct AtmosPipeColorChangedEvent(Color Color);
