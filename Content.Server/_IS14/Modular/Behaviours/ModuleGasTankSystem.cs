// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Server.Atmos.EntitySystems;
using Content.Shared._IS14.Modular.Behaviours;
using Content.Shared._IS14.Modular.Components;
using Content.Shared._IS14.Modular.Systems;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;

namespace Content.Server._IS14.Modular.Behaviours;

/// <summary>
///     The compressor half of the atmospheric module: pulls the chosen gases out of the
///     room and packs them into the suit's own tank.
///
///     Server-side because only the server has an atmosphere to read. It runs on a slow
///     tick rather than every frame — a suit that filled a canister in a second would
///     make every other source of air pointless.
/// </summary>
public sealed class ModuleGasTankSystem : SharedModuleGasTankSystem
{
    [Dependency] private readonly AtmosphereSystem _atmos = default!;
    [Dependency] private readonly ChassisPowerSystem _power = default!;

    /// <summary>Seconds between compressor cycles.</summary>
    private const float Interval = 1f;

    private float _accumulator;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _accumulator += frameTime;

        if (_accumulator < Interval)
            return;

        var elapsed = _accumulator;
        _accumulator = 0f;

        var query = EntityQueryEnumerator<ModuleGasTankComponent, ChassisModuleComponent>();

        while (query.MoveNext(out var uid, out var module, out var chassisModule))
        {
            if (!module.Filtering || !chassisModule.Enabled || chassisModule.Chassis is not { } chassis)
                continue;

            if (module.Filtered.Count == 0 || !TryComp<GasTankComponent>(chassis, out var tank))
                continue;

            Compress((uid, module), chassis, tank, elapsed);
        }
    }

    private void Compress(
        Entity<ModuleGasTankComponent> ent,
        EntityUid chassis,
        GasTankComponent tank,
        float elapsed)
    {
        if (tank.Air.Pressure >= ent.Comp.TargetPressure)
            return;

        // The valve has its own verb on the suit, so it can be opened without the panel
        // ever hearing about it. Filling a bottle that is venting is not worth the charge.
        if (tank.IsValveOpen)
            return;

        // Read the air where the suit is, which is where the wearer is.
        if (_atmos.GetContainingMixture(chassis, false, true) is not { } environment)
            return;

        var temperature = MathF.Max(environment.Temperature, Atmospherics.TCMB);

        // How much more the bottle will hold before it is at pressure, worked out at the
        // temperature the gas is arriving at rather than the one already in there.
        var headroom = (ent.Comp.TargetPressure - tank.Air.Pressure)
                       * tank.Air.Volume
                       / (Atmospherics.R * temperature);

        if (headroom <= 0f)
            return;

        var rate = ent.Comp.FilterRate * elapsed;
        var budget = MathF.Min(rate, headroom);

        // Gas is carried across as a mixture rather than as bare moles: moles moved with
        // AdjustMoles leave their heat behind, and the bottle would sit at the 2.7 K a
        // fresh mixture starts at — no pressure, and nothing anyone could breathe.
        var haul = new GasMixture(tank.Air.Volume) { Temperature = temperature };
        var moved = 0f;

        foreach (var gas in ent.Comp.Filtered)
        {
            if (budget <= 0f)
                break;

            var available = environment.GetMoles(gas);

            // Leave a little behind rather than stripping the tile to vacuum: a suit
            // that could evacuate a room by standing in it is a weapon, not a module.
            available -= Reserve;

            if (available <= 0f)
                continue;

            var take = MathF.Min(budget, available);

            environment.AdjustMoles(gas, -take);
            haul.AdjustMoles(gas, take);

            budget -= take;
            moved += take;
        }

        if (moved <= 0f)
            return;

        // Charged for the work actually done, so a compressor idling in vacuum is free.
        var cost = ent.Comp.FilterDraw * elapsed * (moved / MathF.Max(rate, 0.0001f));

        // Paid for before it is handed over, so a failure puts the air back where it came
        // from instead of unpicking a mixture that has already been stirred in.
        if (_power.TryUseCharge(chassis, cost))
            _atmos.Merge(tank.Air, haul);
        else
            _atmos.Merge(environment, haul);
    }

    /// <summary>
    ///     Moles of a gas left on the tile no matter what. Roughly a breath.
    /// </summary>
    private const float Reserve = 1f;
}
