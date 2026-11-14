// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.GameObjects;
using Robust.Client.Animations;

namespace Content.Client.Effects;

public sealed partial class EffectVisualizerSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<EffectVisualsComponent, AnimationCompletedEvent>(OnEffectAnimComplete);
    }

    private void OnEffectAnimComplete(EntityUid uid, EffectVisualsComponent component, AnimationCompletedEvent args)
    {
        QueueDel(uid);
    }
}