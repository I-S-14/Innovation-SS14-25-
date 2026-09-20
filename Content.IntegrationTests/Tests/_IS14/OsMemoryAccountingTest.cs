// Licensed under IS14's EULA, see EULA.txt for more information.

#nullable enable

using Content.Server._IS14.OS;
using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.Files;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;

namespace Content.IntegrationTests.Tests._IS14;

/// <summary>
///     Memory is the platform's only real constraint (Docs/_IS14/os-design.md §7), so the one
///     number the whole design rests on is <c>UsedMemory</c>. It is now made of three parts —
///     applications, their data and stored files — and three places nudging one counter is
///     exactly how that counter ends up wrong by the end of a shift.
/// </summary>
[TestFixture]
public sealed class OsMemoryAccountingTest
{
    private const string DeviceProto = "IS14_PdaCaptain";

    [Test]
    public async Task DataAndFilesAreBothChargedAndBothRefunded()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var memorySystem = server.System<IS14OsMemorySystem>();
        var fileSystem = server.System<IS14OsFileSystem>();

        await server.WaitPost(() =>
        {
            var map = server.System<SharedMapSystem>();
            map.CreateMap(out var mapId);
            var device = entMan.SpawnEntity(DeviceProto, new MapCoordinates(0, 0, mapId));

            var deviceComp = entMan.GetComponent<IS14OsDeviceComponent>(device);
            var memory = entMan.GetComponent<IS14OsMemoryComponent>(device);
            var ent = new Entity<IS14OsDeviceComponent, IS14OsMemoryComponent>(device, deviceComp, memory);

            var baseline = memory.UsedMemory;
            var free = memorySystem.GetFreeMemory(ent);

            // A file is charged like an app is: a full gallery really does cost you a program.
            var file = fileSystem.TryAdd(ent, "photo", OsFileKind.Photo, 3);
            Assert.That(file, Is.Not.Null, "the device had no room for a 3 GQ file");
            Assert.Multiple(() =>
            {
                Assert.That(memory.UsedFileMemory, Is.EqualTo(3));
                Assert.That(memory.UsedMemory, Is.EqualTo(baseline + 3), "the file was not added to the total");
                Assert.That(memorySystem.GetFreeMemory(ent), Is.EqualTo(free - 3));
            });

            // App data is charged separately, so the readout can say which is which.
            var granted = memorySystem.SetDataUsage(ent, "AppMessenger", 2);
            Assert.Multiple(() =>
            {
                Assert.That(granted, Is.EqualTo(2), "the messenger was refused room it had");
                Assert.That(memory.UsedDataMemory, Is.EqualTo(2));
                Assert.That(memory.UsedMemory, Is.EqualTo(baseline + 5), "app data missed the total");
            });

            // Asking for more than the app's DataCap is capped rather than refused: the caller
            // is expected to drop its oldest data and try again, never to hit a dead end.
            var capped = memorySystem.SetDataUsage(ent, "AppMessenger", 9999);
            Assert.That(capped, Is.LessThan(9999), "DataCap did not limit the app's data");
            Assert.That(memory.UsedMemory, Is.EqualTo(baseline + memory.UsedDataMemory + 3));

            // And it all comes back.
            memorySystem.SetDataUsage(ent, "AppMessenger", 0);
            fileSystem.Remove(memory, file!.Id);

            Assert.Multiple(() =>
            {
                Assert.That(memory.UsedFileMemory, Is.Zero);
                Assert.That(memory.UsedDataMemory, Is.Zero);
                Assert.That(memory.UsedMemory, Is.EqualTo(baseline), "memory was not given back");
                Assert.That(memorySystem.GetFreeMemory(ent), Is.EqualTo(free));
            });

            entMan.DeleteEntity(device);
            map.DeleteMap(mapId);
        });

        await pair.CleanReturnAsync();
    }
}
