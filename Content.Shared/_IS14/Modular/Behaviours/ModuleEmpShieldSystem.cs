// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Emp;
using Content.Shared.Power.Components;
using Robust.Shared.Containers;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     EMP screening for a chassis.
///
///     Putting <c>EmpResistance</c> on the chassis does nothing, which is the trap this
///     system exists to avoid: a pulse is applied to whichever entity holds the charge,
///     and that is the cell, nested two containers deep inside the suit. The resistance
///     has to be read where the pulse lands, so the shield answers on behalf of
///     everything it contains — it damps the pulse rather than cancelling it, so a
///     shielded suit is worth emptying a second grenade into.
/// </summary>
public sealed class ModuleEmpShieldSystem : ModuleBehaviourSystem<ModuleEmpShieldComponent>
{
    [Dependency] private readonly SharedContainerSystem _container = default!;

    /// <summary>
    ///     How far up the container chain a pulsed entity is allowed to look for a screen.
    ///     Cell inside core inside suit is three; the cap is only there so a pathological
    ///     nesting cannot turn every pulse into a long walk.
    /// </summary>
    private const int MaxDepth = 6;

    /// <summary>
    ///     What a pulse is worth once it has been through the screen. A tenth: enough
    ///     that one grenade is an inconvenience rather than a walk home in the dark.
    /// </summary>
    private const float Damping = 0.1f;

    public override void Initialize()
    {
        base.Initialize();

        // Not the pulse event: the engine allows exactly one subscription per
        // component/event pair, and BatteryComponent/EmpPulseEvent is already
        // SharedBatterySystem's. The attempt event is raised on the same entity
        // immediately before the pulse is built, and the pulse is scaled by the
        // target's own EmpResistance right after — so the screen writes itself into
        // that resistance instead of fighting for the handler.
        SubscribeLocalEvent<BatteryComponent, EmpAttemptEvent>(OnEmpAttempt);
    }

    protected override void Start(Entity<ModuleEmpShieldComponent> ent, EntityUid chassis)
    {
        EnsureComp<ChassisEmpShieldComponent>(chassis);
    }

    protected override void Stop(Entity<ModuleEmpShieldComponent> ent, EntityUid chassis)
    {
        if (!TerminatingOrDeleted(chassis))
            RemComp<ChassisEmpShieldComponent>(chassis);
    }

    /// <summary>
    ///     Re-decides, at the only moment it can matter, whether the entity about to be
    ///     pulsed is behind a screen. Doing it here rather than tracking every container
    ///     move means a cell that has left the suit keeps a stale resistance until the
    ///     next pulse — which is precisely when it gets corrected.
    /// </summary>
    private void OnEmpAttempt(Entity<BatteryComponent> ent, ref EmpAttemptEvent args)
    {
        if (IsScreened(ent.Owner))
            Screen(ent.Owner);
        else
            Unscreen(ent.Owner);
    }

    private void Screen(EntityUid uid)
    {
        if (HasComp<ChassisEmpScreenedComponent>(uid))
            return;

        var screened = AddComp<ChassisEmpScreenedComponent>(uid);

        if (!TryComp<EmpResistanceComponent>(uid, out var resistance))
        {
            resistance = AddComp<EmpResistanceComponent>(uid);
        }
        else
        {
            screened.HadResistance = true;
            screened.PrevStrength = resistance.StrengthMultiplier;
            screened.PrevDuration = resistance.DurationMultiplier;
        }

        resistance.StrengthMultiplier = screened.PrevStrength * Damping;
        resistance.DurationMultiplier = screened.PrevDuration * Damping;

        Dirty(uid, resistance);
        Dirty(uid, screened);
    }

    private void Unscreen(EntityUid uid)
    {
        if (!TryComp<ChassisEmpScreenedComponent>(uid, out var screened))
            return;

        if (screened.HadResistance && TryComp<EmpResistanceComponent>(uid, out var resistance))
        {
            resistance.StrengthMultiplier = screened.PrevStrength;
            resistance.DurationMultiplier = screened.PrevDuration;
            Dirty(uid, resistance);
        }
        else
        {
            RemComp<EmpResistanceComponent>(uid);
        }

        RemComp<ChassisEmpScreenedComponent>(uid);
    }

    /// <summary>
    ///     Whether anything holding this entity is a chassis with a live screen.
    /// </summary>
    private bool IsScreened(EntityUid uid)
    {
        var current = uid;

        for (var depth = 0; depth < MaxDepth; depth++)
        {
            if (HasComp<ChassisEmpShieldComponent>(current))
                return true;

            if (!_container.TryGetContainingContainer((current, null, null), out var container))
                return false;

            current = container.Owner;
        }

        return false;
    }
}
