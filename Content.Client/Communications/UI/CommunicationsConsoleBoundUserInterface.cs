using System.Linq;
using Content.Shared.AlertLevel;
using Content.Shared.Station.Systems;
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Communications;
using Content.Goobstation.Shared.VoiceChat; // Goobstation - Voice chat
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;

namespace Content.Client.Communications.UI
{
    public sealed class CommunicationsConsoleBoundUserInterface : BoundUserInterface
    {
        [Dependency] private readonly IConfigurationManager _cfg = default!;
        [Dependency] private readonly StationSystem _station = default!;
        [Dependency] private readonly AlertLevelSystem _alerts = default!;

        [ViewVariables]
        private CommunicationsConsoleMenu? _menu;

        public CommunicationsConsoleBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

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

        public void AlertLevelSelected(string level)
        {
            if (_menu!.AlertLevelSelectable)
            {
                _menu.CurrentLevel = level;
                SendMessage(new CommunicationsConsoleSelectAlertLevelMessage(level));
            }
        }

        public void EmergencyShuttleButtonPressed()
        {
            if (_menu!.CountdownStarted)
                RecallShuttle();
            else
                CallShuttle();
        }

        public void AnnounceButtonPressed(string message)
        {
            var maxLength = _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength);
            var msg = SharedChatSystem.SanitizeAnnouncement(message, maxLength);
            SendMessage(new CommunicationsConsoleAnnounceMessage(msg));
        }

        public void BroadcastButtonPressed(string message)
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

        protected override void UpdateState(BoundUserInterfaceState state)
        {
            base.UpdateState(state);

            if (state is not CommunicationsConsoleInterfaceState commsState)
                return;

            if (_menu != null)
            {
                _menu.CanAnnounce = commsState.CanAnnounce;
                _menu.CanBroadcast = commsState.CanBroadcast;
                _menu.CanCall = commsState.CanCall;
                _menu.CountdownStarted = commsState.CountdownStarted;
                _menu.CountdownEnd = commsState.ExpectedCountdownEnd;
                var station = _station.GetOwningStation(Owner);
                if (station is { } stationUid && EntMan.TryGetComponent<AlertLevelComponent>(stationUid, out var alert))
                {
                    _menu.AlertLevelSelectable = _alerts.CanChangeAlertLevel((stationUid, alert));
                    _menu.CurrentLevel = alert.CurrentAlertLevel.Id;
                    var levels = _alerts.GetSelectableAlertLevels((stationUid, alert)).Select(level => level.Id).ToList();
                    _menu.UpdateAlertLevels(levels, _menu.CurrentLevel);
                }
                else
                {
                    _menu.AlertLevelSelectable = false;
                    _menu.CurrentLevel = "Unknown";
                    _menu.UpdateAlertLevels(null, _menu.CurrentLevel);
                }
                _menu.UpdateCountdown();
                _menu.AlertLevelButton.Disabled = !_menu.AlertLevelSelectable;
                _menu.EmergencyShuttleButton.Disabled = !_menu.CanCall;
                _menu.AnnounceButton.Disabled = !_menu.CanAnnounce;
                _menu.BroadcastButton.Disabled = !_menu.CanBroadcast;
            }
        }
    }
}