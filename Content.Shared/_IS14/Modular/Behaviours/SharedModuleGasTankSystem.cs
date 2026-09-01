// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modular.Components;
using Content.Shared.Atmos;
using Content.Shared.Atmos.Components;
using Content.Shared.Atmos.EntitySystems;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Hangs a tank off the chassis while the module is installed, and exposes which
///     gases the compressor should keep as ordinary module settings — so the choice is
///     made in the suit's own panel rather than through a bespoke window.
/// </summary>
public abstract class SharedModuleGasTankSystem : ModuleBehaviourSystem<ModuleGasTankComponent>
{
    [Dependency] private readonly SharedGasTankSystem _gasTank = default!;

    /// <summary>Settings key for the compressor's on/off switch.</summary>
    public const string PumpKey = "pump";

    /// <summary>Settings key for the bottle's release valve.</summary>
    public const string ValveKey = "valve";

    /// <summary>Settings key for hooking the wearer's lungs up to the bottle.</summary>
    public const string InternalsKey = "internals";

    /// <summary>
    ///     Pressure the bottle feeds a wearer at, matching an ordinary breathing tank.
    ///     The component's own default is a full atmosphere, which would empty the suit
    ///     into somebody's lungs several times faster than they can use it.
    /// </summary>
    private const float BreathingPressure = 21.3f;

    /// <summary>Prefix for the per-gas keys.</summary>
    public const string GasKeyPrefix = "gas-";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModuleGasTankComponent, ModuleGetConfigEvent>(OnGetConfig);
        SubscribeLocalEvent<ModuleGasTankComponent, ModuleConfigChangedEvent>(OnConfigChanged);
    }

    /// <summary>
    ///     The bottle exists for as long as the module is bolted in — not while the suit
    ///     is sealed, not while it is switched on. A tank that emptied itself every time
    ///     the wearer opened their chestplate would be useless, and the seal already
    ///     governs the two things it should: the compressor runs off the module being
    ///     enabled, and breathing needs the whole suit closed.
    /// </summary>
    protected override bool FollowsInstallation(Entity<ModuleGasTankComponent> ent) => true;

    protected override void Start(Entity<ModuleGasTankComponent> ent, EntityUid chassis)
    {
        if (ent.Comp.Applied)
            return;

        var tank = EnsureComp<GasTankComponent>(chassis);
        tank.Air.Volume = ent.Comp.Volume;
        tank.OutputPressure = BreathingPressure;

        // Hand back whatever the module was carrying when it was last pulled.
        if (ent.Comp.Stored is { } stored)
        {
            tank.Air.CopyFrom(stored);
            tank.Air.Volume = ent.Comp.Volume;
            ent.Comp.Stored = null;
        }

        ent.Comp.Applied = true;
        Dirty(chassis, tank);
    }

    protected override void Stop(Entity<ModuleGasTankComponent> ent, EntityUid chassis)
    {
        if (!ent.Comp.Applied)
            return;

        ent.Comp.Applied = false;

        if (TerminatingOrDeleted(chassis))
            return;

        // Whatever was in it goes with the module. Venting a canister into somebody's
        // face because they pulled the wrong card would be a different feature.
        if (TryComp<GasTankComponent>(chassis, out var tank))
            ent.Comp.Stored = tank.Air.Clone();

        RemComp<GasTankComponent>(chassis);
    }

    private void OnGetConfig(Entity<ModuleGasTankComponent> ent, ref ModuleGetConfigEvent args)
    {
        // The compressor switch and the release valve are not listed here: they live on
        // the gauge, where the pressure they act on is already on screen. Both still
        // arrive through this same channel, so the card and the gauge stay one mechanism.
        foreach (var gas in ent.Comp.Available)
        {
            args.Entries.Add(new ModuleConfigEntry(
                GasKeyPrefix + gas,
                Loc.GetString($"chassis-config-gas-{gas.ToString().ToLowerInvariant()}"),
                ModuleConfigKind.Bool,
                ent.Comp.Filtered.Contains(gas)));
        }
    }

    /// <summary>
    ///     The valve belongs to the tank the module granted, not to the module: it is the
    ///     same lever an ordinary canister has, reached through the suit's panel.
    /// </summary>
    private void SetValve(Entity<ModuleGasTankComponent> ent, bool open)
    {
        if (GetChassis(ent) is not { } chassis || !TryComp<GasTankComponent>(chassis, out var tank))
            return;

        if (tank.IsValveOpen == open)
            return;

        tank.IsValveOpen = open;
        Dirty(chassis, tank);
    }

    /// <summary>
    ///     Hooks the wearer's lungs to the bottle, or unhooks them.
    ///
    ///     The station's own way of doing this is the internals alert, and it still works —
    ///     but a suit that becomes a breathing rig only when every piece of it is closed is
    ///     not something a player will find by guessing, so the panel says so and offers
    ///     the switch beside the pressure it feeds from.
    /// </summary>
    private void ToggleInternals(Entity<ModuleGasTankComponent> ent)
    {
        if (GetChassis(ent) is not { } chassis || !TryComp<GasTankComponent>(chassis, out var tank))
            return;

        // The lungs being connected are the wearer's, not those of whoever happens to be
        // holding the panel open.
        var user = GetChassisUser(chassis);

        if (tank.IsConnected)
            _gasTank.DisconnectFromInternals((chassis, tank), user);
        else
            _gasTank.ConnectToInternals((chassis, tank), user);
    }

    private void OnConfigChanged(Entity<ModuleGasTankComponent> ent, ref ModuleConfigChangedEvent args)
    {
        if (args.Value is not bool on)
            return;

        if (args.Key == PumpKey)
        {
            ent.Comp.Filtering = on;

            // Pumping into an open bottle is pumping into the room, so starting the
            // compressor shuts the valve rather than quietly wasting the charge.
            if (on)
                SetValve(ent, false);

            args.Handled = true;
            Dirty(ent);
            return;
        }

        if (args.Key == InternalsKey)
        {
            ToggleInternals(ent);
            args.Handled = true;
            return;
        }

        if (args.Key == ValveKey)
        {
            SetValve(ent, on);

            // And the other direction: venting is the opposite of filling, so opening
            // the valve stops the compressor instead of fighting it.
            if (on && ent.Comp.Filtering)
            {
                ent.Comp.Filtering = false;
                Dirty(ent);
            }

            args.Handled = true;
            return;
        }

        if (!args.Key.StartsWith(GasKeyPrefix)
            || !Enum.TryParse<Gas>(args.Key[GasKeyPrefix.Length..], out var gas)
            || !ent.Comp.Available.Contains(gas))
            return;

        if (on)
        {
            if (!ent.Comp.Filtered.Contains(gas))
                ent.Comp.Filtered.Add(gas);
        }
        else
        {
            ent.Comp.Filtered.Remove(gas);
        }

        args.Handled = true;
        Dirty(ent);
    }
}
