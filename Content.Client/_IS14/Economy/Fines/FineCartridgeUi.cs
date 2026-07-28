using Content.Client.UserInterface.Fragments;
using Content.Shared._IS14.Economy.Fines;
using Content.Shared.CartridgeLoader;
using Robust.Client.UserInterface;

namespace Content.Client._IS14.Economy.Fines;

public sealed partial class FineCartridgeUi : UIFragment
{
    private FineCartridgeUiFragment? _fragment;

    public override Control GetUIFragmentRoot()
    {
        return _fragment!;
    }

    public override void Setup(BoundUserInterface userInterface, EntityUid? fragmentOwner)
    {
        _fragment = new FineCartridgeUiFragment();

        _fragment.OnIssue += (record, preset, amount) => Send(userInterface,
            new FineCartridgeUiMessageEvent(FineCartridgeAction.Issue, record, preset, amount, 0));

        _fragment.OnVoid += fineId => Send(userInterface,
            new FineCartridgeUiMessageEvent(FineCartridgeAction.Void, 0, string.Empty, 0, fineId));
    }

    public override void UpdateState(BoundUserInterfaceState state)
    {
        if (state is FineCartridgeUiState fineState)
            _fragment?.UpdateState(fineState);
    }

    private static void Send(BoundUserInterface userInterface, FineCartridgeUiMessageEvent message)
    {
        userInterface.SendMessage(new CartridgeUiMessage(message));
    }
}
