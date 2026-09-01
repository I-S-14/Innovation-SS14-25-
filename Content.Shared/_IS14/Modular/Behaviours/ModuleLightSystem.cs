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

    private const string RadiusKey = "radius";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModuleLightComponent, ModuleGetConfigEvent>(OnGetConfig);
        SubscribeLocalEvent<ModuleLightComponent, ModuleConfigChangedEvent>(OnConfigChanged);
    }

    /// <summary>
    ///     A lamp you cannot switch off would not be a module, it would be a fixture.
    /// </summary>
    protected override bool RequiresActive(Entity<ModuleLightComponent> ent) => true;

    private void OnGetConfig(Entity<ModuleLightComponent> ent, ref ModuleGetConfigEvent args)
    {
        if (ent.Comp.MaxRadius <= ent.Comp.MinRadius)
            return;

        args.Entries.Add(new ModuleConfigEntry(
            RadiusKey,
            Loc.GetString("chassis-config-light-radius"),
            ModuleConfigKind.Number,
            ent.Comp.Radius,
            min: ent.Comp.MinRadius,
            max: ent.Comp.MaxRadius,
            step: 0.5f));
    }

    private void OnConfigChanged(Entity<ModuleLightComponent> ent, ref ModuleConfigChangedEvent args)
    {
        if (args.Handled || args.Key != RadiusKey || args.Value is not float radius)
            return;

        args.Handled = true;

        ent.Comp.Radius = Math.Clamp(radius, ent.Comp.MinRadius, ent.Comp.MaxRadius);
        Dirty(ent);

        // Only touch the lamp if this module is the one currently driving it — the beam
        // may belong to another module, or to nothing at all while this one is off.
        if (!ent.Comp.Applied
            || GetChassis(ent) is not { } chassis
            || !_light.TryGetLight(chassis, out var light))
        {
            return;
        }

        _light.SetRadius(chassis, ent.Comp.Radius, light);
    }

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
