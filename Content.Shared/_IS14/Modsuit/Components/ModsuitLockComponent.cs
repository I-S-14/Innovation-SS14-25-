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
    ///     Watts the shell costs the core while it is live. Charged whether the suit is
    ///     running or not — the capacitor is held ready either way.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float ElectrifiedDraw = 10f;

    /// <summary>
    ///     Set by cutting the shock wire — electrified with no expiry.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool PermanentlyElectrified;

    /// <summary>
    ///     The actuator link is cut: deploy and fold commands never reach the plating.
    ///     The parts themselves are untouched, so a suit sabotaged this way is stuck in
    ///     whatever shape it was in — open on somebody's back, or a rucksack they cannot
    ///     get into.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool DeployCut;

    /// <summary>
    ///     The seal link is cut: nothing can be pressurised or opened up. A suit caught
    ///     sealed stays sealed, which sounds like a win until the air runs out.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool SealCut;

    /// <summary>
    ///     How many of the power leads have been cut. The suit runs on either one of
    ///     them; losing both takes the core out of circuit until one is mended.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int PowerWiresCut;

    /// <summary>
    ///     How many power leads there are. Must match the count in the wire layout, or
    ///     the suit either never loses power or loses it a wire early.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int PowerWireCount = 2;

    /// <summary>
    ///     Game time until which the suit is running its power circuit too hard. Costs
    ///     charge and says so, and is what a pulse on a power lead buys you.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan? OverloadedUntil;

    /// <summary>
    ///     Watts an overload burns on top of everything else.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float OverloadDraw = 10f;
}
