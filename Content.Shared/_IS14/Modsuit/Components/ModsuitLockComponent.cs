// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Systems;
using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Modsuit.Components;

/// <summary>
///     The suit's ID lock. While engaged the hardware panel stays shut, so a stolen suit
///     cannot simply be stripped for its modules — you need the access, an emag, or the
///     hacking wire.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedModsuitLockSystem))]
public sealed partial class ModsuitLockComponent : Component
{
    /// <summary>
    ///     Whether the lock is currently engaged. Off by default: a suit is only worth
    ///     locking once someone has claimed it with their ID.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Locked;

    /// <summary>
    ///     Set by the hacking wire being cut: access requirements are gone for good.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool AccessWiped;
}

/// <summary>
///     Sabotage state driven by the wire panel and by EMP. A malfunctioning suit burns
///     charge, drops modules at random and lies to its owner about which button they pressed.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(SharedModsuitLockSystem))]
public sealed partial class ModsuitSabotageComponent : Component
{
    /// <summary>
    ///     Interface is broken: the panel UI refuses to open.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool InterfaceBroken;

    /// <summary>
    ///     Chance per second that a running module cuts out while malfunctioning.
    /// </summary>
    [DataField]
    public float ModuleDropoutChance = 0.05f;

    /// <summary>
    ///     Game time until which the suit shocks whoever touches it.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? ElectrifiedUntil;

    /// <summary>
    ///     Set by cutting the shock wire — electrified with no expiry.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool PermanentlyElectrified;

    /// <summary>
    ///     The emergency release has been cut. Somebody who expects to be arrested in
    ///     this suit will have done it in advance, and then the only ways out are the ID
    ///     lock, the core, and a cutting torch.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool ReleaseCut;
}
