using Content.Shared.DeviceNetwork;

namespace Content.Shared.Fax;

public partial record struct FaxEntityPayload : INetworkPayload
{
    [DataField]
    public EntityUid Entity;
    [DataField]
    public bool CrossGrid;
}
