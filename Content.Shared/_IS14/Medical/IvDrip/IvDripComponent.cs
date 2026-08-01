// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Medical.IvDrip;

/// <summary>
/// A drip stand: a pole on wheels with a bag hanging off it and a needle on a line.
/// Dragged onto a patient it goes in, and from then on it moves a little of the bag into
/// them — or a little of them into the bag — every few seconds, unattended.
/// </summary>
/// <remarks>
/// The point of it over a syringe is that it is slow and it is left behind. A syringe is
/// one doctor spending one action; a drip is a doctor walking away, which is the whole
/// difference between treating one patient and treating a ward.
/// </remarks>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class IvDripComponent : Component
{
    /// <summary>Slot the bag hangs in.</summary>
    [DataField]
    public string SlotId = "iv_pack";

    /// <summary>
    /// Who the needle is in, or null when it is coiled on the stand. Networked because
    /// the sprite changes with it and the line is drawn to whoever it names.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? Patient;

    /// <summary>Which way the fluid goes.</summary>
    [DataField, AutoNetworkedField]
    public IvDripMode Mode = IvDripMode.Inject;

    /// <summary>
    /// Whether anything actually moved on the last tick. Drives the running animation, so
    /// a stand that has run dry or filled up visibly stops rather than miming forever.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Flowing;

    /// <summary>How long the doctor spends getting the needle in.</summary>
    [DataField]
    public TimeSpan AttachDelay = TimeSpan.FromSeconds(3);

    /// <summary>
    /// How long a patient spends pulling the needle out of themselves. Short, but not
    /// nothing: it is meant to be a decision, not a reflex.
    /// </summary>
    [DataField]
    public TimeSpan DetachDelay = TimeSpan.FromSeconds(1.5);

    /// <summary>How much moves per tick right now.</summary>
    [DataField, AutoNetworkedField]
    public FixedPoint2 TransferAmount = FixedPoint2.New(2);

    /// <summary>
    /// Rates the valve can be set to, slowest first. A fixed set rather than a free
    /// number because the choice is a medical one — a slow drip over minutes or a fast
    /// one that empties the bag — and three options make that choice legible where a
    /// slider would just be fiddly.
    /// </summary>
    [DataField]
    public List<FixedPoint2> TransferAmounts = new()
    {
        FixedPoint2.New(1),
        FixedPoint2.New(2),
        FixedPoint2.New(5),
        FixedPoint2.New(10),
    };

    /// <summary>
    /// Seconds between ticks. Together with <see cref="TransferAmount"/> this is the
    /// whole dosing rate — slow enough that a drip is not a hypospray with extra steps.
    /// </summary>
    [DataField]
    public TimeSpan TransferInterval = TimeSpan.FromSeconds(2);

    /// <summary>
    /// How far the patient can get before the needle tears out, in metres. The line is a
    /// leash of sorts, but a rude one: nothing reels anybody in, it just comes out.
    /// </summary>
    [DataField]
    public float Range = 2.5f;

    /// <summary>
    /// Fill fractions the bag gauge is cut at, lowest first. The server buckets the bag
    /// against these so the sprite never has to know what counts as nearly empty.
    /// </summary>
    [DataField]
    public List<float> FillThresholds = new() { 0f, 0.1f, 0.25f, 0.5f, 0.75f, 0.8f, 0.9f };

    /// <summary>Colour the line is drawn in when there is nothing in it to colour it.</summary>
    [DataField]
    public Color EmptyLineColor = Color.White;

    /// <summary>Played when the needle goes in.</summary>
    [DataField]
    public SoundSpecifier? AttachSound = new SoundPathSpecifier("/Audio/Items/hypospray.ogg");

    /// <summary>Played when it comes out, however it came out.</summary>
    [DataField]
    public SoundSpecifier? DetachSound = new SoundPathSpecifier("/Audio/Effects/Fluids/splat.ogg");

    /// <summary>When the next tick is due. Server-side bookkeeping.</summary>
    [ViewVariables]
    public TimeSpan NextTransfer;
}

/// <summary>Which way a drip moves fluid.</summary>
[Serializable, Robust.Shared.Serialization.NetSerializable]
public enum IvDripMode : byte
{
    /// <summary>Bag into patient.</summary>
    Inject,

    /// <summary>Patient into bag.</summary>
    Draw,
}
