// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Inventory.Events;
using Content.Shared.Speech;

namespace Content.Shared._Goobstation.Speech;

/// <summary>
/// System that replace your speech sound when you wearing specific clothing
/// </summary>
public sealed class SpeechSoundsReplacerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SpeechSoundsReplacerComponent, GotEquippedEvent>(OnEquip);
        SubscribeLocalEvent<SpeechSoundsReplacerComponent, GotUnequippedEvent>(OnUnequip);
    }

    private void OnEquip(Entity<SpeechSoundsReplacerComponent> replacer, ref GotEquippedEvent args)
    {
        //IS14-change start
        if (TryComp<SpeechComponent>(args.EquipTarget, out var speech))
        //IS14-change end
        {
            replacer.Comp.PreviousSound = speech.SpeechSounds;
            speech.SpeechSounds = replacer.Comp.SpeechSounds;
        }
    }

    private void OnUnequip(Entity<SpeechSoundsReplacerComponent> replacer, ref GotUnequippedEvent args)
    {
        //IS14-change start
        if (TryComp<SpeechComponent>(args.EquipTarget, out var speech))
        //IS14-change end
        {
            speech.SpeechSounds = replacer.Comp.PreviousSound;
            replacer.Comp.PreviousSound = null;
        }
    }
}
