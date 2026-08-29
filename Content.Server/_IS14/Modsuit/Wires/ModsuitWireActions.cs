// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Server.Wires;
using Content.Server._IS14.Modsuit;
using Content.Shared._IS14.Modsuit;
using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modsuit.Systems;
using Content.Shared._IS14.Modular;
using Content.Shared.Wires;

namespace Content.Server._IS14.Modsuit.Wires;

/// <summary>
///     Red. Two of these carry the core, and the suit runs on either one — cutting both
///     takes the core out of circuit until one is mended.
///
///     A pulse instead drives the circuit too hard for ten seconds, which costs charge and
///     nothing else. That is the honest reward for finding a power lead by guessing: you
///     have learned which wire it is, and paid the wearer's battery for the lesson.
/// </summary>
public sealed partial class ModsuitPowerWireAction : BaseWireAction
{
    public override Color Color { get; set; } = Color.Red;
    public override string Name { get; set; } = "wire-name-mod-power";
    public override object StatusKey { get; } = ModsuitWireKey.PowerStatus;

    /// <summary>Seconds a pulse leaves the circuit overloaded.</summary>
    [DataField]
    private int _overloadSeconds = 10;

    private SharedModsuitLockSystem _lock = default!;

    public override void Initialize()
    {
        base.Initialize();
        _lock = EntityManager.System<SharedModsuitLockSystem>();
    }

    /// <summary>
    ///     Both leads share one light, and it counts them down: steady on two, flickering
    ///     on one, dark on none. A hacker who cut one lead can see that the other exists.
    /// </summary>
    public override StatusLightState? GetLightState(Wire wire)
    {
        if (!EntityManager.TryGetComponent<ModsuitSabotageComponent>(wire.Owner, out var comp))
            return null;

        if (_lock.IsPowerCut((wire.Owner, comp)))
            return StatusLightState.Off;

        return comp.PowerWiresCut > 0 ? StatusLightState.BlinkingSlow : StatusLightState.On;
    }

    public override bool Cut(EntityUid user, Wire wire)
    {
        Change(wire, 1);
        return true;
    }

    public override bool Mend(EntityUid user, Wire wire)
    {
        Change(wire, -1);
        return true;
    }

    public override void Pulse(EntityUid user, Wire wire)
    {
        if (EntityManager.TryGetComponent<ModsuitSabotageComponent>(wire.Owner, out var comp))
            _lock.Overload((wire.Owner, comp), TimeSpan.FromSeconds(_overloadSeconds));
    }

    /// <summary>
    ///     Losing the last lead is the same event as a flat cell as far as the suit is
    ///     concerned, so it goes through the same door: the shell blows open a seal at a
    ///     time and everything hanging off the core stops.
    /// </summary>
    private void Change(Wire wire, int delta)
    {
        if (!EntityManager.TryGetComponent<ModsuitSabotageComponent>(wire.Owner, out var comp))
            return;

        if (!_lock.ChangePowerWiresCut((wire.Owner, comp), delta) || !_lock.IsPowerCut((wire.Owner, comp)))
            return;

        var ev = new ChassisPowerDepletedEvent();
        EntityManager.EventBus.RaiseLocalEvent(wire.Owner, ref ev);
    }
}

/// <summary>
///     Green. The link between the controller and the actuators in the plating.
///
///     Cut it and the deploy and fold buttons stop reaching the suit — the plating is
///     stuck in whatever shape it was in, which is a rucksack somebody cannot open or an
///     open suit somebody cannot close. Pulse it and every part moves at once.
/// </summary>
public sealed partial class ModsuitDeployWireAction : BaseWireAction
{
    public override Color Color { get; set; } = Color.Green;
    public override string Name { get; set; } = "wire-name-mod-deploy";
    public override object StatusKey { get; } = ModsuitWireKey.DeployStatus;

    private SharedModsuitLockSystem _lock = default!;
    private SharedModsuitSystem _modsuit = default!;

    public override void Initialize()
    {
        base.Initialize();
        _lock = EntityManager.System<SharedModsuitLockSystem>();
        _modsuit = EntityManager.System<SharedModsuitSystem>();
    }

    public override StatusLightState? GetLightState(Wire wire)
    {
        if (!EntityManager.HasComponent<ModsuitSabotageComponent>(wire.Owner))
            return null;

        return _lock.IsDeployCut(wire.Owner) ? StatusLightState.Off : StatusLightState.On;
    }

    public override bool Cut(EntityUid user, Wire wire)
    {
        if (EntityManager.TryGetComponent<ModsuitSabotageComponent>(wire.Owner, out var comp))
            _lock.SetDeployCut((wire.Owner, comp), true);

        return true;
    }

    public override bool Mend(EntityUid user, Wire wire)
    {
        if (EntityManager.TryGetComponent<ModsuitSabotageComponent>(wire.Owner, out var comp))
            _lock.SetDeployCut((wire.Owner, comp), false);

        return true;
    }

    public override void Pulse(EntityUid user, Wire wire)
    {
        if (EntityManager.TryGetComponent<ModsuitControlComponent>(wire.Owner, out var control))
            _modsuit.TryPulseDeploy((wire.Owner, control), user);
    }
}

/// <summary>
///     Blue. The link that carries pressure commands.
///
///     Cut it and nothing seals or unseals any more. A suit caught open cannot be closed,
///     and a suit caught sealed cannot be opened — which is the interesting half, because
///     the wearer is now living on whatever air is already in there.
/// </summary>
public sealed partial class ModsuitSealWireAction : BaseWireAction
{
    public override Color Color { get; set; } = Color.Blue;
    public override string Name { get; set; } = "wire-name-mod-seal";
    public override object StatusKey { get; } = ModsuitWireKey.SealStatus;

    private SharedModsuitLockSystem _lock = default!;
    private SharedModsuitSystem _modsuit = default!;

    public override void Initialize()
    {
        base.Initialize();
        _lock = EntityManager.System<SharedModsuitLockSystem>();
        _modsuit = EntityManager.System<SharedModsuitSystem>();
    }

    public override StatusLightState? GetLightState(Wire wire)
    {
        if (!EntityManager.HasComponent<ModsuitSabotageComponent>(wire.Owner))
            return null;

        return _lock.IsSealCut(wire.Owner) ? StatusLightState.Off : StatusLightState.On;
    }

    public override bool Cut(EntityUid user, Wire wire)
    {
        if (EntityManager.TryGetComponent<ModsuitSabotageComponent>(wire.Owner, out var comp))
            _lock.SetSealCut((wire.Owner, comp), true);

        return true;
    }

    public override bool Mend(EntityUid user, Wire wire)
    {
        if (EntityManager.TryGetComponent<ModsuitSabotageComponent>(wire.Owner, out var comp))
            _lock.SetSealCut((wire.Owner, comp), false);

        return true;
    }

    public override void Pulse(EntityUid user, Wire wire)
    {
        if (EntityManager.TryGetComponent<ModsuitControlComponent>(wire.Owner, out var control))
            _modsuit.TryToggleSeal((wire.Owner, control), user);
    }
}

/// <summary>
///     Orange. Arms the suit's own defence: the shell goes live for half a minute against
///     whoever opens the wire interface.
///
///     Pulsing and cutting both arm it, and cutting also puts the discharge straight
///     through the person holding the cutters. This is the one wire that bites back, so
///     there is no safe way to test it and no way to disarm it by snipping.
/// </summary>
public sealed partial class ModsuitShockWireAction : BaseWireAction
{
    public override Color Color { get; set; } = Color.Orange;
    public override string Name { get; set; } = "wire-name-mod-shock";
    public override object StatusKey { get; } = ModsuitWireKey.ShockStatus;

    private SharedModsuitLockSystem _lock = default!;
    private ModsuitShockSystem _shock = default!;

    public override void Initialize()
    {
        base.Initialize();
        _lock = EntityManager.System<SharedModsuitLockSystem>();
        _shock = EntityManager.System<ModsuitShockSystem>();
    }

    public override StatusLightState? GetLightState(Wire wire)
    {
        if (!EntityManager.TryGetComponent<ModsuitSabotageComponent>(wire.Owner, out var comp))
            return null;

        return _lock.IsElectrified((wire.Owner, comp)) ? StatusLightState.BlinkingFast : StatusLightState.Off;
    }

    public override bool Cut(EntityUid user, Wire wire)
    {
        if (!EntityManager.TryGetComponent<ModsuitSabotageComponent>(wire.Owner, out var comp))
            return true;

        var ent = new Entity<ModsuitSabotageComponent>(wire.Owner, comp);

        if (_shock.TryArm(ent))
            _shock.Zap(ent, user);

        return true;
    }

    public override bool Mend(EntityUid user, Wire wire)
    {
        if (EntityManager.TryGetComponent<ModsuitSabotageComponent>(wire.Owner, out var comp))
            _lock.ClearElectrification((wire.Owner, comp));

        return true;
    }

    /// <summary>
    ///     A pulse arms it without discharging: the multitool never touches the shell.
    ///     That is the one difference between the two, and it is worth knowing.
    /// </summary>
    public override void Pulse(EntityUid user, Wire wire)
    {
        if (EntityManager.TryGetComponent<ModsuitSabotageComponent>(wire.Owner, out var comp))
            _shock.TryArm((wire.Owner, comp));
    }
}

/// <summary>
///     Yellow. Breaks the suit's own interface, so the wearer is stuck with whatever
///     configuration they had when it went.
/// </summary>
public sealed partial class ModsuitInterfaceWireAction : BaseToggleWireAction
{
    public override Color Color { get; set; } = Color.Yellow;
    public override string Name { get; set; } = "wire-name-mod-interface";
    public override object StatusKey { get; } = ModsuitWireKey.InterfaceStatus;

    private SharedModsuitLockSystem _lock = default!;

    public override void Initialize()
    {
        base.Initialize();
        _lock = EntityManager.System<SharedModsuitLockSystem>();
    }

    public override StatusLightState? GetLightState(Wire wire)
    {
        return GetValue(wire.Owner) ? StatusLightState.On : StatusLightState.Off;
    }

    public override void ToggleValue(EntityUid owner, bool setting)
    {
        if (EntityManager.TryGetComponent<ModsuitSabotageComponent>(owner, out var comp))
            _lock.SetInterfaceBroken((owner, comp), !setting);
    }

    public override bool GetValue(EntityUid owner)
    {
        return !_lock.IsInterfaceBroken(owner);
    }
}
