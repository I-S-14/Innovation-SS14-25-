using Content.Goobstation.Shared.RoundIntro;

namespace Content.Client.GameTicking;

public sealed partial class ClientGameTicker
{
    public string? InGameInfoBlob => ServerInfoBlob;
    public event Action? InGameInfoBlobUpdated
    {
        add => InfoBlobUpdated += value;
        remove => InfoBlobUpdated -= value;
    }

    private bool GoobJoinGameAttempt()
    {
        var ev = new ClientJoinGameAttemptEvent();
        RaiseLocalEvent(ref ev);
        return ev.Handled;
    }
}
