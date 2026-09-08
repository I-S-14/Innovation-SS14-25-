// Licensed under IS14's EULA, see EULA.txt for more information.

namespace Content.Client._IS14.Controls;

/// <summary>
///     The IS14 palette as data rather than as static fields.
///
///     <see cref="IS14Palette"/> is one fixed look, which is right for hardware readouts that
///     should never change. Anything skinnable — the OS shell and its themes, for one — needs
///     to hand a different set of colours to the same controls, so the controls take one of
///     these instead of reaching for the statics.
/// </summary>
public sealed class IS14ThemePalette
{
    public Color Backdrop = IS14Palette.Backdrop;
    public Color Panel = IS14Palette.Panel;
    public Color PanelRaised = IS14Palette.PanelRaised;
    public Color Border = IS14Palette.Border;
    public Color BorderBright = IS14Palette.BorderBright;
    public Color Accent = IS14Palette.Accent;
    public Color Good = IS14Palette.Good;
    public Color Warn = IS14Palette.Warn;
    public Color Bad = IS14Palette.Bad;
    public Color Muted = IS14Palette.Muted;
    public Color Text = IS14Palette.Text;

    /// <summary>The stock IS14 hardware look. Shared, so never mutate it.</summary>
    public static readonly IS14ThemePalette Default = new();
}
