// Licensed under IS14's EULA, see EULA.txt for more information.

#nullable enable

using System;
using System.Collections.Generic;
using Content.Client._IS14.Economy.EconomyMonitor;
using Content.Client._IS14.Economy.Gosplan;
using Content.Client._IS14.Economy.Payroll;
using Content.Client._IS14.Economy.Treasury;
using Robust.Client.UserInterface;

namespace Content.IntegrationTests.Tests._IS14;

/// <summary>
///     The standalone economy consoles, now that each is a thin window around the panel the OS
///     application also uses (Docs/_IS14/os-design.md §12.2).
///
///     Both ways these break are invisible to the compiler: a XAML root tag that is not the
///     class itself loads fine and throws when the window is opened, and a texture path naming
///     a raw PNG inside an .rsi logs an error and silently draws the fallback. Neither window
///     was ever constructed in a test before, which is how both went unnoticed.
/// </summary>
[TestFixture]
public sealed class EconomyConsoleWindowTest
{
    [Test]
    public async Task ConsoleWindowsBuild()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
        });
        var client = pair.Client;
        var uiMan = client.ResolveDependency<IUserInterfaceManager>();

        var broken = new List<string>();

        await client.WaitPost(() =>
        {
            var builders = new (string Name, Func<Control> Build)[]
            {
                (nameof(PayrollConsoleWindow), () => new PayrollConsoleWindow()),
                (nameof(TreasuryConsoleWindow), () => new TreasuryConsoleWindow()),
                (nameof(GosplanConsoleWindow), () => new GosplanConsoleWindow()),
                (nameof(EconomyMonitorWindow), () => new EconomyMonitorWindow()),
            };

            foreach (var (name, build) in builders)
            {
                try
                {
                    var window = build();

                    // Adding it to the tree is what makes the engine actually lay the thing out;
                    // a control that only survives construction has not been proven to work.
                    uiMan.RootControl.AddChild(window);
                    uiMan.RootControl.RemoveChild(window);
                }
                catch (Exception e)
                {
                    broken.Add($"{name}: {e.GetType().Name}: {e.Message}");
                }
            }
        });

        Assert.That(broken, Is.Empty, "console windows that would not build");

        await pair.CleanReturnAsync();
    }
}
