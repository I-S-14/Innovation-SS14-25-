// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.GameObjects;

using static Content.Shared.Paper.PaperComponent;

namespace Content.Client.Paper.UI;

public sealed class PaperVisualizerSystem : VisualizerSystem<PaperVisualsComponent>
{
    [Dependency] private readonly SpriteSystem _sprite = default!;

    protected override void OnAppearanceChange(EntityUid uid, PaperVisualsComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (AppearanceSystem.TryGetData<PaperStatus>(uid, PaperVisuals.Status, out var writingStatus, args.Component))
            _sprite.LayerSetVisible((uid, args.Sprite), PaperVisualLayers.Writing, writingStatus == PaperStatus.Written);

        if (AppearanceSystem.TryGetData<string>(uid, PaperVisuals.Stamp, out var stampState, args.Component))
        {
            if (stampState != string.Empty)
            {
                _sprite.LayerSetRsiState((uid, args.Sprite), PaperVisualLayers.Stamp, stampState);

                //IS14-change start: the shared greyscale state is tinted with the stamp's own ink.
                // Always set the colour, so a coloured state never inherits a tint from before.
                if (!AppearanceSystem.TryGetData<Color>(uid, PaperVisuals.StampColor, out var stampColor, args.Component))
                    stampColor = Color.White;

                _sprite.LayerSetColor((uid, args.Sprite), PaperVisualLayers.Stamp, stampColor);
                //IS14-change end

                _sprite.LayerSetVisible((uid, args.Sprite), PaperVisualLayers.Stamp, true);
            }
            else
            {
                _sprite.LayerSetVisible((uid, args.Sprite), PaperVisualLayers.Stamp, false);
            }

        }
    }
}

public enum PaperVisualLayers
{
    Stamp,
    Writing
}