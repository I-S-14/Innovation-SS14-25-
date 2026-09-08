// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.Serialization;

namespace Content.Shared._IS14.OS.UI.Apps;

/// <summary>
///     "Open my messenger on this conversation" — sent by the reply button on the chat
///     notification, which is the one place the OS is reached from outside its own window.
///
///     It names a device because a player can carry more than one. The server treats that name
///     as a claim and checks it: the notification is not a licence to open somebody else's PDA.
/// </summary>
[Serializable, NetSerializable]
public sealed class OsMessengerOpenRequest : EntityEventArgs
{
    public NetEntity Device;

    /// <summary>Network address of the conversation to bring up.</summary>
    public string Address;

    public OsMessengerOpenRequest(NetEntity device, string address)
    {
        Device = device;
        Address = address;
    }
}
