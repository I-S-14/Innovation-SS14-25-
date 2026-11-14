// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;
using Robust.Shared.GameStates;
using Content.Shared.AlertLevel;
using Content.Shared.Containers.ItemSlots;

namespace Content.Shared.PDA
{
    [RegisterComponent, NetworkedComponent]
    public sealed partial class PdaComponent : Component
    {
        public const string PdaIdSlotId = "PDA-id";
        public const string PdaPenSlotId = "PDA-pen";
        public const string PdaPaiSlotId = "PDA-pai";

        [DataField]
        public ItemSlot IdSlot = new();

        [DataField]
        public ItemSlot PenSlot = new();
        [DataField]
        public ItemSlot PaiSlot = new();

        // Really this should just be using ItemSlot.StartingItem. However, seeing as we have so many different starting
        // PDA's and no nice way to inherit the other fields from the ItemSlot data definition, this makes the yaml much
        // nicer to read.
        [DataField("id")]
        public EntProtoId? IdCard;

        // TODO: Fix persistence
        [ViewVariables] public EntityUid? ContainedId;
        [ViewVariables] public EntityUid? ContainedPen;//goob addition for pen
        [ViewVariables] public bool FlashlightOn;

        [ViewVariables(VVAccess.ReadWrite)] public string? OwnerName;
        // The Entity that "owns" the PDA, usually a player's character.
        // This is useful when we are doing stuff like renaming a player and want to find their PDA to change the name
        // as well.
        [ViewVariables(VVAccess.ReadWrite)] public EntityUid? PdaOwner;
        [ViewVariables] public string? StationName;
        [ViewVariables]
        public ProtoId<AlertLevelPrototype>? StationAlertLevel;
        [ViewVariables] public Color StationAlertColor = Color.White;
        [ViewVariables] public string? StationAlertInstructions; // CorvaxGoob-custom-alert-instructions-in-pda
    }
}
