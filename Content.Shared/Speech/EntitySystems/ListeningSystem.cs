// SPDX-License-Identifier: MIT
using Robust.Shared.Random;
using Content.Shared.Chat;
using Content.Shared.Speech.Components;

namespace Content.Shared.Speech.EntitySystems;

/// <summary>
/// This system redirects local chat messages to listening entities (e.g., radio microphones).
/// </summary>
public sealed partial class ListeningSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _xforms = default!;
    [Dependency] private Robust.Shared.Random.IRobustRandom _random = default!; // Einstein Engines - Language

    [SubscribeLocalEvent]
    private void OnSpeak(EntitySpokeEvent ev)
    {
        PingListeners(ev.Source, ev.Message, ev.OriginalMessage, ev.IsWhisper); // Einstein Engines - Languages
    }

    public void PingListeners(EntityUid source, string message, string originalMessage, bool isWhisper) // Einstein Engines - Language
    {
        // TODO whispering / audio volume? Microphone sensitivity?
        // for now, whispering just arbitrarily reduces the listener's max range.

        var sourceXform = Transform(source);
        var sourcePos = _xforms.GetWorldPosition(sourceXform);

        var attemptEv = new ListenAttemptEvent(source);
        var ev = new ListenEvent(message, originalMessage, source);
        var obfuscatedEv = !isWhisper ? null : new ListenEvent(ObfuscateMessageReadability(message), originalMessage, source); // Einstein Engines - Language
        var query = EntityQueryEnumerator<ActiveListenerComponent, TransformComponent>();

        while(query.MoveNext(out var listenerUid, out var listener, out var xform))
        {
            if (xform.MapID != sourceXform.MapID)
                continue;

            // range checks
            // TODO proper speech occlusion
            var distance = (sourcePos - _xforms.GetWorldPosition(xform)).LengthSquared();
            if (distance > listener.Range * listener.Range)
                continue;

            RaiseLocalEvent(listenerUid, attemptEv);
            if (attemptEv.Cancelled)
            {
                attemptEv.Uncancel();
                continue;
            }

            if (obfuscatedEv != null && distance > SharedChatSystem.WhisperClearRange)
                RaiseLocalEvent(listenerUid, obfuscatedEv);
            else
                RaiseLocalEvent(listenerUid, ev);
        }
    }

    // Einstein Engines - Language: whisper obfuscation, moved here from the server ChatSystem
    // because wizden made ListeningSystem shared.
    private string ObfuscateMessageReadability(string message, float chance = 0.2f)
    {
        var modifiedMessage = new System.Text.StringBuilder(message);
        for (var i = 0; i < message.Length; i++)
        {
            if (char.IsWhiteSpace(modifiedMessage[i]))
                continue;

            if (_random.Prob(1 - chance))
                modifiedMessage[i] = '~';
        }

        return modifiedMessage.ToString();
    }
}
