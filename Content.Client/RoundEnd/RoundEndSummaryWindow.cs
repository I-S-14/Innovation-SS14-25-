// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Client.Message;
// Goob Station - End of Round Screen
using Content.Client.Stylesheets;
using Content.Shared.GameTicking;
using Content.Shared.Mobs;
using Robust.Client.UserInterface;
using RoundEndPlayerInfo = Content.Shared.GameTicking.RoundEndMessageEvent.RoundEndPlayerInfo;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Utility;
using System.Linq;
using System.Numerics;
using static Robust.Client.UserInterface.Controls.BoxContainer;

namespace Content.Client.RoundEnd;

/// <summary>
/// Window displaying round end information including player manifest.
/// </summary>
public sealed partial class RoundEndSummaryWindow : DefaultWindow
{
    public sealed partial class RoundEndSummaryWindow : DefaultWindow // CorvaxGoob-PhotoCamera : made it partial
    {
        private readonly IFileDialogManager _fileDialogManager; // CorvaxGoob-PhotoCamera
        private readonly IEntityManager _entityManager;
        public int RoundId;

        public RoundEndSummaryWindow(string gm, string roundEnd, TimeSpan roundTimeSpan, int roundId,
            RoundEndMessageEvent.RoundEndPlayerInfo[] info, IEntityManager entityManager, IFileDialogManager fileDialogManager) // CorvaxGoob-PhotoCamera
        {
            _entityManager = entityManager;
            _fileDialogManager = fileDialogManager; // CorvaxGoob-PhotoCamera

            MinSize = new Vector2(610, 580); // CorvaxGoob-PhotoCamera : size changes

        //Gamemode Name
        var gamemodeLabel = new RichTextLabel();
        var gamemodeMessage = new FormattedMessage();
        gamemodeMessage.AddMarkupOrThrow(Loc.GetString("round-end-summary-window-round-id-label", ("roundId", roundId)));
        gamemodeMessage.AddText(" ");
        gamemodeMessage.AddMarkupOrThrow(Loc.GetString("round-end-summary-window-gamemode-name-label", ("gamemode", gamemode)));
        gamemodeLabel.SetMessage(gamemodeMessage);
        roundEndSummaryContainer.AddChild(gamemodeLabel);

        //Duration
        var roundTimeLabel = new RichTextLabel();
        roundTimeLabel.SetMarkup(Loc.GetString("round-end-summary-window-duration-label",
                                               ("hours", roundDuration.Hours),
                                               ("minutes", roundDuration.Minutes),
                                               ("seconds", roundDuration.Seconds)));
        roundEndSummaryContainer.AddChild(roundTimeLabel);

            RoundId = roundId;
            var roundEndTabs = new TabContainer();
            roundEndTabs.AddChild(MakeRoundEndSummaryTab(gm, roundEnd, roundTimeSpan, roundId));
            roundEndTabs.AddChild(MakePlayerManifestTab(info));
            roundEndTabs.AddChild(MakeStationReportTab()); //goob

            // CorvaxGoob-PhotoCamera
            var photoTab = MakePhotoReportTab();
            if (photoTab is not null)
                roundEndTabs.AddChild(photoTab);

            ContentsContainer.AddChild(roundEndTabs);

            OpenCenteredRight();
            MoveToFront();
        }

        roundEndSummaryContainerScrollbox.AddChild(roundEndSummaryContainer);
        roundEndSummaryTab.AddChild(roundEndSummaryContainerScrollbox);

        return roundEndSummaryTab;
    }

    private BoxContainer MakePlayerManifestTab()
    {
        var playerManifestTab = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            Name = Loc.GetString("round-end-summary-window-player-manifest-tab-title")
        };

            var roundEndSummaryContainerScrollbox = new ScrollContainer
            {
                VerticalExpand = true,
                Margin = new Thickness(10),
                //HScrollEnabled = false, CorvaxGoob-Coins
            };
            var roundEndSummaryContainer = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical
            };

        var searchLabel = new Label
        {
            Text = "Filter: ",
            VerticalAlignment = VAlignment.Center,
            MinSize = new Vector2(40, 1)
        };

        var searchBar = new LineEdit
        {
            PlaceHolder = Loc.GetString("round-end-summary-window-player-manifest-tab-search-placeholder"),
            HorizontalExpand = true,
            MinSize = new Vector2(200, 1)
        };

            //Round end text
            if (!string.IsNullOrEmpty(roundEnd))
            {
                var roundEndLabel = new RichTextLabel();
                roundEndLabel.SetMessage(FormattedMessage.FromMarkupOrThrow(roundEnd), tagsAllowed: null); // Goob
                roundEndSummaryContainer.AddChild(roundEndLabel);
            }

        searchContainer.AddChild(searchLabel);
        searchContainer.AddChild(searchBar);
        playerManifestTab.AddChild(searchContainer);

        // Header with sort buttons
        var headerContainer = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            Margin = new Thickness(10, 5, 10, 5)
        };

        var icNameButton = CreateSortButton("round-end-summary-window-player-manifest-tab-sort-character", SortField.ICName);
        var roleButton = CreateSortButton("round-end-summary-window-player-manifest-tab-sort-role", SortField.Role);
        var playerTypeButton = CreateSortButton("round-end-summary-window-player-manifest-tab-sort-player-type", SortField.PlayerType);
        var oocNameButton = CreateSortButton("round-end-summary-window-player-manifest-tab-sort-player", SortField.OOCName);

        playerTypeButton.SetSortIndicator(true);
        headerContainer.AddChild(icNameButton);

        // Add small spacer between buttons
        headerContainer.AddChild(new Control
        {
            MinSize = new Vector2(5, 1),
            HorizontalExpand = false
        });

        headerContainer.AddChild(roleButton);

        // Add small spacer between buttons
        headerContainer.AddChild(new Control
        {
            MinSize = new Vector2(5, 1),
            HorizontalExpand = false
        });

        headerContainer.AddChild(playerTypeButton);

        // Add small spacer between buttons
        headerContainer.AddChild(new Control
        {
            MinSize = new Vector2(5, 1),
            HorizontalExpand = false
        });

        headerContainer.AddChild(oocNameButton);

        playerManifestTab.AddChild(headerContainer);

        var scrollContainer = new ScrollContainer
        {
            VerticalExpand = true,
            Margin = new Thickness(10, 0, 10, 10),
        };

        _playerGrid = new GridContainer
        {
            Columns = 6, // Player Sprite,IC Name,Role,Player Type,OOC Name
            HorizontalExpand = true,
        };

        RefreshPlayerList();

        scrollContainer.AddChild(_playerGrid);
        playerManifestTab.AddChild(scrollContainer);

        return playerManifestTab;
    }

    private SortButton CreateSortButton(string text, SortField field)
    {
        var button = new SortButton(Loc.GetString(text), field);
        button.OnPressed += _ => SortBy(field);
        _sortButtons.Add(button);
        return button;
    }

    /// <summary>
    /// Handles sorting by the specified field, toggling direction if the same field is clicked
    /// </summary>
    private void SortBy(SortField field)
    {
        if (_currentSortField == field)
        {
            _sortDescending = !_sortDescending;
        }
        else
        {
            _currentSortField = field;
            _sortDescending = false;
        }

        #region Goob Station
        // Everything inside this region is heavily edited for goob.
        private BoxContainer MakePlayerManifestTab(RoundEndMessageEvent.RoundEndPlayerInfo[] playersInfo)
        {
            button.SetSortIndicator(button.Field == _currentSortField, _sortDescending);
        }

            // CorvaxGoob Start
            var searchBar = new LineEdit
            {
                PlaceHolder = Loc.GetString("round-end-summary-window-player-manifest-search"),
                HorizontalExpand = true,
                Margin = new Thickness(10, 6, 10, 2)
            };
            // CorvaxGoob End

            var playerInfoContainerScrollbox = new ScrollContainer
            {
                VerticalExpand = true,
                Margin = new Thickness(10)
            };
            var playerInfoContainer = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical
            };

    /// <summary>
    /// Refreshes the player list grid by clearing it and repopulating with sorted player data
    /// </summary>
    private void RefreshPlayerList()
    {
        _playerGrid.RemoveAllChildren();

            var playerEntries = new List<(PanelContainer, string)>(); // CorvaxGoob

            //Create labels for each player info.
            foreach (var playerInfo in sortedPlayersInfo)
            {
                var panel = new PanelContainer
                {
                    StyleClasses = { StyleNano.StyleClassBackgroundBaseDark },
                    Margin = new Thickness(0, 0, 0, 6)
                };

                var hBox = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    VerticalExpand = true
                };

                if (playerInfo.PlayerNetEntity != null)
                {
                    hBox.AddChild(new SpriteView(playerInfo.PlayerNetEntity.Value, _entityManager)
                    {
                        OverrideDirection = Direction.South,
                        VerticalAlignment = VAlignment.Center,
                        SetSize = new Vector2(64, 64),
                        VerticalExpand = true,
                        Stretch = SpriteView.StretchMode.Fill,
                        Margin = new Thickness(3, 0, 3, 0)
                    });
                }

                var textVBox = new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    VerticalExpand = true,
                    SeparationOverride = 2,
                };

                var playerTitleBox = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                };

                var playerInfoText = new RichTextLabel
                {
                    VerticalAlignment = VAlignment.Center,
                    VerticalExpand = true,
                };

                if (playerInfo.PlayerICName != null)
                {
                    var playerNameText = new Label
                    {
                        VerticalAlignment = VAlignment.Bottom,
                        StyleClasses = { StyleNano.StyleClassLabelHeading },
                        Margin = new Thickness(0, 0, 6, 0),
                        Text = playerInfo.PlayerICName
                    };
                    playerTitleBox.AddChild(playerNameText);

                    var role = Loc.GetString(playerInfo.Role);
                    var playerRoleText = new Label
                    {
                        VerticalAlignment = VAlignment.Bottom,
                        StyleClasses = { StyleNano.StyleClassLabelSubText },
                        Text = Loc.GetString("round-end-summary-window-player-name",
                            ("player", playerInfo.PlayerOOCName))
                    };

                    if (role != "Unknown")
                        playerRoleText.Text = Loc.GetString("round-end-summary-window-player-name-role",
                                ("role", role),
                                ("player", playerInfo.PlayerOOCName));

                    playerTitleBox.AddChild(playerRoleText);
                }

                textVBox.AddChild(playerTitleBox);

                if (!string.IsNullOrWhiteSpace(playerInfo.LastWords))
                {
                    var playerLastWordsText = new RichTextLabel
                    {
                        VerticalAlignment = VAlignment.Center,
                        VerticalExpand = true,
                    };

                    playerLastWordsText.SetMarkup(Loc.GetString("round-end-summary-window-last-words",
                        ("lastWords", playerInfo.LastWords)));

                    textVBox.AddChild(playerLastWordsText);
                }

                var hDeathBox = new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                };

                var deathLabel = new RichTextLabel
                {
                    VerticalAlignment = VAlignment.Center,
                    VerticalExpand = true,
                };

                textVBox.AddChild(deathLabel);

                if (playerInfo.EntMobState == MobState.Dead
                    && playerInfo.DamagePerGroup.Values.Any(v => v > 0))
                {
                    var totalDamage = playerInfo.DamagePerGroup.Values.Sum(static v => (decimal) v);
                    var severityAdj = totalDamage switch
                    {
                        >= 1000 => "catastrophic",
                        >= 750 => "devastating",
                        >= 500 => "agonizing",
                        >= 300 => "painful",
                        >= 200 => "brutal",
                        _ => "tragic"
                    };

                    var highestDamage = playerInfo.DamagePerGroup
                        .OrderByDescending(kvp => kvp.Value)
                        .First();
                    var typeAdj = highestDamage.Key switch
                    {
                        "Burn" => "fiery",
                        "Brute" => "crushing",
                        "Toxin" => "poisonous",
                        "Airloss" => "suffocating",
                        "Genetic" => "twisted",
                        "Metaphysical" => "otherworldly",
                        "Electronic" => "shocking",
                        _ => "mysterious",
                    };

                    deathLabel.SetMarkup(
                        Loc.GetString("round-end-summary-window-death",
                            ("severity", severityAdj),
                            ("type", typeAdj)));

                    var damageTable = new GridContainer
                    {
                        Columns = playerInfo.DamagePerGroup.Count,
                    };

                    foreach (var damage in playerInfo.DamagePerGroup)
                    {
                        if (damage.Value <= 0)
                            continue;

                        var color = damage.Key switch
                        {
                            "Burn" => Color.Orange,
                            "Brute" => Color.Red,
                            "Toxin" => Color.Green,
                            "Airloss" => Color.Blue,
                            "Genetic" => Color.Cyan,
                            "Metaphysical" => Color.Purple,
                            "Electronic" => Color.DarkOrange,
                            _ => Color.White,
                        };
                        var damagePanel = new PanelContainer
                        {
                            StyleClasses = { StyleNano.StyleClassBackgroundBaseLight },
                            Margin = new Thickness(2, 2, 2, 2)
                        };
                        var damageBox = new BoxContainer
                        {
                            Orientation = LayoutOrientation.Vertical,
                            Margin = new Thickness(1)
                        };
                        var valueLabel = new Label
                        {
                            Text = Math.Round((float) damage.Value).ToString(),
                            FontColorOverride = color,
                            HorizontalAlignment = HAlignment.Center,
                            VerticalAlignment = VAlignment.Center,
                        };
                        var headerLabel = new Label
                        {
                            Text = damage.Key,
                            FontColorOverride = Color.Gray,
                            HorizontalAlignment = HAlignment.Center,
                            VerticalAlignment = VAlignment.Center,
                        };
                        damagePanel.AddChild(damageBox);
                        damageBox.AddChild(valueLabel);
                        damageBox.AddChild(headerLabel);
                        damageTable.AddChild(damagePanel);
                    }

                    textVBox.AddChild(damageTable);
                }
                else if (playerInfo.EntMobState == MobState.Invalid)
                {
                    deathLabel.SetMarkup(Loc.GetString("round-end-summary-window-death-unknown"));
                }

                hBox.AddChild(textVBox);
                panel.AddChild(hBox);
                playerInfoContainer.AddChild(panel);

                // CorvaxGoob Start
                var searchText = $"{playerInfo.PlayerICName} {playerInfo.PlayerOOCName} {Loc.GetString(playerInfo.Role)}".ToLower();
                playerEntries.Add((panel, searchText));
                // CorvaxGoob End
            }

            // CorvaxGoob Start
            searchBar.OnTextChanged += args =>
            {
                var query = args.Text.ToLower();
                foreach (var (panel, searchText) in playerEntries)
                    panel.Visible = searchText.Contains(query);
            };
            // CorvaxGoob End

            playerInfoContainerScrollbox.AddChild(playerInfoContainer);
            playerManifestTab.AddChild(searchBar); // CorvaxGoob
            playerManifestTab.AddChild(playerInfoContainerScrollbox);

            return playerManifestTab;
        }
        private BoxContainer MakeStationReportTab()
        {
            //gets the stationreport varibible and sets the station report tab text to it if the map doesn't have a tablet will say No station report submitted
            var stationReportSystem = _entityManager.System<Content.Goobstation.Common.StationReport.StationReportSystem>();
            string stationReportText = stationReportSystem.StationReportText ?? Loc.GetString("no-station-report-summited");
            var stationReportTab = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical,
                Name = Loc.GetString("round-end-summary-window-station-report-tab-title")
            };
            var StationReportContainerScrollbox = new ScrollContainer
            {
                VerticalExpand = true,
                Margin = new Thickness(10),
                HScrollEnabled = false,
            };
            var StationReportContainer = new BoxContainer
            {
                Orientation = LayoutOrientation.Vertical
            };
            var StationReportLabel = new RichTextLabel();
            var StationReportmessage = new FormattedMessage();
            StationReportmessage.AddMarkupPermissive(stationReportText);
            StationReportLabel.SetMessage(StationReportmessage);
            StationReportContainer.AddChild(StationReportLabel);

    /// <summary>
    /// Adds a single player row to the grid with all columns (sprite, IC name, role, player type, OOC name)
    /// </summary>
    private void AddPlayerRow(RoundEndPlayerInfo playerInfo)
    {
        // Player Sprite column
        if (playerInfo.PlayerNetEntity != null)
        {
            _playerGrid.AddChild(new SpriteView(playerInfo.PlayerNetEntity.Value, _entityManager)
            {
                OverrideDirection = Direction.South,
                VerticalAlignment = VAlignment.Center,
                SetSize = new Vector2(32, 32),
            });
        }
        else
        {
            _playerGrid.AddChild(new Control
            {
                SetSize = new Vector2(32, 32),
            });
        }

        // IC Name column
        var icNameLabel = new Label
        {
            Text = playerInfo.PlayerICName ?? playerInfo.PlayerOOCName,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            ClipText = true
        };

        // Apply color coding for antagonists
        if (playerInfo.Antag)
        {
            icNameLabel.FontColorOverride = Color.Red;
        }

        _playerGrid.AddChild(icNameLabel);

        _playerGrid.AddChild(new Control
        {
            SetSize = new Vector2(32, 32),
        });

        // Role column
        var roleLabel = new Label
        {
            Text = playerInfo.Observer ? "-" : Loc.GetString(playerInfo.Role),
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            ClipText = true
        };
        _playerGrid.AddChild(roleLabel);

        // Player Type column
        var playerTypeLabel = new Label
        {
            Text = GetPlayerTypeText(playerInfo),
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            ClipText = true
        };

        // Apply color coding based on player type
        if (playerInfo.Antag)
        {
            playerTypeLabel.FontColorOverride = Color.Red;
        }
        else if (playerInfo.Observer)
        {
            playerTypeLabel.FontColorOverride = Color.Gray;
        }

        _playerGrid.AddChild(playerTypeLabel);

        // OOC Name column
        var oocNameLabel = new Label
        {
            Text = playerInfo.PlayerOOCName,
            VerticalAlignment = VAlignment.Center,
            HorizontalExpand = true,
            ClipText = true
        };

        _playerGrid.AddChild(oocNameLabel);
    }

    /// <summary>
    /// Gets the player type text for a player based on their observer and antagonist flags
    /// </summary>
    private static string GetPlayerTypeText(RoundEndPlayerInfo playerInfo)
    {
        if (playerInfo.Observer)
            return Loc.GetString("round-end-summary-window-player-manifest-tab-sort-player-type-observer");
        if (playerInfo.Antag)
            return Loc.GetString("round-end-summary-window-player-manifest-tab-sort-player-type-antag");

        return Loc.GetString("round-end-summary-window-player-manifest-tab-sort-player-type-crew");
    }

    private IEnumerable<RoundEndPlayerInfo> GetSortedPlayers()
    {
        // First filter players based on search text
        var filteredPlayers = string.IsNullOrEmpty(_searchText)
            ? _playersInfo
            : _playersInfo.Where(PlayerMatchesSearch);

        static string GetIcKey(RoundEndPlayerInfo p) =>
                (p.PlayerICName ?? p.PlayerOOCName).ToLowerInvariant();

        static string GetOocKey(RoundEndPlayerInfo p) =>
            p.PlayerOOCName.ToLowerInvariant();

        static string GetRoleKey(RoundEndPlayerInfo p) =>
            (p.Observer ? "zzz_observer" : p.Role).ToLowerInvariant();

        static int GetPlayerTypeSortKey(RoundEndPlayerInfo p) =>
            p.Antag ? 1 : p.Observer ? 3 : 2;

        return _currentSortField switch
        {
            SortField.ICName => ApplySort(filteredPlayers, GetIcKey, _sortDescending),
            SortField.OOCName => ApplySort(filteredPlayers, GetOocKey, _sortDescending),
            SortField.Role => ApplySort(filteredPlayers, GetRoleKey, _sortDescending),
            SortField.PlayerType => ApplySort(filteredPlayers, GetPlayerTypeSortKey, _sortDescending),
            _ => filteredPlayers
        };
    }

    private static IEnumerable<RoundEndPlayerInfo> ApplySort<TKey>(
        IEnumerable<RoundEndPlayerInfo> players,
        Func<RoundEndPlayerInfo, TKey> primaryKey,
        bool descending)
    {
        static string SecondaryKey(RoundEndPlayerInfo p) =>
            (p.PlayerICName ?? p.PlayerOOCName).ToLowerInvariant();

        return descending
            ? players.OrderByDescending(primaryKey).ThenByDescending(SecondaryKey)
            : players.OrderBy(primaryKey).ThenBy(SecondaryKey);
    }

    /// <summary>
    /// Gets a sort key for player type to ensure consistent ordering: Antagonist -> Crew -> Observer
    /// </summary>

    /// <summary>
    /// Checks if a player matches the current search filter
    /// </summary>
    private bool PlayerMatchesSearch(RoundEndPlayerInfo playerInfo)
    {
        if (string.IsNullOrEmpty(_searchText))
            return true;

        // Search in character name (IC name)
        if (!string.IsNullOrEmpty(playerInfo.PlayerICName) &&
            playerInfo.PlayerICName.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
            return true;

        // Search in player name (OOC name)
        if (!string.IsNullOrEmpty(playerInfo.PlayerOOCName) &&
            playerInfo.PlayerOOCName.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
            return true;

        // Search in role
        if (!string.IsNullOrEmpty(playerInfo.Role))
        {
            if (playerInfo.Role.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                Loc.GetString(playerInfo.Role).Contains(_searchText, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Search in player type
        var playerType = GetPlayerTypeText(playerInfo);
        if (playerType.Contains(_searchText, StringComparison.OrdinalIgnoreCase))
            return true;

        // Search for "Observer" when they are observers
        if (playerInfo.Observer && "observer".Contains(_searchText, StringComparison.OrdinalIgnoreCase))
            return true;

        return false;
    }

    /// <summary>
    /// Handles search text changes and refreshes the player list
    /// </summary>
    private void OnSearchTextChanged(LineEdit.LineEditEventArgs args)
    {
        _searchText = args.Text;
        RefreshPlayerList();
    }

    private sealed class SortButton : Button
    {
        public SortField Field { get; }
        private readonly Label _sortIndicator;

        public SortButton(string text, SortField field)
        {
            Field = field;
            HorizontalExpand = true;

            var container = new BoxContainer
            {
                Orientation = LayoutOrientation.Horizontal,
                HorizontalExpand = true
            };

            var label = new Label
            {
                Text = text,
                HorizontalExpand = true
            };

            _sortIndicator = new Label
            {
                Text = "",
                HorizontalAlignment = HAlignment.Right,
                MinSize = new Vector2(15, 1)
            };

            container.AddChild(label);
            container.AddChild(_sortIndicator);

            AddChild(container);
        }

        public void SetSortIndicator(bool active, bool descending = false)
        {
            if (!active)
            {
                _sortIndicator.Text = "";
                return;
            }

            _sortIndicator.Text = descending ? "▼" : "▲";
        }
    }
}
