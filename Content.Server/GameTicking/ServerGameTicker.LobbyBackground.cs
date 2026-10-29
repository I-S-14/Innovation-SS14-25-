// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.GameTicking.Prototypes;
using Robust.Shared.Random;
using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Server.GameTicking;

public sealed partial class ServerGameTicker
{
    [ViewVariables]
    public ProtoId<LobbyBackgroundPrototype>? LobbyBackground { get; private set; }

    [ViewVariables]
    private List<ProtoId<LobbyBackgroundPrototype>> _lobbyBackgrounds = [];

    private static readonly string[] WhitelistedBackgroundExtensions = new string[] {"png", "jpg", "jpeg", "webp"};

    private void InitializeLobbyBackground()
    {
        var allprotos = ProtoMan.EnumeratePrototypes<LobbyBackgroundPrototype>().ToList();
        _lobbyBackgrounds ??= new List<ProtoId<LobbyBackgroundPrototype>>();

        //create protoids from them
        foreach (var proto in allprotos)
        {
            if (!WhitelistedBackgroundExtensions.Contains(prototype.Background.Extension))
            {
                _sawmill.Warning($"Lobby background '{prototype.ID}' has an invalid extension '{prototype.Background.Extension}' and will be ignored.");
                continue;
            }

            _lobbyBackgrounds.Add(prototype.ID);
        }

        RandomizeLobbyBackground();
    }

    private void RandomizeLobbyBackground()
    {
        if (_lobbyBackgrounds != null && _lobbyBackgrounds.Count != 0)
            LobbyBackground = Random.Pick(_lobbyBackgrounds);
        else
            LobbyBackground = null;
    }
}
