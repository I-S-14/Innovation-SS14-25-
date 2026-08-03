// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.Tag;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Medical.BloodType;

/// <summary>
/// The writing on a blood bag: putting it there, and reading it back.
/// </summary>
/// <remarks>
/// Nothing about a container writes its own label. Filling a bag used to name the group on it,
/// which meant the answer cost a syringe and nothing else and made the whole testing half of
/// the system decoration. Now a label is somebody's handwriting: they tested the blood, or
/// they guessed, or they are lying, and the bag looks identical in all three cases.
/// </remarks>
public sealed class BloodLabelSystem : EntitySystem
{
    private static readonly ProtoId<TagPrototype> WriteTag = "Write";

    [Dependency] private readonly BloodTypeSystem _bloodType = default!;
    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly TagSystem _tags = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodLabelComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<BloodLabelComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<BloodLabelComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<BloodLabelComponent, BloodLabelWriteMessage>(OnWrite);
    }

    /// <summary>
    /// Writes a group on a container, or wipes the label when given null.
    /// </summary>
    public void SetLabel(EntityUid uid, ProtoId<BloodTypePrototype>? type)
    {
        var comp = EnsureComp<BloodLabelComponent>(uid);

        if (comp.Type == type)
            return;

        comp.Type = type;
        Dirty(uid, comp);

        if (_ui.HasUi(uid, BloodLabelUiKey.Key))
            _ui.SetUiState(uid, BloodLabelUiKey.Key, BuildState((uid, comp)));
    }

    /// <summary>What the label claims, which is not necessarily what is inside.</summary>
    public ProtoId<BloodTypePrototype>? GetLabel(Entity<BloodLabelComponent?> ent)
    {
        return Resolve(ent, ref ent.Comp, false) ? ent.Comp.Type : null;
    }

    // ── Writing ───────────────────────────────────────────────────────────────

    /// <summary>
    /// A marker opens the label. Containers that were never meant to be written on have no
    /// interface declared, so the pen falls through them and still does whatever it did before.
    /// </summary>
    private void OnInteractUsing(Entity<BloodLabelComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled
            || !_tags.HasTag(args.Used, WriteTag)
            || !_ui.HasUi(ent.Owner, BloodLabelUiKey.Key))
            return;

        args.Handled = true;
        _ui.TryOpenUi(ent.Owner, BloodLabelUiKey.Key, args.User);
    }

    private void OnUiOpened(Entity<BloodLabelComponent> ent, ref BoundUIOpenedEvent args)
    {
        _ui.SetUiState(ent.Owner, BloodLabelUiKey.Key, BuildState(ent));
    }

    /// <remarks>
    /// Re-checked here rather than trusted from the window, which can be told to send this at
    /// any moment with anything in it. Unlike the strip's patient name this is not permanent —
    /// a bag gets emptied, topped up and re-tested, and a label that could not be corrected
    /// would guarantee a shelf of bags that lie.
    /// </remarks>
    private void OnWrite(Entity<BloodLabelComponent> ent, ref BloodLabelWriteMessage args)
    {
        if (!HasPen(args.Actor))
        {
            _popup.PopupClient(Loc.GetString("is14-blood-label-need-pen"), ent.Owner, args.Actor);
            return;
        }

        // Null is the eraser. Anything else has to be something the marker actually offered,
        // so a hand-rolled message cannot write a slime group onto a bag of human blood.
        if (args.Type is { } type && !GetOptions(ent.Owner).Contains(type))
            return;

        SetLabel(ent.Owner, args.Type);
    }

    private bool HasPen(EntityUid user)
    {
        foreach (var held in _hands.EnumerateHeld(user))
        {
            if (_tags.HasTag(held, WriteTag))
                return true;
        }

        return false;
    }

    private BloodLabelUiState BuildState(Entity<BloodLabelComponent> ent)
    {
        return new BloodLabelUiState(GetOptions(ent.Owner), ent.Comp.Type);
    }

    /// <summary>
    /// The groups this container's label can be given.
    /// </summary>
    /// <remarks>
    /// Narrowed to the blood that is actually in there, so a bag of human blood does not offer
    /// sap. An empty bag offers everything, which is also the honest answer — nothing about it
    /// says what it is going to hold.
    /// </remarks>
    private List<ProtoId<BloodTypePrototype>> GetOptions(EntityUid uid)
    {
        var options = new List<ProtoId<BloodTypePrototype>>();

        if (_bloodType.TryGetSample(uid, out var sample))
        {
            foreach (var type in _bloodType.GetTypes(sample.Reagent))
            {
                options.Add(type.ID);
            }

            if (options.Count > 0)
                return options;
        }

        foreach (var type in _protos.EnumeratePrototypes<BloodTypePrototype>())
        {
            options.Add(type.ID);
        }

        // Enumeration order is not promised, and two clients disagreeing about button order
        // would be a small madness to debug.
        options.Sort(static (a, b) => string.CompareOrdinal(a.Id, b.Id));

        return options;
    }

    // ── Reading ───────────────────────────────────────────────────────────────

    private void OnExamined(Entity<BloodLabelComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange || ent.Comp.Type is not { } type || !_protos.TryIndex(type, out var proto))
            return;

        args.PushMarkup(Loc.GetString(
            "is14-blood-label-examine",
            ("type", Loc.GetString(proto.ShortName)),
            ("color", (proto.Color ?? Color.White).ToHex())));
    }
}
