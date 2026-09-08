// Licensed under IS14's EULA, see EULA.txt for more information.

#nullable enable

using System.Collections.Generic;
using System.Numerics;
using Content.Client._IS14.Controls;
using Content.Client._IS14.OS.Shell;
using Content.IntegrationTests.Pair;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.IntegrationTests.Tests._IS14;

/// <summary>
///     The window's close and minimise buttons. A headless test cannot look at pixels, but it
///     can rule out every way a mark ends up invisible on screen: no texture, no size, or a
///     tint that is not there. The first version of these buttons drew nothing at all.
/// </summary>
[TestFixture]
public sealed class OsWindowChromeTest
{
    [Test]
    public async Task ChromeButtonsShowTheirMark()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var uiMan = client.ResolveDependency<IUserInterfaceManager>();

        var faults = new List<string>();

        await client.WaitPost(() =>
        {
            foreach (var mark in new[] { GlyphButton.Mark.Cross, GlyphButton.Mark.Bar })
            {
                var button = new GlyphButton { Glyph = mark };

                uiMan.RootControl.AddChild(button);

                button.Measure(new Vector2(64, 64));
                button.Arrange(new UIBox2(0, 0, button.DesiredSize.X, button.DesiredSize.Y));

                var found = Find<TextureRect>(button);

                if (found == null)
                {
                    faults.Add($"{mark}: no mark in the button at all");
                }
                else
                {
                    if (found.Texture == null)
                        faults.Add($"{mark}: the mark has no texture");

                    if (found.Size.X < 10 || found.Size.Y < 2)
                        faults.Add($"{mark}: the mark was laid out at {found.Size}");

                    // Tinting is how the mark takes the theme's colour; a transparent or unset
                    // tint is the difference between a visible glyph and nothing.
                    var tint = found.ModulateSelfOverride;
                    if (tint is not { A: > 0.5f })
                        faults.Add($"{mark}: the mark is tinted {tint?.ToString() ?? "nothing"}");
                }

                if (button.Size.X < 22 || button.Size.Y < 18)
                    faults.Add($"{mark}: the button itself is only {button.Size}");

                uiMan.RootControl.RemoveChild(button);
            }
        });

        Assert.That(faults, Is.Empty);

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     The shell's own glyphs, including the launcher mark drawn for IS14. A mistyped state
    ///     name or a malformed RSI only shows up when the taskbar tries to draw itself.
    /// </summary>
    [Test]
    public async Task ShellIconsResolve()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
        });
        var client = pair.Client;

        var missing = new List<string>();

        await client.WaitPost(() =>
        {
            var sprites = client.System<SpriteSystem>();

            var icons = new (string Name, SpriteSpecifier Sprite)[]
            {
                (nameof(IS14OsStyle.Apps), IS14OsStyle.Apps),
                (nameof(IS14OsStyle.Settings), IS14OsStyle.Settings),
                (nameof(IS14OsStyle.Photo), IS14OsStyle.Photo),
            };

            foreach (var (name, sprite) in icons)
            {
                if (IS14OsStyle.Resolve(sprites, sprite) == null)
                    missing.Add(name);
            }
        });

        Assert.That(missing, Is.Empty, "shell icons that did not resolve to a texture");

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     A taskbar tab clips the app's name to keep every tab the same width. Clipped labels
    ///     measure as zero wide in the engine, so this checks the name is still given room —
    ///     the same trap that once emptied every value column in the OS.
    /// </summary>
    [Test]
    public async Task ClippedCaptionsKeepTheirWidth()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var uiMan = client.ResolveDependency<IUserInterfaceManager>();

        var width = -1f;

        await client.WaitPost(() =>
        {
            var tile = new IconTile
            {
                Compact = true,
                ClipCaption = true,
                Caption = "Мессенджер",
                MinWidth = 96,
            };

            uiMan.RootControl.AddChild(tile);

            tile.Measure(new Vector2(200, 40));
            tile.Arrange(new UIBox2(0, 0, 96, 24));

            width = Find<Label>(tile)?.Size.X ?? -1f;

            uiMan.RootControl.RemoveChild(tile);
        });

        Assert.That(width, Is.GreaterThan(0f), "the clipped caption was laid out at zero width");

        await pair.CleanReturnAsync();
    }

    private static T? Find<T>(Control control) where T : Control
    {
        if (control is T match)
            return match;

        foreach (var child in control.Children)
        {
            if (Find<T>(child) is { } found)
                return found;
        }

        return null;
    }
}
