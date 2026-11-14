// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Chat;
using Robust.Shared.Prototypes;

namespace Content.Shared.Speech.EntitySystems;

public sealed partial class EmotesMenuSystem : EntitySystem
{
    [Dependency] private SharedChatSystem _chat = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<PlayEmoteMessage>(OnPlayEmote);
    }

    private void OnPlayEmote(PlayEmoteMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        if (!player.HasValue)
            return;

        if (!ProtoMan.Resolve(msg.ProtoId, out var proto) || proto.ChatTriggers.Count == 0)
            return;

        _chat.TryEmoteWithChat(player.Value, msg.ProtoId); // Goob - emotespam
    }
}
