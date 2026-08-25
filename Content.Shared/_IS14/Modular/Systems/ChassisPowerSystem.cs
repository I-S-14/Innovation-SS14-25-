// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modular.Components;
using Content.Shared._IS14.Modular.Systems;
using Robust.Shared.Network;

namespace Content.Shared._IS14.Modular.Systems;

/// <summary>
///     Drains the chassis while it runs and routes charge queries to whoever holds the charge.
///     Nothing here knows what a MOD core is — a mech's internal cell answers the same events.
/// </summary>
public sealed class ChassisPowerSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedModularChassisSystem _chassis = default!;

    /// <summary>
    ///     Charge is spent in whole-second chunks rather than per tick, so a suit full
    ///     of modules does not hammer the battery with tiny writes.
    /// </summary>
    private const float DrainInterval = 1f;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // The server holds the authoritative charge. Draining client-side would
        // fight the networked state, so prediction deliberately sits this one out.
        if (!_net.IsServer)
            return;

        var query = EntityQueryEnumerator<ChassisPowerComponent, ModularChassisComponent>();
        while (query.MoveNext(out var uid, out var power, out var chassis))
        {
            if (!chassis.Active)
                continue;

            power.Accumulator += frameTime;
            if (power.Accumulator < DrainInterval)
                continue;

            var seconds = power.Accumulator;
            power.Accumulator = 0f;

            var draw = GetTotalDraw((uid, chassis), power) * seconds;
            if (draw <= 0f)
                continue;

            if (!TryUseCharge(uid, draw))
                OnDepleted((uid, power, chassis));
            else
                CheckLowCharge((uid, power));
        }
    }

    /// <summary>
    ///     Total watts drawn right now: the chassis baseline plus every running module.
    /// </summary>
    public float GetTotalDraw(Entity<ModularChassisComponent> chassis, ChassisPowerComponent power)
    {
        var draw = power.BaseDraw;

        foreach (var module in _chassis.GetModuleEntities(chassis))
        {
            if (!TryComp<ChassisModuleComponent>(module, out var comp) || !comp.Enabled)
                continue;

            draw += comp.IdleDraw;

            if (comp.Active && comp.Kind is ModuleKind.Toggleable or ModuleKind.Active)
                draw += comp.ActiveDraw;
        }

        draw *= power.DrawMultiplier;

        if (power.Malfunctioning)
            draw *= power.MalfunctionDrawMultiplier;

        return draw;
    }

    private void OnDepleted(Entity<ChassisPowerComponent, ModularChassisComponent> ent)
    {
        var ev = new ChassisPowerDepletedEvent();
        RaiseLocalEvent(ent, ref ev);
    }

    private void CheckLowCharge(Entity<ChassisPowerComponent> ent)
    {
        var (current, max) = GetCharge(ent);
        if (max <= 0f)
            return;

        var low = current / max <= ent.Comp.LowChargeWarningFraction;

        if (low == ent.Comp.LowChargeWarned)
            return;

        ent.Comp.LowChargeWarned = low;

        var ev = new ChassisPowerChangedEvent(current, max);
        RaiseLocalEvent(ent, ref ev);
    }

    #region Charge access

    /// <summary>
    ///     Current and maximum charge, or (0, 0) if nothing answers — no core installed.
    /// </summary>
    public (float Current, float Max) GetCharge(EntityUid chassis)
    {
        var ev = new ChassisGetChargeEvent(0f, 0f, false);
        RaiseLocalEvent(chassis, ref ev);

        return ev.Handled ? (ev.Current, ev.Max) : (0f, 0f);
    }

    public bool HasCharge(EntityUid chassis, float amount)
    {
        return GetCharge(chassis).Current >= amount;
    }

    /// <summary>
    ///     Spends charge. Returns false and spends nothing if the chassis cannot afford it.
    /// </summary>
    public bool TryUseCharge(EntityUid chassis, float amount)
    {
        if (amount <= 0f)
            return true;

        var ev = new ChassisTryUseChargeEvent(amount, false);
        RaiseLocalEvent(chassis, ref ev);

        if (!ev.Handled)
            return false;

        var (current, max) = GetCharge(chassis);
        var changed = new ChassisPowerChangedEvent(current, max);
        RaiseLocalEvent(chassis, ref changed);

        return true;
    }

    public bool TryAddCharge(EntityUid chassis, float amount)
    {
        if (amount <= 0f)
            return true;

        var ev = new ChassisAddChargeEvent(amount, false);
        RaiseLocalEvent(chassis, ref ev);

        if (!ev.Handled)
            return false;

        var (current, max) = GetCharge(chassis);
        var changed = new ChassisPowerChangedEvent(current, max);
        RaiseLocalEvent(chassis, ref changed);

        return true;
    }

    #endregion
}
