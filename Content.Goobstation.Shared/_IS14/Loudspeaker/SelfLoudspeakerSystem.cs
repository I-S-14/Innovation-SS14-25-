// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Goobstation.Common.Speech;
using Content.Goobstation.Shared.Loudspeaker.Components;
using Content.Goobstation.Shared.Loudspeaker.Events;

namespace Content.Goobstation.Shared._IS14.Loudspeaker;

/// <summary>
///     Lets an entity that carries a <see cref="LoudspeakerComponent"/> itself be the
///     loudspeaker, rather than only entities holding one as equipment.
///
///     The stock system builds its list from <c>GotEquippedEvent</c> and
///     <c>GotEquippedHandEvent</c>, so a loudspeaker only ever registers by being put in a
///     slot or a hand. That covers megaphones and nothing else: a MOD module lives in a
///     container inside the suit and is never equipped by anyone, so the component it
///     grants its wearer raises no equip event and the speaker ends up owning a
///     loudspeaker that no part of the chat path can see.
///
///     Rather than teach modules to fake equipment, this fills the obvious gap — you are
///     holding yourself — which keeps the module a plain grant and leaves the stock
///     system untouched.
///
///     Lives in this project because the loudspeaker components and events do; nothing in
///     Content.Shared can reference them.
/// </summary>
public sealed class SelfLoudspeakerSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<LoudspeakerComponent, GetLoudspeakerEvent>(OnGetLoudspeaker);
        SubscribeLocalEvent<LoudspeakerComponent, GetSpeechSoundEvent>(OnGetSpeechSound);
    }

    private void OnGetLoudspeaker(Entity<LoudspeakerComponent> ent, ref GetLoudspeakerEvent args)
    {
        if (args.Loudspeakers == null)
        {
            args.Loudspeakers = new List<EntityUid> { ent.Owner };
            return;
        }

        if (args.Loudspeakers.Contains(ent.Owner))
            return;

        // The holder hands back its own list by reference; appending in place would add
        // the speaker to their own equipment for the rest of the round.
        args.Loudspeakers = new List<EntityUid>(args.Loudspeakers) { ent.Owner };
    }

    private void OnGetSpeechSound(Entity<LoudspeakerComponent> ent, ref GetSpeechSoundEvent args)
    {
        // Unlike the holder's version this checks the switch: a module that has been
        // turned off should not keep talking through a megaphone.
        if (args.Handled || !ent.Comp.IsActive || ent.Comp.SpeechSounds is not { } sounds)
            return;

        args.SpeechSoundProtoId = sounds;
    }
}
