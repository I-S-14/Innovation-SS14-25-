// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.StatusIcon;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client.Access.UI;

/// <summary>
/// Initializes a <see cref="AgentIDCardWindow"/> and updates it when new server messages are received.
/// </summary>
public sealed class AgentIDCardBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private AgentIDCardWindow? _window;

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent(Owner, out AgentIDCardComponent? agent))
            return;

        _window = this.CreateWindow<AgentIDCardWindow>();

        _window.OnNameChanged += OnNameChanged;
        _window.OnJobChanged += OnJobChanged;
        _window.OnJobIconChanged += OnJobIconChanged;
        _window.OnNumberChanged += OnNumberChanged; // DeltaV
    }

    // DeltaV - Add number change handler
    private void OnNumberChanged(uint newNumber)
    {
        SendMessage(new AgentIDCardNumberChangedMessage(newNumber));
    }

    public override void Update()
    {
        base.Update();

        if (_window == null)
            return;

        if (!EntMan.TryGetComponent<IdCardComponent>(Owner, out var card))
            return;

        _window.Update(card);
    }

    private void OnNameChanged(string newName)
    {
        SendPredictedMessage(new AgentIDCardNameChangedMessage(newName));
    }

    private void OnJobChanged(string newJob)
    {
        SendPredictedMessage(new AgentIDCardJobChangedMessage(newJob));
    }

    private void OnJobIconChanged(ProtoId<JobIconPrototype> newJobIconId)
    {
        base.UpdateState(state);
        if (_window == null || state is not AgentIDCardBoundUserInterfaceState cast)
            return;

        _window.SetCurrentName(cast.CurrentName);
        _window.SetCurrentJob(cast.CurrentJob);
        _window.SetAllowedIcons(cast.CurrentJobIconId);
        _window.SetCurrentNumber(cast.CurrentNumber); // DeltaV
    }
}
