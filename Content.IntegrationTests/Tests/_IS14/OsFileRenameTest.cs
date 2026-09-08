// Licensed under IS14's EULA, see EULA.txt for more information.

#nullable enable

using System.Linq;
using Content.IntegrationTests.Pair;
using Content.Server._IS14.OS;
using Content.Server.GameTicking;
using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.Files;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.UI.Apps;
using Content.Shared.Inventory;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

/// <summary>
///     Renaming a file in the Explorer. The server half has been sitting there unused until the
///     app grew a button for it, so this is the first thing that ever exercises it.
/// </summary>
[TestFixture]
public sealed class OsFileRenameTest
{
    [Test]
    public async Task FilesCanBeRenamed()
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
        var entMan = server.EntMan;
        var inv = server.System<InventorySystem>();
        var files = server.System<IS14OsFileSystem>();

        var renamed = string.Empty;
        var clamped = string.Empty;
        var kept = string.Empty;

        await server.WaitPost(() =>
        {
            Assert.That(inv.TryGetSlotEntity(player, "id", out var slot), "no PDA in the id slot");
            var pda = slot!.Value;

            var device = entMan.GetComponent<IS14OsDeviceComponent>(pda);
            var memory = entMan.GetComponent<IS14OsMemoryComponent>(pda);

            var file = files.TryAdd((pda, device, memory), "Заметка", OsFileKind.Text, 1, null, "text");
            Assert.That(file, Is.Not.Null, "the device had no room for a one unit file");

            Rename(entMan, pda, file!.Id, "  Отчёт по атмосу  ");
            renamed = file.Name;

            Rename(entMan, pda, file.Id, new string('и', 80));
            clamped = file.Name;

            // A blank name would leave a nameless row nobody can tell apart.
            Rename(entMan, pda, file.Id, "   ");
            kept = file.Name;
        });

        Assert.Multiple(() =>
        {
            Assert.That(renamed, Is.EqualTo("Отчёт по атмосу"), "the new name was not trimmed");
            Assert.That(clamped, Has.Length.EqualTo(32), "an overlong name was not clamped");
            Assert.That(kept, Has.Length.EqualTo(32), "a blank name overwrote the old one");
        });

        await pair.CleanReturnAsync();
    }

    private static void Rename(IEntityManager entMan, EntityUid device, int file, string name)
    {
        var ev = new OsAppEventRaised("AppFiles", new OsFileRenameEvent(file, name), device);
        entMan.EventBus.RaiseLocalEvent(device, ref ev);
    }
}
