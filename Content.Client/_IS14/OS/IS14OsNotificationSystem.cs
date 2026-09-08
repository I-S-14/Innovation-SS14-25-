// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.OS.UI.Apps;

namespace Content.Client._IS14.OS;

/// <summary>
///     The client half of the chat notifications the OS puts in the chat box. A markup tag is
///     not an entity system and cannot raise network events itself, so the reply button asks
///     this to do it.
/// </summary>
public sealed class IS14OsNotificationSystem : EntitySystem
{
    /// <summary>Ask the server to bring up this device's messenger on one conversation.</summary>
    public void RequestOpenChat(NetEntity device, string address)
    {
        RaiseNetworkEvent(new OsMessengerOpenRequest(device, address));
    }
}
