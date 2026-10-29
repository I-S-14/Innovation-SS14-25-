// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.AlertLevel;
using Content.Shared.Communications;
using Content.Goobstation.Shared.VoiceChat; // Goobstation - Voice chat
using Content.Shared.Station.Systems;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Client.Communications.UI;

/// <summary>
/// The BUI for the communications console.
/// Handles sending messages back to the server to call the shuttle,
/// send messages, set the alert level, and set the text on screens.
/// </summary>
/// <seealso cref="CommunicationsConsoleComponent"/>
public sealed partial class CommunicationsConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private StationSystem _station = default!;
    [Dependency] private AlertLevelSystem _alertLevel = default!;

    [Dependency] private EntityQuery<AlertLevelComponent> _alertLevelQuery;

    [ViewVariables]
    private CommunicationsConsoleMenu? _menu;

    private static readonly EntProtoId FallbackScreen = "Screen";
    private static readonly ProtoId<AlertLevelPrototype> UnknownAlertLevel = "Unknown";

    /// <inheritdoc/>
    protected override void Open()
    {
        base.Open();

        _menu = this.CreateWindow<CommunicationsConsoleMenu>();
        _menu.OnAnnounce += AnnounceButtonPressed;
        _menu.OnBroadcast += BroadcastButtonPressed;
        _menu.OnAlertLevel += AlertLevelSelected;
        _menu.OnEmergencyLevel += EmergencyShuttleButtonPressed;
        _menu.Console = Owner; // Goobstation - Voice chat
        _menu.OnVoiceBroadcast += () => SendMessage(new VoiceBroadcastToggleMessage()); // Goobstation - Voice chat
    }

    public void AlertLevelSelected(ProtoId<AlertLevelPrototype> level)
    {
        // TODO: This does not work until the console UI is predicted and uses component states.
        // Also someone decided to send BUI states regularly in an update loop, so this just gets randomly bulldozed until the message reaches the server.
        // _menu.CurrentAlertLevel = level;
        // _menu.AlertLevelSelectable = false;
        // _menu.AlertLevelButton.Disabled = true;
        SendMessage(new CommunicationsConsoleSelectAlertLevelMessage(level));
    }

    public void RadioAnnounceButtonPressed(string message)
    {
        var maxLength = _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength);
        var msg = SharedChatSystem.SanitizeAnnouncement(message, maxLength);
        SendMessage(new CommunicationsConsoleAnnounceMessage(msg));
    }

    public void ScreenBroadcastButtonPressed(string message)
    {
        SendMessage(new CommunicationsConsoleBroadcastMessage(message));
    }

    public void CallShuttle()
    {
        SendMessage(new CommunicationsConsoleCallEmergencyShuttleMessage());
    }

    public void RecallShuttle()
    {
        SendMessage(new CommunicationsConsoleRecallEmergencyShuttleMessage());
    }

    // TODO: Use component states and update in an AfterAutoHandleState subscription
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_menu == null)
            return;

        if (state is not CommunicationsConsoleInterfaceState commsState)
            return;

        var stationUid = _station.GetOwningStation(Owner);

        ProtoId<AlertLevelPrototype> currentAlertLevel;
        List<ProtoId<AlertLevelPrototype>> selectableAlertLevels;
        bool canChangeAlertLevel;
        if (_alertLevelQuery.TryGetComponent(stationUid, out var alertComp))
        {
            currentAlertLevel = alertComp.CurrentAlertLevel;
            selectableAlertLevels = _alertLevel.GetSelectableAlertLevels((stationUid.Value, alertComp));
            canChangeAlertLevel = _alertLevel.CanChangeAlertLevel((stationUid.Value, alertComp));
        }
        else
        {
            currentAlertLevel = UnknownAlertLevel;
            selectableAlertLevels = new() { UnknownAlertLevel };
            canChangeAlertLevel = false;
        }

        _menu.UpdateState(commsState, currentAlertLevel, selectableAlertLevels, canChangeAlertLevel);
    }
}
