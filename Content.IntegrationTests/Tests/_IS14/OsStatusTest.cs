// Licensed under IS14's EULA, see EULA.txt for more information.

#nullable enable

using System.Numerics;
using Content.Client._IS14.Controls;
using Content.IntegrationTests.Pair;
using Content.Server.GameTicking;
using Content.Shared._IS14.OS.UI;
using Content.Shared.Inventory;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.GameObjects;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._IS14;

/// <summary>
///     The Status app renders straight from the shell state, so this checks the shell state
///     a spawned crewman's own PDA actually pushes: owner, job and station must be there.
/// </summary>
[TestFixture]
public sealed class OsStatusTest
{
    [Test]
    public async Task ShellStatusIsFilled()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            DummyTicker = false,
            Connected = true,
            InLobby = true,
        });

        var server = pair.Server;
        var ticker = server.System<GameTicker>();

        await server.WaitPost(() =>
        {
            ticker.ToggleReadyAll(true);
            ticker.StartRound();
        });
        await pair.RunTicksSync(10);

        var player = pair.Player!.AttachedEntity!.Value;
        var inv = server.System<InventorySystem>();
        var ui = server.System<SharedUserInterfaceSystem>();

        EntityUid pda = default;
        await server.WaitPost(() =>
        {
            Assert.That(inv.TryGetSlotEntity(player, "id", out var slot), "no PDA in the id slot");
            pda = slot!.Value;
            ui.TryOpenUi(pda, IS14OsUiKey.Key, player);
        });

        await pair.RunTicksSync(10);

        IS14OsUiState? state = null;
        await server.WaitPost(() =>
        {
            ui.TryGetUiState<IS14OsUiState>(pda, IS14OsUiKey.Key, out state);
        });

        Assert.That(state, Is.Not.Null, "no OS state was pushed at all");

        var shell = state!.Shell;
        TestContext.Out.WriteLine($"device='{shell.DeviceName}' owner='{shell.OwnerName}' " +
                                  $"idName='{shell.IdName}' idJob='{shell.IdJob}' " +
                                  $"station='{shell.StationName}' alert='{shell.AlertLevel}' " +
                                  $"address='{shell.Address}'");

        Assert.Multiple(() =>
        {
            Assert.That(shell.OwnerName, Is.Not.Null, "OwnerName");
            Assert.That(shell.IdName, Is.Not.Null, "IdName");
            Assert.That(shell.IdJob, Is.Not.Null, "IdJob");
            Assert.That(shell.StationName, Is.Not.Null, "StationName");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A StatRow must actually give its value some width. The engine's Label measures as
    ///     zero wide whenever ClipText is on, so a value that does not claim the row's leftover
    ///     space is laid out at zero pixels and the readout renders as captions only.
    /// </summary>
    [Test]
    public async Task StatRowGivesTheValueWidth()
    {
        await using var pair = await PoolManager.GetServerClient();

        var client = pair.Client;
        var uiMan = client.ResolveDependency<IUserInterfaceManager>();

        var width = -1f;

        await client.WaitPost(() =>
        {
            var row = new StatRow
            {
                Caption = "Владелец",
                Value = "Алисия Томас",
            };

            uiMan.RootControl.AddChild(row);

            row.Measure(new Vector2(340, 400));
            row.Arrange(new UIBox2(0, 0, 340, row.DesiredSize.Y));

            width = FindLabel(row, "Алисия Томас")?.Size.X ?? -1f;

            uiMan.RootControl.RemoveChild(row);
        });

        Assert.That(width, Is.GreaterThan(0f), "the value label was laid out at zero width");

        await pair.CleanReturnAsync();
    }

    private static Label? FindLabel(Control control, string text)
    {
        if (control is Label label && label.Text == text)
            return label;

        foreach (var child in control.Children)
        {
            if (FindLabel(child, text) is { } found)
                return found;
        }

        return null;
    }
}
