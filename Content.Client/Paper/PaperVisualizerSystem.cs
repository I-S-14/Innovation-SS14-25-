// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Paper;
using Robust.Client.GameObjects;

using static Content.Shared.Paper.PaperComponent;

namespace Content.Client.Paper;

public sealed partial class PaperVisualizerSystem : VisualizerSystem<PaperVisualizerComponent>
{
    protected override void OnAppearanceChange(EntityUid uid, PaperVisualizerComponent component, ref AppearanceChangeEvent args)
    {
        if (args.Sprite == null)
            return;

        if (args.TryGetData<PaperStatus>(PaperVisuals.Status, out var writingStatus))
            SpriteSystem.LayerSetVisible((uid, args.Sprite), PaperVisualLayers.Writing, writingStatus == PaperStatus.Written);

        if (args.TryGetData<string>(PaperVisuals.Stamp, out var stampState))
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

/// <summary>
/// Sprite mapping enum.
/// </summary>
public enum PaperVisualLayers
{
    Stamp,
    Writing,
}
