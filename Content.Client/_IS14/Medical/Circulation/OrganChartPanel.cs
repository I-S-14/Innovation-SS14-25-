// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using System.Numerics;
using Content.Goobstation.Maths.FixedPoint;
using Robust.Shared.Timing;
using Content.Shared._Shitmed.Medical.Surgery.Traumas;
using Content.Shared.Body.Organ;
using Content.Shared.Body.Part;
using Content.Shared.Body.Systems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._IS14.Medical.Circulation;

/// <summary>
/// The organs tab: the patient in the middle, their organs ranged around them, and whichever
/// one you last poked spelled out underneath.
/// </summary>
/// <remarks>
/// Built in code rather than XAML because the layout is generated — the number of organs is
/// whatever the body happens to have, and they are dealt out down either side of the figure.
/// <para>
/// The shape is the one every game with a paper-doll uses, and for the reason they all use it:
/// a list of organ names tells you nothing about the patient, while a body with readings hung
/// off it tells you at a glance where the trouble is. Detail is behind a click rather than
/// printed everywhere at once, which is what lets the tab carry twelve organs without becoming
/// a wall of text.
/// </para>
/// </remarks>
public sealed class OrganChartPanel : BoxContainer
{
    private readonly IEntityManager _entities;

    private readonly Label _heading;
    private readonly GridContainer _grid;
    private readonly Label _empty;

    // Detail block
    private readonly PanelContainer _detail;
    private readonly SpriteView _detailIcon;
    private readonly Label _detailName;
    private readonly Label _detailIntegrity;
    private readonly RichTextLabel _detailStatus;
    private readonly Label _detailLocation;

    private readonly Dictionary<EntityUid, OrganCard> _cards = new();
    private readonly List<(EntityUid Organ, FixedPoint2 Integrity, OrganSeverity Severity)> _last = new();

    private SharedBodySystem? _body;
    private EntityUid? _target;
    private EntityUid? _selected;

    public OrganChartPanel()
    {
        _entities = IoCManager.Resolve<IEntityManager>();

        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;

        // Two columns, worst first, reading left to right. No figure: the doll upstairs is
        // already the body, and a second one here only competed with it for the eye.
        _heading = new Label
        {
            Text = Loc.GetString("is14-analyzer-section-organs"),
            StyleClasses = { "LabelSubText" },
            FontColorOverride = Heading,
            Margin = new Thickness(0, 0, 0, 3),
        };

        _grid = new GridContainer
        {
            Columns = 3,
            HorizontalExpand = true,
        };

        _empty = new Label
        {
            Text = Loc.GetString("is14-analyzer-organs-none"),
            HorizontalAlignment = HAlignment.Center,
            FontColorOverride = Muted,
            Visible = false,
        };

        _detailIcon = new SpriteView
        {
            OverrideDirection = Direction.South,
            SetSize = new Vector2(48, 48),
            VerticalAlignment = VAlignment.Top,
            Margin = new Thickness(0, 0, 8, 0),
            Stretch = SpriteView.StretchMode.Fill,
        };

        _detailName = new Label { StyleClasses = { "LabelBig" } };
        _detailIntegrity = new Label { FontColorOverride = Muted };
        _detailStatus = new RichTextLabel { Margin = new Thickness(0, 2, 0, 0) };
        _detailLocation = new Label { FontColorOverride = Muted };

        _detail = new PanelContainer
        {
            Visible = false,
            Margin = new Thickness(0, 8, 0, 0),
            PanelOverride = new StyleBoxFlat { BackgroundColor = CardBack },
            Children =
            {
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    HorizontalExpand = true,
                    Margin = new Thickness(8, 6),
                    Children =
                    {
                        _detailIcon,
                        new BoxContainer
                        {
                            Orientation = LayoutOrientation.Vertical,
                            HorizontalExpand = true,
                            Children = { _detailName, _detailLocation, _detailIntegrity, _detailStatus },
                        },
                    },
                },
            },
        };

        AddChild(_heading);
        AddChild(_grid);
        AddChild(_empty);
        AddChild(_detail);
    }

    /// <summary>Points the chart at a patient. Everything after that it reads itself.</summary>
    /// <remarks>
    /// Organ integrity is a networked field, so the client already has it — asking the body
    /// directly means the chart is live on whichever tab it is sitting on, instead of being a
    /// snapshot that only arrives when the scanner happens to be in organs mode.
    /// </remarks>
    public void SetTarget(EntityUid? target)
    {
        if (_target == target)
            return;

        _target = target;
        _selected = null;
        _last.Clear();
        Rebuild();
    }

    protected override void FrameUpdate(FrameEventArgs args)
    {
        base.FrameUpdate(args);

        if (Changed())
            Rebuild();
    }

    /// <summary>Whether anything worth redrawing has moved since the last rebuild.</summary>
    private bool Changed()
    {
        if (_target is not { } target || Body is not { } body)
            return _last.Count > 0;

        var index = 0;

        foreach (var (organ, comp) in body.GetBodyOrgans(target))
        {
            if (index >= _last.Count)
                return true;

            var (lastOrgan, lastIntegrity, lastSeverity) = _last[index++];

            if (lastOrgan != organ || lastIntegrity != comp.OrganIntegrity || lastSeverity != comp.OrganSeverity)
                return true;
        }

        return index != _last.Count;
    }

    private SharedBodySystem? Body => _body ??= _entities.EntitySysManager.TryGetEntitySystem<SharedBodySystem>(out var sys)
        ? sys
        : null;

    private void Rebuild()
    {
        _grid.RemoveAllChildren();
        _cards.Clear();
        _last.Clear();

        var organs = new List<(EntityUid Organ, OrganComponent Comp)>();

        if (_target is { } target && Body is { } body)
        {
            foreach (var entry in body.GetBodyOrgans(target))
            {
                organs.Add((entry.Id, entry.Component));
                _last.Add((entry.Id, entry.Component.OrganIntegrity, entry.Component.OrganSeverity));
            }
        }

        // Worst first, so triage does not depend on the order the body happens to list its
        // organs in. Dealt alternately afterwards to keep the two columns level.
        var ordered = organs
            .Where(entry => entry.Comp.IntegrityCap > 0)
            .OrderBy(entry => (entry.Comp.OrganIntegrity / entry.Comp.IntegrityCap).Float())
            .ToList();

        foreach (var (organ, comp) in ordered)
        {
            var fraction = (comp.OrganIntegrity / comp.IntegrityCap).Float();
            var card = new OrganCard(organ, comp, fraction, _entities);

            card.OnPressed += _ => Toggle(organ);

            _cards[organ] = card;
            _grid.AddChild(card);
        }

        _empty.Visible = ordered.Count == 0;
        _heading.Visible = ordered.Count > 0;

        // Keep whatever was open open, so a refresh does not close the panel under the cursor.
        if (_selected is { } previous && _cards.ContainsKey(previous))
            Select(previous);
        else
            Deselect();
    }

    /// <summary>Клик по уже открытому органу закрывает подробности.</summary>
    private void Toggle(EntityUid organ)
    {
        if (_selected == organ)
            Deselect();
        else
            Select(organ);
    }

    private void Deselect()
    {
        _selected = null;
        _detail.Visible = false;

        foreach (var card in _cards.Values)
        {
            card.SetSelected(false);
        }
    }

    private void Select(EntityUid organ)
    {
        if (!_cards.TryGetValue(organ, out var card))
        {
            Deselect();
            return;
        }

        _selected = organ;

        foreach (var (uid, other) in _cards)
        {
            other.SetSelected(uid == organ);
        }

        var comp = card.Organ;
        var meta = _entities.GetComponent<MetaDataComponent>(organ);

        _detail.Visible = true;
        _detailIcon.SetEntity(organ);
        _detailName.Text = meta.EntityName;

        // Орган лежит в контейнере части тела, так что его родитель по трансформу и есть та
        // часть, где он находится — отдельного поля «где он» спрашивать не у кого.
        var parent = _entities.GetComponent<TransformComponent>(organ).ParentUid;

        _detailLocation.Visible = _entities.HasComponent<BodyPartComponent>(parent);
        _detailLocation.Text = _detailLocation.Visible
            ? Loc.GetString("is14-analyzer-organ-location",
                ("part", _entities.GetComponent<MetaDataComponent>(parent).EntityName))
            : string.Empty;

        _detailIntegrity.Text = Loc.GetString(
            "is14-analyzer-organ-integrity",
            ("current", comp.OrganIntegrity.Int()),
            ("max", comp.IntegrityCap.Int()));

        // The wording the rest of the analyser already uses, so a doctor reads the same phrase
        // here that they would have read in the old list. The organ's flavour description is
        // deliberately not repeated — it says nothing about this patient.
        _detailStatus.SetMessage(
            Loc.GetString($"condition-organ-damage-{comp.OrganSeverity}", ("organ", meta.EntityName)));
    }

    private static readonly Color Heading = Color.FromHex("#A88B5E");
    private static readonly Color Muted = Color.FromHex("#7A8B99");
    private static readonly Color Fine = Color.FromHex("#3ABB6A");
    private static readonly Color Hurt = Color.FromHex("#BB8A3A");
    private static readonly Color CardBack = Color.FromHex("#141C24");
    private static readonly Color DollBack = Color.FromHex("#0F1620");

    /// <summary>One organ on the chart: its icon, its name, and how intact it is.</summary>
    private sealed class OrganCard : ContainerButton
    {
        public readonly OrganComponent Organ;

        private readonly PanelContainer _panel;

        public OrganCard(EntityUid organ, OrganComponent comp, float fraction, IEntityManager entities)
        {
            Organ = comp;

            var colour = comp.OrganSeverity switch
            {
                OrganSeverity.Destroyed => Color.FromHex("#C4564E"),
                OrganSeverity.Damaged => Hurt,
                _ => Fine,
            };

            var name = entities.GetComponent<MetaDataComponent>(organ).EntityName;

            _panel = new PanelContainer
            {
                PanelOverride = new StyleBoxFlat { BackgroundColor = CardBack },
                Children =
                {
                    new BoxContainer
                    {
                        Orientation = LayoutOrientation.Horizontal,
                        HorizontalExpand = true,
                        Margin = new Thickness(9, 6),
                        Children =
                        {
                            MakeIcon(organ, 42),
                            new BoxContainer
                            {
                                Orientation = LayoutOrientation.Vertical,
                                HorizontalExpand = true,
                                Children =
                                {
                                    new Label { Text = name, HorizontalExpand = true },
                                    new Label
                                    {
                                        Text = Loc.GetString(
                                            "is14-analyzer-percent",
                                            ("percent", (int) MathF.Round(fraction * 100f))),
                                        FontColorOverride = colour,
                                    },
                                },
                            },
                        },
                    },
                },
            };

            Margin = new Thickness(3, 4);
            HorizontalExpand = true;
            AddChild(_panel);
        }

        /// <summary>A small view of the organ itself. Read-only property, so it is set after.</summary>
        private static SpriteView MakeIcon(EntityUid organ, float size)
        {
            var view = new SpriteView
            {
                OverrideDirection = Direction.South,
                SetSize = new Vector2(size, size),
                VerticalAlignment = VAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),

                // Fit — режим по умолчанию — только уменьшает и никогда не увеличивает, поэтому
                // спрайт 32 px оставался 32 px в коробке любого размера. Fill растягивает его до
                // размеров контрола с сохранением пропорций.
                Stretch = SpriteView.StretchMode.Fill,
            };

            view.SetEntity(organ);
            return view;
        }

        /// <summary>Lifts the selected card out of the row so the detail block has an owner.</summary>
        public void SetSelected(bool selected)
        {
            _panel.PanelOverride = new StyleBoxFlat
            {
                BackgroundColor = selected ? Color.FromHex("#1E2C3A") : CardBack,
            };
        }
    }
}
