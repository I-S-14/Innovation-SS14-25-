// SPDX-License-Identifier: MIT

using Robust.Client.GameObjects;
using Content.Shared.Atmos.Visuals;
using Content.Client.Power;

namespace Content.Client.Atmos.Visualizers;

/// <summary>
/// Controls client-side visuals for portable scrubbers.
/// </summary>
public sealed partial class PortableScrubberSystem : VisualizerSystem<PortableScrubberVisualsComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, PortableScrubberVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (args.TryGetData<bool>(PortableScrubberVisuals.IsFull, out var isFull)
            && args.TryGetData<bool>(PortableScrubberVisuals.IsRunning, out var isRunning))
        {
            var runningState = isRunning ? component.RunningState : component.IdleState;
            _sprite.LayerSetRsiState((uid, args.Sprite), PortableScrubberVisualLayers.IsRunning, runningState);

            var fullState = isFull ? component.FullState : component.ReadyState;
            _sprite.LayerSetRsiState((uid, args.Sprite), PowerDeviceVisualLayers.Powered, fullState);
        }

        if (args.TryGetData<bool>(PortableScrubberVisuals.IsDraining, out var isDraining))
        {
            _sprite.LayerSetVisible((uid, args.Sprite), PortableScrubberVisualLayers.IsDraining, isDraining);
        }
    }
}

public enum PortableScrubberVisualLayers : byte
{
    IsRunning,

    IsDraining
}