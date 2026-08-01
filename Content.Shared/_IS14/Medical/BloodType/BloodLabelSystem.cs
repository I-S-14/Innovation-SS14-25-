// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Examine;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Medical.BloodType;

/// <summary>
/// The writing on a blood bag: putting it there, and reading it back.
/// </summary>
public sealed class BloodLabelSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _protos = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BloodLabelComponent, ExaminedEvent>(OnExamined);
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
    }

    /// <summary>What the label claims, which is not necessarily what is inside.</summary>
    public ProtoId<BloodTypePrototype>? GetLabel(Entity<BloodLabelComponent?> ent)
    {
        return Resolve(ent, ref ent.Comp, false) ? ent.Comp.Type : null;
    }

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
