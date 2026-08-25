// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Server.Wires;
using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modsuit.Systems;
using Content.Shared.Wires;

namespace Content.Server._IS14.Modsuit.Wires;

/// <summary>
///     Keys the wire panel uses to remember what it has done to a suit.
/// </summary>
public enum ModsuitWireKey : byte
{
    LockStatus,
    MalfunctionStatus,
    ShockStatus,
    InterfaceStatus,
}

/// <summary>
///     Green light. Pulsing flips the ID lock; cutting wipes the access list for good.
/// </summary>
public sealed partial class ModsuitLockWireAction : BaseWireAction
{
    public override Color Color { get; set; } = Color.Green;
    public override string Name { get; set; } = "wire-name-mod-lock";
    public override object StatusKey { get; } = ModsuitWireKey.LockStatus;

    private SharedModsuitLockSystem _lock = default!;

    public override void Initialize()
    {
        base.Initialize();
        _lock = EntityManager.System<SharedModsuitLockSystem>();
    }

    public override StatusLightState? GetLightState(Wire wire)
    {
        if (!EntityManager.TryGetComponent<ModsuitLockComponent>(wire.Owner, out var comp))
            return null;

        if (comp.AccessWiped)
            return StatusLightState.Off;

        return comp.Locked ? StatusLightState.On : StatusLightState.BlinkingSlow;
    }

    public override bool Cut(EntityUid user, Wire wire)
    {
        if (EntityManager.TryGetComponent<ModsuitLockComponent>(wire.Owner, out var comp))
            _lock.WipeAccess((wire.Owner, comp));

        return true;
    }

    public override bool Mend(EntityUid user, Wire wire)
    {
        // Access, once wiped, is gone — mending the wire does not remember it.
        return true;
    }

    public override void Pulse(EntityUid user, Wire wire)
    {
        if (EntityManager.TryGetComponent<ModsuitLockComponent>(wire.Owner, out var comp) && !comp.AccessWiped)
            _lock.SetLocked((wire.Owner, comp), !comp.Locked);
    }
}

/// <summary>
///     Red light. A cut wire leaves the suit malfunctioning until it is mended.
/// </summary>
public sealed partial class ModsuitMalfunctionWireAction : BaseWireAction
{
    public override Color Color { get; set; } = Color.Red;
    public override string Name { get; set; } = "wire-name-mod-malfunction";
    public override object StatusKey { get; } = ModsuitWireKey.MalfunctionStatus;

    /// <summary>Seconds a pulse leaves the suit glitching before it recovers.</summary>
    [DataField]
    private int _pulseTimeout = 30;

    private SharedModsuitLockSystem _lock = default!;

    public override void Initialize()
    {
        base.Initialize();
        _lock = EntityManager.System<SharedModsuitLockSystem>();
    }

    public override StatusLightState? GetLightState(Wire wire)
    {
        return IsMalfunctioning(wire.Owner) ? StatusLightState.Off : StatusLightState.BlinkingFast;
    }

    private bool IsMalfunctioning(EntityUid uid)
    {
        return EntityManager.TryGetComponent<Content.Shared._IS14.Modular.Components.ChassisPowerComponent>(uid, out var power)
               && power.Malfunctioning;
    }

    public override bool Cut(EntityUid user, Wire wire)
    {
        _lock.SetMalfunctioning(wire.Owner, true);
        WiresSystem.TryCancelWireAction(wire.Owner, ModsuitWireKey.MalfunctionStatus);
        return true;
    }

    public override bool Mend(EntityUid user, Wire wire)
    {
        _lock.SetMalfunctioning(wire.Owner, false);
        return true;
    }

    public override void Pulse(EntityUid user, Wire wire)
    {
        _lock.SetMalfunctioning(wire.Owner, true);

        WiresSystem.StartWireAction(
            wire.Owner,
            _pulseTimeout,
            ModsuitWireKey.MalfunctionStatus,
            new TimedWireEvent(AwaitPulseCancel, wire));
    }

    private void AwaitPulseCancel(Wire wire)
    {
        _lock.SetMalfunctioning(wire.Owner, false);
    }
}

/// <summary>
///     Orange light. Electrifies the suit against whoever pokes at it.
/// </summary>
public sealed partial class ModsuitShockWireAction : BaseWireAction
{
    public override Color Color { get; set; } = Color.Orange;
    public override string Name { get; set; } = "wire-name-mod-shock";
    public override object StatusKey { get; } = ModsuitWireKey.ShockStatus;

    [DataField]
    private int _pulseTimeout = 30;

    private SharedModsuitLockSystem _lock = default!;

    public override void Initialize()
    {
        base.Initialize();
        _lock = EntityManager.System<SharedModsuitLockSystem>();
    }

    public override StatusLightState? GetLightState(Wire wire)
    {
        if (!EntityManager.TryGetComponent<ModsuitSabotageComponent>(wire.Owner, out var comp))
            return null;

        return _lock.IsElectrified((wire.Owner, comp)) ? StatusLightState.BlinkingFast : StatusLightState.Off;
    }

    public override bool Cut(EntityUid user, Wire wire)
    {
        if (EntityManager.TryGetComponent<ModsuitSabotageComponent>(wire.Owner, out var comp))
            _lock.Electrify((wire.Owner, comp), null);

        return true;
    }

    public override bool Mend(EntityUid user, Wire wire)
    {
        if (EntityManager.TryGetComponent<ModsuitSabotageComponent>(wire.Owner, out var comp))
            _lock.ClearElectrification((wire.Owner, comp));

        return true;
    }

    public override void Pulse(EntityUid user, Wire wire)
    {
        if (EntityManager.TryGetComponent<ModsuitSabotageComponent>(wire.Owner, out var comp))
            _lock.Electrify((wire.Owner, comp), TimeSpan.FromSeconds(_pulseTimeout));
    }
}

/// <summary>
///     Yellow light. Breaks the suit's own interface, so the wearer is stuck with
///     whatever configuration they had when it went.
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
