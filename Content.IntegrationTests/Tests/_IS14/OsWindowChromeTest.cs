// Licensed under IS14's EULA, see EULA.txt for more information.

#nullable enable

using System.Collections.Generic;
using System.Numerics;
using Content.Client._IS14.Controls;
using Content.IntegrationTests.Pair;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Maths;

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

                var found = Find(button);

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

    private static TextureRect? Find(Control control)
    {
        if (control is TextureRect rect)
            return rect;

        foreach (var child in control.Children)
        {
            if (Find(child) is { } found)
                return found;
        }

        return null;
    }
}
