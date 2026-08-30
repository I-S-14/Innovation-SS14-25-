// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Diagnostics.CodeAnalysis;
using Content.Client.UserInterface.Systems.Hands.Controls;

namespace Content.Client.UserInterface.Systems.Hands;

/// <summary>
///     IS14 additions to the hands bar. The controller owns its buttons and does not hand
///     them out; content that wants to mark one up — a badge on the slot holding a MOD
///     device, say — needs a way in that does not mean reparenting somebody else's UI.
/// </summary>
public sealed partial class HandsUIController
{
    /// <summary>
    ///     The button showing a given hand of the local player, if it is on screen.
    /// </summary>
    public bool TryGetHandButton(string handName, [NotNullWhen(true)] out HandButton? button)
    {
        return _handLookup.TryGetValue(handName, out button);
    }
}
