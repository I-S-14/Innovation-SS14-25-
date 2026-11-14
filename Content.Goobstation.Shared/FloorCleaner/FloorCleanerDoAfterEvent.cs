// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Decals;
using Content.Shared.DoAfter;
using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.FloorCleaner;

// CorvaxGoob-Footprint-Refactoring очистка сущностей удалена, т.к. следы переделаны в decals
[Serializable, NetSerializable]
public sealed partial class FloorCleanerDoAfterEvent : DoAfterEvent
{
    [DataField]
    public HashSet<(DecalIndex Index, Decal Decal)> Decals = default!;

    public FloorCleanerDoAfterEvent(HashSet<(DecalIndex Index, Decal Decal)> decals)
    {
        Decals = decals;
    }

    public override DoAfterEvent Clone() => this;
}
