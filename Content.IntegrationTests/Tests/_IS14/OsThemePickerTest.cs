// Licensed under IS14's EULA, see EULA.txt for more information.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Content.Client._IS14.Controls;
using Robust.Client.Input;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.IntegrationTests.Tests._IS14;

/// <summary>
///     The theme picker in Settings. A dropdown hides its options behind a click, so unlike the
///     strip it replaced, nothing about it is visible until something opens it — which makes it
///     exactly the kind of control that can rot unnoticed.
/// </summary>
[TestFixture]
public sealed class OsThemePickerTest
{
    private static readonly List<ChoiceStripItem> Themes = new()
    {
        new ChoiceStripItem("ThemeNt", "Нанотрейзен", Swatch: Color.FromHex("#3B7DD8")),
        new ChoiceStripItem("ThemeAmber", "Янтарь", Swatch: Color.FromHex("#D8A13B")),
        new ChoiceStripItem("ThemeSyndie", "Синдикат", Swatch: Color.FromHex("#D83B3B")),
    };

    [Test]
    public async Task PickingAThemeFromTheListReportsIt()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;
        var uiMan = client.ResolveDependency<IUserInterfaceManager>();

        DropDown drop = default!;
        var picked = new List<string>();

        await client.WaitPost(() =>
        {
            drop = new DropDown { Placeholder = "—" };
            drop.OnItemSelected += id => picked.Add(id);
            drop.SetItems(Themes, "ThemeNt");

            drop.MinSize = new Vector2(160, 24);
            uiMan.RootControl.AddChild(drop);
        });

        await pair.RunTicksSync(3);

        // The closed button has to say which theme is on, or the setting is invisible.
        var face = FindAll<Label>(drop).FirstOrDefault();
        Assert.That(face?.Text, Is.EqualTo("Нанотрейзен"), "the closed dropdown does not name the current theme");

        await Click(pair, drop);
        await pair.RunTicksSync(3);

        // The modal root is shared with the rest of the client's popups, so ours is the one
        // holding the theme rows.
        var popup = FindAll<Popup>(uiMan.ModalRoot)
            .FirstOrDefault(p => FindAll<IconTile>(p).Count == Themes.Count);

        Assert.That(drop.IsOpen, "pressing the dropdown did not open the list");
        Assert.That(popup, Is.Not.Null, "the open list is not in the modal root");
        Assert.That(popup!.Visible, Is.True, "the list is in the tree but not visible");

        var rows = FindAll<IconTile>(popup);

        Assert.Multiple(() =>
        {
            Assert.That(rows, Has.Count.EqualTo(Themes.Count), "the open list does not hold every theme");
            Assert.That(rows.Select(r => r.Caption), Is.EqualTo(Themes.Select(t => t.Caption)).AsCollection);

            // Losing the colour block is the whole cost of folding the strip into a dropdown,
            // so it has to survive into the list.
            Assert.That(rows.All(r => r.Swatch != null), "a theme in the list lost its colour block");
            Assert.That(rows[0].Selected, "the current theme is not marked in the list");
            Assert.That(popup.Size.X, Is.GreaterThanOrEqualTo(drop.Size.X), "the list is narrower than its button");
        });

        await Click(pair, rows[2]);
        await pair.RunTicksSync(3);

        Assert.Multiple(() =>
        {
            Assert.That(picked, Is.EqualTo(new[] { "ThemeSyndie" }).AsCollection, "the wrong theme was reported");
            Assert.That(drop.IsOpen, Is.False, "the list stayed open after a pick");
        });

        // The server owns the theme, so the picker is told what it ended up as; it must not
        // just latch whatever was clicked.
        await client.WaitPost(() => drop.SetItems(Themes, "ThemeAmber"));
        await pair.RunTicksSync(1);

        Assert.That(FindAll<Label>(drop).FirstOrDefault()?.Text, Is.EqualTo("Янтарь"),
            "the dropdown ignored the selection pushed to it");

        await client.WaitPost(() => uiMan.RootControl.RemoveChild(drop));
        await pair.CleanReturnAsync();
    }

    private static async Task Click(Pair.TestPair pair, Control control)
    {
        var coords = new ScreenCoordinates(
            control.GlobalPixelPosition + control.PixelSize / 2,
            control.Window?.Id ?? default);

        var relative = coords.Position / control.UIScale - control.GlobalPosition;
        var relativePixel = coords.Position - control.GlobalPixelPosition;

        foreach (var state in new[] { BoundKeyState.Down, BoundKeyState.Up })
        {
            await pair.Client.DoGuiEvent(control, new GUIBoundKeyEventArgs(
                EngineKeyFunctions.UIClick,
                state,
                coords,
                default,
                relative,
                relativePixel));

            await pair.RunTicksSync(1);
        }
    }

    private static List<T> FindAll<T>(Control control) where T : Control
    {
        var found = new List<T>();

        void Walk(Control c)
        {
            if (c is T match)
                found.Add(match);

            foreach (var child in c.Children)
                Walk(child);
        }

        Walk(control);
        return found;
    }
}
