using Content.Shared._CorvaxGoob.Botany.PlantAnalyzer;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._CorvaxGoob.Botany.PlantAnalyzer;

[UsedImplicitly]
public sealed class CorvaxPlantAnalyzerBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private CorvaxPlantAnalyzerWindow? _window;

    public CorvaxPlantAnalyzerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();
        _window = this.CreateWindow<CorvaxPlantAnalyzerWindow>();
        _window.Title = EntMan.GetComponent<MetaDataComponent>(Owner).EntityName;
        _window.Print.OnPressed += _ => Print();
    }

    protected override void ReceiveMessage(BoundUserInterfaceMessage message)
    {
        if (_window == null)
            return;

        if (message is not CorvaxPlantAnalyzerScannedUserMessage cast)
            return;

        _window.Populate(cast);
    }

    private void Print()
    {
        SendMessage(new CorvaxPlantAnalyzerPrintMessage());
        if (_window != null)
            _window.Print.Disabled = true;
    }
}
