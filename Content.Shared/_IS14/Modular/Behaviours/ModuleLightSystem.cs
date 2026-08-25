// Licensed under IS14's EULA, see EULA.txt for more information.

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Drives the chassis' point light from a toggleable module.
///     The beam shape — cone mask and auto-rotation — is declared on the chassis
///     prototype, because the engine locks those fields to its own light system.
/// </summary>
public sealed class ModuleLightSystem : ModuleBehaviourSystem<ModuleLightComponent>
{
    [Dependency] private readonly SharedPointLightSystem _light = default!;

    /// <summary>
    ///     A lamp you cannot switch off would not be a module, it would be a fixture.
    /// </summary>
    protected override bool RequiresActive(Entity<ModuleLightComponent> ent) => true;

    protected override void Start(Entity<ModuleLightComponent> ent, EntityUid chassis)
    {
        if (ent.Comp.Applied)
            return;

        var light = _light.EnsureLight(chassis);

        _light.SetColor(chassis, ent.Comp.Color, light);
        _light.SetRadius(chassis, ent.Comp.Radius, light);
        _light.SetEnergy(chassis, ent.Comp.Energy, light);
        _light.SetSoftness(chassis, ent.Comp.Softness, light);
        _light.SetEnabled(chassis, true, light);

        ent.Comp.Applied = true;
    }

    protected override void Stop(Entity<ModuleLightComponent> ent, EntityUid chassis)
    {
        if (!ent.Comp.Applied)
            return;

        ent.Comp.Applied = false;

        if (TerminatingOrDeleted(chassis) || !_light.TryGetLight(chassis, out var light))
            return;

        _light.SetEnabled(chassis, false, light);
    }
}
