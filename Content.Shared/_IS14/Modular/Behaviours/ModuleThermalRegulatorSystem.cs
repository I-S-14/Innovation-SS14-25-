// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Temperature.Components;
using Content.Shared.Temperature.Systems;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Drives the wearer towards the temperature they dialled in.
/// </summary>
public sealed class ModuleThermalRegulatorSystem : ModuleBehaviourSystem<ModuleThermalRegulatorComponent>
{
    [Dependency] private readonly SharedTemperatureSystem _temperature = default!;

    private const string TargetKey = "temperature";

    /// <summary>
    ///     Kelvin at zero Celsius, so the setting can be shown in degrees the wearer
    ///     thinks in rather than the kelvin the simulation runs on.
    /// </summary>
    private const float Zero = 273.15f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModuleThermalRegulatorComponent, ModuleGetConfigEvent>(OnGetConfig);
        SubscribeLocalEvent<ModuleThermalRegulatorComponent, ModuleConfigChangedEvent>(OnConfigChanged);
    }

    /// <summary>
    ///     A climate loop that ran on an unpowered suit would be a blanket, not a module.
    /// </summary>
    protected override bool RequiresActive(Entity<ModuleThermalRegulatorComponent> ent) => true;

    protected override void Start(Entity<ModuleThermalRegulatorComponent> ent, EntityUid chassis)
    {
        ent.Comp.Running = true;
    }

    protected override void Stop(Entity<ModuleThermalRegulatorComponent> ent, EntityUid chassis)
    {
        ent.Comp.Running = false;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<ModuleThermalRegulatorComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (!comp.Running || GetChassis(uid) is not { } chassis)
                continue;

            if (GetChassisUser(chassis) is not { } wearer
                || !TryComp<TemperatureComponent>(wearer, out var temperature))
            {
                continue;
            }

            var diff = comp.Target - temperature.CurrentTemperature;

            if (MathF.Abs(diff) <= comp.Tolerance)
                continue;

            // Move at a fixed rate rather than proportionally to the error: a wearer
            // pulled out of a fire should warm the room's worth of heat away over a
            // believable stretch, not snap to comfortable the moment the door shuts.
            var step = Math.Clamp(diff, -comp.Rate * frameTime, comp.Rate * frameTime);

            // ChangeHeat is server-side; on the client this whole pass is a no-op, which
            // is what we want — body temperature is not a predicted quantity.
            _temperature.ChangeHeat(wearer, step * _temperature.GetHeatCapacity(wearer, temperature), true, temperature);
        }
    }

    private void OnGetConfig(Entity<ModuleThermalRegulatorComponent> ent, ref ModuleGetConfigEvent args)
    {
        if (ent.Comp.MaxTarget <= ent.Comp.MinTarget)
            return;

        // Offered in Celsius. The suit thinks in kelvin, the wearer does not.
        args.Entries.Add(new ModuleConfigEntry(
            TargetKey,
            Loc.GetString("chassis-config-regulator-temperature"),
            ModuleConfigKind.Number,
            ent.Comp.Target - Zero,
            min: ent.Comp.MinTarget - Zero,
            max: ent.Comp.MaxTarget - Zero,
            step: 1f));
    }

    private void OnConfigChanged(Entity<ModuleThermalRegulatorComponent> ent, ref ModuleConfigChangedEvent args)
    {
        if (args.Handled || args.Key != TargetKey || args.Value is not float celsius)
            return;

        args.Handled = true;

        ent.Comp.Target = Math.Clamp(celsius + Zero, ent.Comp.MinTarget, ent.Comp.MaxTarget);
        Dirty(ent);
    }
}
