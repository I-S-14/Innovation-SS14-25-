// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Components;
using Content.Shared.Alert;

namespace Content.Shared._IS14.Modsuit.Systems;

/// <summary>
///     The charge readout the wearer gets without opening anything.
///
///     The suit window is a dashboard you go and look at; this is the number you need
///     while you are busy. It sits in the alert strip next to internals and pressure,
///     which is where a player already looks when something is about to go wrong.
/// </summary>
public sealed partial class SharedModsuitSystem
{
    /// <summary>
    ///     Severity steps on the alert sprite. Zero is an empty cell, ten is a full one.
    /// </summary>
    private const short ChargeAlertSteps = 10;

    /// <summary>
    ///     Pushes the wearer's charge alert to match the suit. Safe to call often — the
    ///     alert system drops a repeat of a state it is already showing.
    /// </summary>
    public void RefreshChargeAlert(Entity<ModsuitControlComponent> ent)
    {
        // The drain loop is server-authoritative and so is the alert. Predicting it
        // would only fight the state that follows a moment later.
        if (!_net.IsServer)
            return;

        if (ent.Comp.Wearer is not { } wearer || TerminatingOrDeleted(wearer))
            return;

        var (current, max) = _power.GetCharge(ent);

        // No core, or a core with nothing in it. Both read the same to the wearer: the
        // suit is jewellery until something is put back in it.
        if (max <= 0f)
        {
            _alerts.ClearAlert(wearer, ent.Comp.ChargeAlert);
            _alerts.ShowAlert(wearer, ent.Comp.NoChargeAlert);
            return;
        }

        var fraction = Math.Clamp(current / max, 0f, 1f);
        var severity = (short) MathF.Round(fraction * ChargeAlertSteps);

        _alerts.ClearAlert(wearer, ent.Comp.NoChargeAlert);
        _alerts.ShowAlert(wearer, ent.Comp.ChargeAlert, severity);
    }

    /// <summary>
    ///     Takes both alerts off whoever was wearing the suit. Called when the suit comes
    ///     off, and when it is about to be handed to somebody else.
    /// </summary>
    private void ClearChargeAlert(Entity<ModsuitControlComponent> ent, EntityUid wearer)
    {
        if (!_net.IsServer || TerminatingOrDeleted(wearer))
            return;

        _alerts.ClearAlert(wearer, ent.Comp.ChargeAlert);
        _alerts.ClearAlert(wearer, ent.Comp.NoChargeAlert);
    }
}
