// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Console;

namespace Content.Client._IS14.Research.Editor;

/// <summary>Opens the research tree editor. Client-side: it edits files, not the round.</summary>
public sealed class IS14TreeEditorCommand : IConsoleCommand
{
    public string Command => "is14treeeditor";

    public string Description => "Открывает редактор дерева исследований НИЦ.";

    public string Help => "is14treeeditor";

    private IS14TreeEditorWindow? _window;

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (_window is { Disposed: false })
        {
            _window.Open();
            _window.MoveToFront();
            return;
        }

        _window = new IS14TreeEditorWindow();
        _window.OpenCentered();
    }
}
