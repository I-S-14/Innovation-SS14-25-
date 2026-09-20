// Licensed under IS14's EULA, see EULA.txt for more information.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Content.Client._IS14.OS.Apps;
using Content.IntegrationTests.Pair;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.IntegrationTests.Tests._IS14;

/// <summary>
///     The document editor keeping what was typed. The server drops app events for an app that
///     is no longer open, so a document that has not been written back by the time the window
///     goes is simply gone — which makes this the one part of the editor that cannot be left to
///     the writer remembering a button.
/// </summary>
[TestFixture]
public sealed class OsNotesAutoSaveTest
{
    [Test]
    public async Task DocumentIsKeptWithoutBeingAskedTo()
    {
        // The fragment resolves SpriteSystem for its icons, and a client only has entity
        // systems once it is connected.
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
        });
        var client = pair.Client;
        var uiMan = client.ResolveDependency<IUserInterfaceManager>();

        NotesAppFragment fragment = default!;
        TextEdit edit = default!;
        var saves = new List<string>();

        await client.WaitPost(() =>
        {
            fragment = new NotesAppFragment();
            fragment.OnSave += (text, _) => saves.Add(text);

            uiMan.RootControl.AddChild(fragment);

            edit = FindAll<TextEdit>(fragment).First();
            edit.InsertAtCursor("отчёт по атмосу");
        });

        // Closing the window is the last moment anything can be sent, so the flush has to
        // happen there and not a frame later.
        await client.WaitPost(() => fragment.Flush());

        Assert.That(saves, Is.EqualTo(new[] { "отчёт по атмосу" }).AsCollection,
            "closing the editor did not write the document back");

        // A second flush with nothing changed must not send anything: the device would be
        // taking a message per close for no reason.
        await client.WaitPost(() => fragment.Flush());

        Assert.That(saves, Has.Count.EqualTo(1), "an unchanged document was saved again");

        // ...and typing more makes it dirty again.
        await client.WaitPost(() =>
        {
            edit.InsertAtCursor(" готов");
            fragment.Flush();
        });

        Assert.That(saves, Has.Count.EqualTo(2), "an edited document was not saved on close");
        Assert.That(saves[^1], Does.Contain("готов"), "the wrong text was saved");

        await client.WaitPost(() => uiMan.RootControl.RemoveChild(fragment));
        await pair.CleanReturnAsync();
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
