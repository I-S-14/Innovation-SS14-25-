// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.MedicalScanner;
using Content.Shared._Shitmed.Targeting; // Shitmed Change
using Content.Shared._Shitmed.Medical.HealthAnalyzer; // Shitmed Change
using Content.Shared.Medical.HealthAnalyzer;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.HealthAnalyzer.UI;

[UsedImplicitly]
public sealed partial class HealthAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    [ViewVariables]
    private HealthAnalyzerWindow? _window;

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<HealthAnalyzerWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
    }

    /// <summary>
    /// This will update the UI to reflect the newest health changes of the scanned entity.
    /// This gets called in the <see cref="HealthAnalyzerSystem"/> by SetUIState().
    /// </summary>
    protected override void UpdateState(BoundUserInterfaceState state)
    {
        if (_window == null || state is not HealthAnalyzerUiState cast)
            return;

            _window = this.CreateWindow<HealthAnalyzerWindow>();
            _window.OnBodyPartSelected += SendBodyPartMessage; // Shitmed Change
            _window.OnModeChanged += SendModeMessage;
            _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        }


        protected override void ReceiveMessage(BoundUserInterfaceMessage message)
        {
            if (_window == null)
                return;

            switch (message)
            {
                case HealthAnalyzerBodyMessage bodyMessage:
                    _window.Populate(bodyMessage);
                    break;
                case HealthAnalyzerOrgansMessage organsMessage:
                    _window.Populate(organsMessage);
                    break;
                case HealthAnalyzerChemicalsMessage chemicalsMessage:
                    _window.Populate(chemicalsMessage);
                    break;
            }
        }

        // Shitmed Change Start
        private void SendBodyPartMessage(TargetBodyPart? part, EntityUid target) => SendMessage(new HealthAnalyzerPartMessage(EntMan.GetNetEntity(target), part ?? null));

        private void SendModeMessage(HealthAnalyzerMode mode, EntityUid target) => SendMessage(new HealthAnalyzerModeSelectedMessage(EntMan.GetNetEntity(target), mode));
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
                return;

            if (_window != null)
                _window.OnBodyPartSelected -= SendBodyPartMessage;

            _window?.Dispose();
        }

        // Shitmed Change End
    }
}