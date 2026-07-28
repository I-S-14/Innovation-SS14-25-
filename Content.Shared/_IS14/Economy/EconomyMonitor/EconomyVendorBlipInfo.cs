using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Economy.EconomyMonitor;

/// <summary>
/// Info about a trackable economic device (vending machine) sent to the console UI.
/// </summary>
[Serializable, NetSerializable]
public sealed class EconomyVendorBlipInfo
{
    public NetEntity Entity;
    public string Name;
    public NetCoordinates Coordinates;
    /// <summary>EntityPrototype ID used on the client to render the machine's own sprite as a map icon.</summary>
    public string? PrototypeId;

    public EconomyVendorBlipInfo(NetEntity entity, string name, NetCoordinates coordinates, string? prototypeId = null)
    {
        Entity = entity;
        Name = name;
        Coordinates = coordinates;
        PrototypeId = prototypeId;
    }
}
