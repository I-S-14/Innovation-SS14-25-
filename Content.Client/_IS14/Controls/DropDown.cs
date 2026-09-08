// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Linq;
using System.Numerics;
using Content.Client.Resources;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client._IS14.Controls;

/// <summary>
///     Pick one of a list, with the options folded away until asked for. The counterpart to
///     <see cref="ChoiceStrip"/>: the strip spends height to keep every option on screen, this
///     spends a click to keep the panel short.
///
///     It takes the same <see cref="ChoiceStripItem"/> the strip does, so an option keeps its
///     colour block or glyph both on the closed button and in the open list — a theme is still
///     something you pick by seeing it, not by reading its name.
///
///     Not the engine's OptionButton: that one is text-only and styled by the global
///     stylesheet, which would drop a stock SS14 button into a window painted by the device's
///     own theme.
/// </summary>
public sealed class DropDown : ContainerButton
{
    private const string TrianglePath = "/Textures/Interface/Nano/inverted_triangle.svg.png";

    /// <summary>The open list never grows past this; beyond it, it scrolls.</summary>
    private const float MaxListHeight = 220f;

    private readonly PanelContainer _swatch;
    private readonly TextureRect _icon;
    private readonly Label _caption;
    private readonly TextureRect _triangle;

    private readonly Popup _popup;
    private readonly PanelContainer _popupPanel;
    private readonly BoxContainer _popupList;

    private readonly List<ChoiceStripItem> _items = new();
    private IS14ThemePalette _palette = IS14ThemePalette.Default;
    private string? _selectedId;

    public event Action<string>? OnItemSelected;

    /// <summary>Shown when nothing is selected, or when there is nothing to select.</summary>
    public string? Placeholder { get; set; }

    /// <summary>Whether the list is currently down.</summary>
    public bool IsOpen => _popup.Visible;

    public IS14ThemePalette Palette
    {
        get => _palette;
        set
        {
            _palette = value;
            ApplyPalette();
        }
    }

    public string? SelectedId
    {
        get => _selectedId;
        set
        {
            _selectedId = value;
            UpdateFace();
            UpdateListSelection();
        }
    }

    public DropDown()
    {
        _swatch = new PanelContainer
        {
            MinSize = new Vector2(14, 14),
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            MouseFilter = MouseFilterMode.Ignore,
            Visible = false,
        };

        _icon = new TextureRect
        {
            SetSize = new Vector2(14, 14),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(0, 0, 6, 0),
            MouseFilter = MouseFilterMode.Ignore,
            Visible = false,
        };

        _caption = new Label
        {
            Align = Label.AlignMode.Left,
            HorizontalExpand = true,
            VerticalAlignment = VAlignment.Center,
            MouseFilter = MouseFilterMode.Ignore,
        };

        _triangle = new TextureRect
        {
            Texture = IoCManager.Resolve<IResourceCache>().GetTexture(TrianglePath),
            Stretch = TextureRect.StretchMode.KeepAspectCentered,
            SetSize = new Vector2(10, 10),
            VerticalAlignment = VAlignment.Center,
            Margin = new Thickness(6, 0, 0, 0),
            MouseFilter = MouseFilterMode.Ignore,
        };

        var face = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            MouseFilter = MouseFilterMode.Ignore,
        };

        face.AddChild(_swatch);
        face.AddChild(_icon);
        face.AddChild(_caption);
        face.AddChild(_triangle);

        AddChild(face);

        _popupList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
        };

        _popupPanel = new PanelContainer
        {
            Children =
            {
                new ScrollContainer
                {
                    // Without this a scroll container asks for no width at all and the list
                    // is squeezed to nothing.
                    ReturnMeasure = true,
                    MaxHeight = MaxListHeight,
                    HScrollEnabled = false,
                    Children = { _popupList },
                },
            },
        };

        _popup = new Popup { Children = { _popupPanel } };
        _popup.OnPopupHide += OnPopupHidden;

        OnPressed += _ => Toggle();

        ApplyPalette();
    }

    /// <summary>
    ///     Replaces the options. Rebuilt only when the set actually changed, so a state push
    ///     that only moves the selection does not tear the open list out from under the cursor.
    /// </summary>
    public void SetItems(IReadOnlyList<ChoiceStripItem> items, string? selectedId)
    {
        var same = _items.Select(i => i.Id).SequenceEqual(items.Select(i => i.Id));

        _items.Clear();
        _items.AddRange(items);

        if (!same)
            RebuildList();

        SelectedId = selectedId;
    }

    private void Toggle()
    {
        if (_popup.Visible)
        {
            _popup.Close();
            return;
        }

        if (_items.Count == 0 || Root == null)
            return;

        // Below the button, and never narrower than it: a list narrower than its own button
        // would clip the very names it exists to show.
        var origin = GlobalPosition + new Vector2(0, Size.Y + 1);

        _popupPanel.Measure(Window?.Size ?? Vector2Helpers.Infinity);
        var desired = _popupPanel.DesiredSize;

        Root.ModalRoot.AddChild(_popup);
        _popup.Open(UIBox2.FromDimensions(origin, new Vector2(MathF.Max(desired.X, Size.X), desired.Y)));

        DrawModeChanged();
    }

    private void OnPopupHidden()
    {
        _popup.Orphan();
        DrawModeChanged();
    }

    private void RebuildList()
    {
        _popupList.RemoveAllChildren();

        foreach (var item in _items)
        {
            var tile = new IconTile
            {
                Palette = _palette,
                Compact = true,
                Caption = item.Caption,
                Icon = item.Icon,
                Swatch = item.Swatch,
                IconSize = 14,
                ToolTip = item.ToolTip,
                HorizontalExpand = true,
                Selected = item.Id == _selectedId,
            };

            var id = item.Id;
            tile.OnPressed += _ =>
            {
                _popup.Close();
                SelectedId = id;
                OnItemSelected?.Invoke(id);
            };

            _popupList.AddChild(tile);
        }
    }

    private void UpdateListSelection()
    {
        for (var i = 0; i < _items.Count && i < _popupList.ChildCount; i++)
        {
            if (_popupList.GetChild(i) is IconTile tile)
                tile.Selected = _items[i].Id == _selectedId;
        }
    }

    /// <summary>Mirrors the chosen option onto the closed button.</summary>
    private void UpdateFace()
    {
        var item = _items.FirstOrDefault(i => i.Id == _selectedId);
        var found = item.Id != null;

        _caption.Text = found ? item.Caption : Placeholder;

        _icon.Texture = found ? item.Icon : null;
        _icon.Visible = _icon.Texture != null;

        _swatch.Visible = found && item.Swatch != null;

        if (_swatch.Visible)
        {
            _swatch.PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = item.Swatch!.Value,
                BorderColor = _palette.Border,
                BorderThickness = new Thickness(1),
            };
        }
    }

    private void ApplyPalette()
    {
        _caption.FontColorOverride = _palette.Text;
        _icon.ModulateSelfOverride = _palette.Text;
        _triangle.ModulateSelfOverride = _palette.Muted;

        _popupPanel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = _palette.Panel,
            BorderColor = _palette.BorderBright,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 2,
            ContentMarginRightOverride = 2,
            ContentMarginTopOverride = 2,
            ContentMarginBottomOverride = 2,
        };

        foreach (var child in _popupList.Children)
        {
            if (child is IconTile tile)
                tile.Palette = _palette;
        }

        UpdateFace();
        DrawModeChanged();
    }

    protected override void DrawModeChanged()
    {
        base.DrawModeChanged();

        // The base constructor gets here before our fields are assigned.
        if (_popup == null!)
            return;

        var open = _popup.Visible;

        var (background, border) = DrawMode switch
        {
            DrawModeEnum.Pressed => (_palette.Accent.WithAlpha(0.26f), _palette.Accent),
            DrawModeEnum.Hover => (_palette.PanelRaised, _palette.Accent.WithAlpha(0.7f)),
            DrawModeEnum.Disabled => (_palette.Panel.WithAlpha(0.5f), _palette.Border),
            _ => (_palette.PanelRaised, open ? _palette.Accent : _palette.Border),
        };

        StyleBoxOverride = new StyleBoxFlat
        {
            BackgroundColor = background,
            BorderColor = border,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 6,
            ContentMarginRightOverride = 6,
            ContentMarginTopOverride = 4,
            ContentMarginBottomOverride = 4,
        };

        Modulate = DrawMode == DrawModeEnum.Disabled ? new Color(1f, 1f, 1f, 0.55f) : Color.White;
    }
}
