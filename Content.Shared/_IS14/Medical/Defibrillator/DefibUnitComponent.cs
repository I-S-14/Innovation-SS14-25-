// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Medical.Defibrillator;

/// <summary>
/// A defibrillator built the way a real one is: the box stays where you put it and the
/// paddles come off it on a cord. The box is the defibrillator proper — it holds the
/// cell, the charge and the upstream <c>DefibrillatorComponent</c>.
/// </summary>
/// <remarks>
/// The cord, the cradle and everything about the paddles being tethered belong to the
/// generic leash and cord components; this only carries what is specific to a
/// defibrillator, which is the cell cover and the two tones.
/// </remarks>
[RegisterComponent, NetworkedComponent]
public sealed partial class DefibUnitComponent : Component
{
    /// <summary>Played at the paddles when they are taken in both hands and the unit charges.</summary>
    [DataField]
    public SoundSpecifier? WieldSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_safety_on.ogg");

    /// <summary>Played at the paddles when they are let go of and the unit powers down.</summary>
    [DataField]
    public SoundSpecifier? UnwieldSound = new SoundPathSpecifier("/Audio/Items/Defib/defib_safety_off.ogg");

    /// <summary>Tool quality needed to get the cell out. Not something to do mid-code-blue.</summary>
    [DataField]
    public string CellTool = "Screwing";

    /// <summary>How long unscrewing the cell cover takes.</summary>
    [DataField]
    public float CellToolDelay = 1.5f;

    /// <summary>Charge bucket last written to the appearance, to avoid dirtying it every tick.</summary>
    [ViewVariables]
    public int LastChargeLevel = -1;
}

/// <summary>
/// The paddles on the end of a unit's cord. They carry no power and no logic of their
/// own — held in one hand they are dead weight, wielded in both they switch the unit on,
/// and anything they touch is forwarded to the unit.
/// </summary>
/// <remarks>
/// Which unit they belong to is the leash's business, not this component's. This is only
/// here to mark which end of a lead is a pair of paddles.
/// </remarks>
[RegisterComponent, NetworkedComponent]
public sealed partial class DefibPaddlesComponent : Component
{
}
