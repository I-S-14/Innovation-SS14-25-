// SPDX-License-Identifier: MIT
using Content.Shared.Chat;
using Content.Shared.Speech.Components;

namespace Content.Shared.Speech.EntitySystems;

/// <summary>
/// This system redirects local chat messages to listening entities (e.g., radio microphones).
/// </summary>
public sealed partial class ListeningSystem : EntitySystem
{
    [Dependency] private SharedTransformSystem _xforms = default!;

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
        var obfuscatedEv = !isWhisper ? null : new ListenEvent(_chat.ObfuscateMessageReadability(message), originalMessage, source); // Einstein Engines - Language
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
}
