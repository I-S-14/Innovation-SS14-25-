// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Numerics;
using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.UserInterface.Controls;

/// <summary>
///     IS14 additions to inventory and hand slots: a small emblem drawn over the item.
///
///     Worn gear that belongs to something else — a device the MOD suit lent you, an item
///     out of its compartments — looks exactly like your own once it is in a slot. The
///     badge says whose it is without a tooltip.
/// </summary>
public abstract partial class SlotControl
{
    public TextureRect BadgeRect { get; private set; } = default!;

    /// <summary>
    ///     The emblem itself. Null hides it. Clicks pass straight through.
    /// </summary>
    public Texture? BadgeTexture
    {
        get => BadgeRect.Texture;
        set
        {
            BadgeRect.Texture = value;
            BadgeRect.Visible = value != null;
        }
    }

    /// <summary>
    ///     Called from the constructor, last, so the badge sits over the item rather than
    ///     under it.
    /// </summary>
    private void IS14InitBadge()
    {
        AddChild(BadgeRect = new TextureRect
        {
            TextureScale = new Vector2(2, 2),
            MouseFilter = MouseFilterMode.Ignore,
            Visible = false,
        });
    }
}
