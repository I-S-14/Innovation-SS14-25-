// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modular;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Modular;

public sealed class ModularChassisBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private ModularChassisWindow? _window;

    public ModularChassisBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey) { }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ModularChassisWindow>();
        _window.SetChassis(Owner);

        _window.OnToggleActive += () => SendMessage(new ChassisToggleActiveMessage());
        _window.OnToggleDeploy += () => SendMessage(new ChassisToggleDeployMessage());
        _window.OnSelectModule += module => SendMessage(new ChassisSelectModuleMessage(module));
        _window.OnEjectModule += module => SendMessage(new ChassisEjectModuleMessage(module));
        _window.OnTogglePart += part => SendMessage(new ChassisTogglePartMessage(part));
        _window.OnSealPart += part => SendMessage(new ChassisSealPartMessage(part));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is ModularChassisUiState chassisState)
            _window?.UpdateState(chassisState);
    }
}
