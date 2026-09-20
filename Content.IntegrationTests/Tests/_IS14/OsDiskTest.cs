// Licensed under IS14's EULA, see EULA.txt for more information.

#nullable enable

using Content.Server._IS14.OS;
using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.Files;
using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._IS14;

/// <summary>
///     Removable media (Docs/_IS14/os-design.md §7.4). The point of a disk is that it can be
///     carried off, so the rules about what may come off one matter: a disk must not be a way
///     to put software somewhere it was never meant to run, and a pressed disk must not be a
///     writable one with extra steps.
///
///     Every refusal has to say why. A disk operation that silently does nothing is
///     indistinguishable from a broken button, which is exactly how the console drive being
///     missing went unnoticed.
/// </summary>
[TestFixture]
public sealed class OsDiskTest
{
    private const string Pda = "IS14_PdaCaptain";
    private const string Console = "IS14OsHeadConsoleCommand";

    [Test]
    public async Task DisksInstallOnlyWhatTheyMayAndSayWhyNot()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var disks = server.System<IS14OsDiskSystem>();
        var slots = server.System<ItemSlotsSystem>();

        await server.WaitPost(() =>
        {
            var map = server.System<SharedMapSystem>();
            map.CreateMap(out var mapId);
            var where = new MapCoordinates(0, 0, mapId);

            var pda = entMan.SpawnEntity(Pda, where);
            var disk = entMan.SpawnEntity("IS14OsDiskCommand", where);

            Assert.That(slots.TryInsert(pda, IS14OsDiskComponent.SlotId, disk, null),
                "the disk would not go into the drive");

            Assert.That(disks.GetDisk(pda)?.Owner, Is.EqualTo(disk), "the drive did not report its disk");

            var pdaEnt = new Entity<IS14OsDeviceComponent, IS14OsMemoryComponent>(
                pda,
                entMan.GetComponent<IS14OsDeviceComponent>(pda),
                entMan.GetComponent<IS14OsMemoryComponent>(pda));

            var diskEnt = new Entity<IS14OsDiskComponent>(disk, entMan.GetComponent<IS14OsDiskComponent>(disk));

            Assert.Multiple(() =>
            {
                // The command disk carries console software. A PDA is not a console, and a disk
                // is not a way around that — but the player has to be told, not just refused.
                Assert.That(disks.Install(pdaEnt, diskEnt, "AppTreasury"),
                    Is.EqualTo("is14-os-disk-error-device"),
                    "stationary software installed onto a handheld off a disk, or refused silently");

                // Nor is it a way to install something that is not on it.
                Assert.That(disks.Install(pdaEnt, diskEnt, "AppCalculator"),
                    Is.EqualTo("is14-os-disk-error-missing"));

                // Pressed disks are pressings.
                Assert.That(disks.Delete(diskEnt, 1), Is.EqualTo("is14-os-disk-error-readonly"));

                // The device says up front what it could never run, so the button can be greyed
                // out rather than pressed and refused.
                Assert.That(disks.CanRun(pdaEnt.Comp1, "AppTreasury"), Is.False);
                Assert.That(disks.CanRun(pdaEnt.Comp1, "AppNotes"), Is.True);
            });

            entMan.DeleteEntity(pda);
            map.DeleteMap(mapId);
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A console is the one machine the command disk exists for, so it has to have somewhere
    ///     to put one. The first version of the consoles had no drive at all, which made the
    ///     install button on that disk do nothing anywhere in the game.
    /// </summary>
    [Test]
    public async Task ConsolesHaveADriveAndTakeTheCommandDisk()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var disks = server.System<IS14OsDiskSystem>();
        var memorySystem = server.System<IS14OsMemorySystem>();
        var slots = server.System<ItemSlotsSystem>();

        await server.WaitPost(() =>
        {
            var map = server.System<SharedMapSystem>();
            map.CreateMap(out var mapId);
            var where = new MapCoordinates(0, 0, mapId);

            var console = entMan.SpawnEntity(Console, where);
            var disk = entMan.SpawnEntity("IS14OsDiskCommand", where);

            Assert.That(slots.TryInsert(console, IS14OsDiskComponent.SlotId, disk, null),
                "the console has no disk drive");

            var consoleEnt = new Entity<IS14OsDeviceComponent, IS14OsMemoryComponent>(
                console,
                entMan.GetComponent<IS14OsDeviceComponent>(console),
                entMan.GetComponent<IS14OsMemoryComponent>(console));

            var diskEnt = new Entity<IS14OsDiskComponent>(disk, entMan.GetComponent<IS14OsDiskComponent>(disk));

            // The captain's console ships with all three, so make room for the disk to do
            // something by taking one back off.
            Assert.That(memorySystem.Uninstall(consoleEnt, "AppTreasury"), Is.True);

            Assert.That(disks.Install(consoleEnt, diskEnt, "AppTreasury"), Is.Null,
                "the command disk would not install onto a console");

            // And a second attempt says so rather than pretending to work.
            Assert.That(disks.Install(consoleEnt, diskEnt, "AppTreasury"),
                Is.EqualTo("is14-os-disk-error-installed"));

            entMan.DeleteEntity(console);
            map.DeleteMap(mapId);
        });

        await pair.CleanReturnAsync();
    }

    [Test]
    public async Task BlankDisksTakeFilesUntilTheyAreFull()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var disks = server.System<IS14OsDiskSystem>();
        var files = server.System<IS14OsFileSystem>();
        var slots = server.System<ItemSlotsSystem>();

        await server.WaitPost(() =>
        {
            var map = server.System<SharedMapSystem>();
            map.CreateMap(out var mapId);
            var where = new MapCoordinates(0, 0, mapId);

            var pda = entMan.SpawnEntity(Pda, where);
            var disk = entMan.SpawnEntity("IS14OsDiskBlank", where);

            Assert.That(slots.TryInsert(pda, IS14OsDiskComponent.SlotId, disk, null));

            var device = entMan.GetComponent<IS14OsDeviceComponent>(pda);
            var memory = entMan.GetComponent<IS14OsMemoryComponent>(pda);
            var diskComp = entMan.GetComponent<IS14OsDiskComponent>(disk);

            var pdaEnt = new Entity<IS14OsDeviceComponent, IS14OsMemoryComponent>(pda, device, memory);
            var diskEnt = new Entity<IS14OsDiskComponent>(disk, diskComp);

            var file = files.TryAdd(pdaEnt, "смена", OsFileKind.Text, 2, text: "проверить сингулярность");
            Assert.That(file, Is.Not.Null);

            Assert.That(disks.CopyToDisk(memory, diskEnt, file!.Id), Is.Null, "a blank disk refused a file");
            Assert.Multiple(() =>
            {
                Assert.That(diskComp.Files, Has.Count.EqualTo(1));
                Assert.That(diskComp.UsedMemory, Is.EqualTo(2), "the disk did not charge for the file");

                // Copying, not moving: whoever hands the disk over keeps their own copy.
                Assert.That(memory.Files, Has.Count.EqualTo(1), "copying to disk took the original away");
            });

            // The copy comes back as a second, separate file on the device.
            Assert.That(disks.CopyToDevice(pdaEnt, diskEnt, diskComp.Files[0].Id), Is.Null);
            Assert.That(memory.Files, Has.Count.EqualTo(2));

            // Capacity is real: a disk that quietly swallowed everything would not be a
            // constraint, and the whole platform is built on storage being one.
            diskComp.UsedMemory = diskComp.Capacity;
            Assert.That(disks.CopyToDisk(memory, diskEnt, file.Id),
                Is.EqualTo("is14-os-disk-error-disk-full"),
                "a full disk took another file");

            entMan.DeleteEntity(pda);
            map.DeleteMap(mapId);
        });

        await pair.CleanReturnAsync();
    }
}
