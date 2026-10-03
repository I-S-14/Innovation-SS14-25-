// SPDX-License-Identifier: AGPL-3.0-or-later

using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;
using Content.Shared.Paper;
using Content.Shared._IS14.Paper; //IS14-change: stamp placement
using Robust.Client.Player; //IS14-change: signature placement
using static Content.Shared.Paper.PaperComponent;

namespace Content.Client.Paper.UI;

[UsedImplicitly]
public sealed class PaperBoundUserInterface : BoundUserInterface
{
    [ViewVariables]
    private PaperWindow? _window;

    //IS14-change: needed to tell whether a signature offer is aimed at this client
    private readonly IPlayerManager _player = IoCManager.Resolve<IPlayerManager>();

    public PaperBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<PaperWindow>();
        _window.OnSaved += InputOnTextEntered;
        _window.OnSignatureRequested += OnSignatureRequested; // Starlight-edit
        _window.StampPlacement.OnStampPlaced += OnStampPlaced; //IS14-change: stamp placement
        _window.StampPlacement.OnSignaturePlaced += OnSignaturePlaced; //IS14-change: signature placement
        _window.StampPlacement.OnSignatureCancelled += OnSignatureCancelled; //IS14-change

        if (EntMan.TryGetComponent<PaperComponent>(Owner, out var paper))
        {
            _window.MaxInputLength = paper.ContentSize;
        }
        if (EntMan.TryGetComponent<PaperVisualsComponent>(Owner, out var visuals))
        {
            _window.InitVisuals(Owner, visuals);
        }
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        var paperState = (PaperBoundUserInterfaceState) state;
        _window?.Populate(paperState);

        //IS14-change start: the server offers a signature to whoever used the sign verb
        if (_window == null)
            return;

        var offered = paperState.SignatureRequestedBy is { } requester
                      && EntMan.GetEntity(requester) == _player.LocalEntity
                      && paperState.SignatureRequestedName is { } name
            ? new StampDisplayInfo
            {
                StampedName = name,
                StampedColor = StampPlacementSystem.SignatureColor,
            }
            : (StampDisplayInfo?) null;

        _window.StampPlacement.SetSignature(offered);
        //IS14-change end
    }

    private void InputOnTextEntered(string text)
    {
        SendMessage(new PaperInputTextMessage(text));

        if (_window != null)
        {
            _window.Input.TextRope = Rope.Leaf.Empty;
            _window.Input.CursorPosition = new TextEdit.CursorPos(0, TextEdit.LineBreakBias.Top);
        }
    }

    // Starlight
    private void OnSignatureRequested(int signatureIndex) => SendMessage(new PaperSignatureRequestMessage(signatureIndex));

    //IS14-change start: the player dropped a held stamp or their signature onto a spot on the page
    private void OnStampPlaced(System.Numerics.Vector2 position, float rotation)
        => SendMessage(new PaperPlaceStampMessage(position, rotation));

    private void OnSignaturePlaced(System.Numerics.Vector2 position, float rotation)
        => SendMessage(new PaperPlaceSignatureMessage(position, rotation));

    private void OnSignatureCancelled() => SendMessage(new PaperCancelSignatureMessage());
    //IS14-change end
}
