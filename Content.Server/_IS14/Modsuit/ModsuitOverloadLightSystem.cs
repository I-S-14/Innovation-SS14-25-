// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Server.Wires;
using Content.Shared._IS14.Modsuit;
using Content.Shared._IS14.Modsuit.Components;
using Content.Shared._IS14.Modsuit.Systems;
using Content.Shared.Wires;

namespace Content.Server._IS14.Modsuit;

/// <summary>
///     The overload lamp on the wire panel.
///
///     Every other light in there reports a wire. This one reports the suit: a pulse on a
///     power lead drives the circuit too hard for ten seconds, and the only way a hacker
///     would otherwise know they had found a power lead is by watching the wearer's
///     battery. A lamp says it plainly, which is the point of hitting the right wire.
///
///     Server-side because the wire panel is.
/// </summary>
public sealed class ModsuitOverloadLightSystem : EntitySystem
{
    [Dependency] private readonly SharedModsuitLockSystem _lock = default!;
    [Dependency] private readonly WiresSystem _wires = default!;

    public override void Initialize()
    {
        base.Initialize();

        // Every sabotage state change already announces itself; overload rides along.
        SubscribeLocalEvent<ModsuitSabotageComponent, ModsuitSecurityChangedEvent>(OnSecurityChanged);

        // And once at spawn, so the lamp is on the panel from the first time it is opened
        // rather than appearing the moment something goes wrong.
        SubscribeLocalEvent<ModsuitSabotageComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<ModsuitSabotageComponent> ent, ref MapInitEvent args)
    {
        Refresh(ent);
    }

    private void OnSecurityChanged(Entity<ModsuitSabotageComponent> ent, ref ModsuitSecurityChangedEvent args)
    {
        Refresh(ent);
    }

    private void Refresh(Entity<ModsuitSabotageComponent> ent)
    {
        if (!HasComp<WiresComponent>(ent))
            return;

        // Off rather than absent: a lamp that vanishes when the suit is behaving tells a
        // hacker how many lights to expect, which is half the puzzle.
        var state = _lock.IsOverloaded(ent) ? StatusLightState.BlinkingFast : StatusLightState.Off;

        _wires.SetStatus(
            ent.Owner,
            ModsuitWireKey.OverloadStatus,
            new StatusLightData(Color.Purple, state, Loc.GetString("wire-status-mod-overload")));
    }
}
