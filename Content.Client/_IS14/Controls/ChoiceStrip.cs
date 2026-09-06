// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Linq;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._IS14.Controls;

/// <summary>
///     Pick one of a handful of options, laid out as tiles rather than hidden behind a dropdown.
///
///     A dropdown makes sense for dozens of entries; for a short list it hides the choices and
///     forces two clicks to see them. This shows all of them at once, which also lets each
///     option carry an icon or a colour block — the difference between reading "Retro" and
///     seeing what Retro looks like.
/// </summary>
public sealed class ChoiceStrip : BoxContainer
{
    private IS14ThemePalette _palette = IS14ThemePalette.Default;
    private readonly List<ChoiceStripItem> _items = new();
    private string? _selectedId;

    public event Action<string>? OnChoiceSelected;

    public IS14ThemePalette Palette
    {
        get => _palette;
        set
        {
            _palette = value;
            Rebuild();
        }
    }

    public string? SelectedId
    {
        get => _selectedId;
        set
        {
            if (_selectedId == value)
                return;

            _selectedId = value;
            UpdateSelection();
        }
    }

    public ChoiceStrip()
    {
        Orientation = LayoutOrientation.Vertical;
    }

    /// <summary>
    ///     Replaces the options. Rebuilds only when the set actually changed, so a state push
    ///     that only moves the selection does not throw the controls away under the cursor.
    /// </summary>
    public void SetItems(IReadOnlyList<ChoiceStripItem> items, string? selectedId)
    {
        if (!_items.Select(i => i.Id).SequenceEqual(items.Select(i => i.Id)))
        {
            _items.Clear();
            _items.AddRange(items);
            _selectedId = selectedId;
            Rebuild();
            return;
        }

        // Same options: refresh their captions and colours in place, then move the highlight.
        for (var i = 0; i < items.Count; i++)
        {
            _items[i] = items[i];

            if (i < ChildCount && GetChild(i) is IconTile tile)
            {
                tile.Caption = items[i].Caption;
                tile.Icon = items[i].Icon;
                tile.Swatch = items[i].Swatch;
                tile.ToolTip = items[i].ToolTip;
            }
        }

        SelectedId = selectedId;
    }

    private void Rebuild()
    {
        RemoveAllChildren();

        foreach (var item in _items)
        {
            var tile = new IconTile
            {
                Palette = _palette,
                Compact = Orientation == LayoutOrientation.Vertical,
                Caption = item.Caption,
                Icon = item.Icon,
                Swatch = item.Swatch,
                IconSize = 14,
                ToolTip = item.ToolTip,
                HorizontalExpand = Orientation == LayoutOrientation.Vertical,
                Selected = item.Id == _selectedId,
                Margin = Orientation == LayoutOrientation.Vertical
                    ? new Thickness(0, 0, 0, 2)
                    : new Thickness(0, 0, 2, 0),
            };

            var id = item.Id;
            tile.OnPressed += _ =>
            {
                SelectedId = id;
                OnChoiceSelected?.Invoke(id);
            };

            AddChild(tile);
        }
    }

    private void UpdateSelection()
    {
        for (var i = 0; i < _items.Count && i < ChildCount; i++)
        {
            if (GetChild(i) is IconTile tile)
                tile.Selected = _items[i].Id == _selectedId;
        }
    }
}

/// <param name="Id">Value reported back when the option is picked.</param>
/// <param name="Caption">Label shown on the tile.</param>
/// <param name="Icon">Optional glyph.</param>
/// <param name="Swatch">Optional colour block, shown instead of a glyph.</param>
public readonly record struct ChoiceStripItem(
    string Id,
    string Caption,
    Texture? Icon = null,
    Color? Swatch = null,
    string? ToolTip = null);
