// Licensed under IS14's EULA, see EULA.txt for more information.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Content.Client._IS14.OS;
using Content.Client._IS14.OS.Shell;
using Content.Server._IS14.OS;
using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.Prototypes;
using Robust.Client.GameObjects;
using Robust.Shared.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._IS14;

/// <summary>
///     The head-of-department consoles (Docs §12). Two things about them fail silently: an app
///     whose UI class nobody wrote opens as an empty window, and an icon path with a typo is
///     quietly swapped for <c>noSprite.png</c> by the engine rather than throwing.
/// </summary>
[TestFixture]
public sealed class OsConsoleAppsTest
{
    /// <summary>Consoles that are supposed to come up as a working desktop.</summary>
    private static readonly string[] Consoles =
    {
        "IS14OsHeadConsoleCommand",
        "IS14OsHeadConsoleEngineering",
        "IS14OsHeadConsoleCargo",
        "IS14OsDepartmentTerminal",
    };

    [Test]
    public async Task ConsolesInstallTheirSoftware()
    {
        await using var pair = await PoolManager.GetServerClient();
        var server = pair.Server;
        var entMan = server.EntMan;

        var faults = new List<string>();

        await server.WaitPost(() =>
        {
            var map = server.System<SharedMapSystem>();
            map.CreateMap(out var mapId);

            foreach (var protoId in Consoles)
            {
                var console = entMan.SpawnEntity(protoId, new MapCoordinates(0, 0, mapId));

                if (!entMan.TryGetComponent(console, out IS14OsMemoryComponent? memory))
                {
                    faults.Add($"{protoId}: no memory component, so it installed nothing");
                    continue;
                }

                var device = entMan.GetComponent<IS14OsDeviceComponent>(console);

                // Preinstalled software that did not fit, or was refused by device flags, would
                // leave a head staring at a desktop with none of their tools on it.
                foreach (var app in memory.Preinstalled)
                {
                    if (!memory.Installed.ContainsKey(app))
                        faults.Add($"{protoId}: {app.Id} is listed as preinstalled but did not install");
                }

                // Lidless machines have no session to open: they run on mains power, and are on
                // from map init. A console that boots to nothing is furniture.
                if (!device.Lidless)
                    faults.Add($"{protoId}: a bolted-down console must not have a lid");

                // A camera photographs what its owner is looking at, which a machine bolted to
                // the floor never is; the gallery is a view onto those photos and goes with it.
                // Both are undeletable system software, so only their device flags keep them off.
                foreach (var handheldOnly in new[] { "AppCamera", "AppGallery" })
                {
                    if (memory.Installed.ContainsKey(handheldOnly))
                        faults.Add($"{protoId}: {handheldOnly} has no business on a console");
                }

                entMan.DeleteEntity(console);
            }

            map.DeleteMap(mapId);
        });

        Assert.That(faults, Is.Empty);

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     Every app prototype has to have a client UI class, an icon that resolves, and a
    ///     control tree that actually builds. All three fail invisibly: a missing UI class opens
    ///     an empty window, a mistyped icon path is quietly swapped for <c>noSprite.png</c> by
    ///     the engine, and a XAML file whose root tag is not its own class compiles perfectly
    ///     and throws only when someone opens the app in a round.
    /// </summary>
    [Test]
    public async Task EveryAppHasUiAndIcon()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
        });
        var client = pair.Client;
        var proto = client.ResolveDependency<IPrototypeManager>();

        var noUi = new List<string>();
        var noIcon = new List<string>();
        var broken = new List<string>();

        await client.WaitPost(() =>
        {
            var sprites = client.System<SpriteSystem>();

            foreach (var app in proto.EnumeratePrototypes<IS14OsAppPrototype>())
            {
                var ui = IS14OsAppUiRegistry.Create(app.ID);

                if (ui == null)
                {
                    noUi.Add(app.ID);
                }
                else
                {
                    // App UIs build their control tree lazily, so reading Root is what actually
                    // loads the XAML. This is the only place the whole catalogue gets built.
                    try
                    {
                        if (ui.Root == null!)
                            broken.Add($"{app.ID}: built no control at all");
                    }
                    catch (Exception e)
                    {
                        broken.Add($"{app.ID}: {e.GetType().Name}: {e.Message}");
                    }
                }

                if (IS14OsStyle.Resolve(sprites, app.Icon ?? IS14OsStyle.Fallback) == null)
                    noIcon.Add(app.ID);
            }
        });

        Assert.Multiple(() =>
        {
            Assert.That(noUi, Is.Empty, "apps with no client UI class");
            Assert.That(noIcon, Is.Empty, "apps whose icon did not resolve to a texture");
            Assert.That(broken, Is.Empty, "apps whose control tree would not build");
        });

        await pair.CleanReturnAsync();
    }
}
