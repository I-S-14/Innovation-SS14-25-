// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Linq;
using Content.Goobstation.Shared.GPS;
using Content.IntegrationTests.Tests.Interaction;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared._IS14.Cyborgs.Systems;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

/// <summary>
///     Cyborg tools used by a player-controlled cyborg, for what needs a session like opening a UI.
/// </summary>
public sealed class CyborgInteractionTest : InteractionTest
{
    protected override string PlayerPrototype => "IS14CyborgNTMiner";

    /// <summary>
    ///     Using the built-in GPS in a tool slot opens its screen.
    /// </summary>
    [Test]
    public async Task GpsScreenOpensOnUse()
    {
        EntityUid gps = default;
        await Server.WaitPost(() =>
        {
            var cyborgSys = SEntMan.System<SharedCyborgSystem>();
            var cyborg = SEntMan.GetComponent<CyborgComponent>(SPlayer);
            cyborgSys.SetActive((SPlayer, cyborg), true);

            gps = cyborg.ToolContainer.ContainedEntities.Single(e =>
                SEntMan.GetComponent<MetaDataComponent>(e).EntityPrototype?.ID == "IS14CyborgToolGPS");
            var tool = SEntMan.GetComponent<CyborgToolComponent>(gps);
            Assert.That(cyborgSys.TryDeployTool((SPlayer, cyborg), (gps, tool)));
            Assert.That(HandSys.IsHolding(SPlayer, gps, out var hand));
            HandSys.SetActiveHand(SPlayer, hand!);
        });

        await RunTicks(5);
        await Server.WaitPost(() => InteractSys.UseInHandInteraction(SPlayer, gps));
        await RunTicks(5);

        Assert.That(IsUiOpen(GpsUiKey.Key), "the GPS screen should open on use");
    }
}
