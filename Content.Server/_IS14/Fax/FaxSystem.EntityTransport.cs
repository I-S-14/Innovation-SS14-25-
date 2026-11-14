using Content.Shared.DeviceNetwork.Events;
using Content.Shared.Fax;
using Content.Shared.Fax.Components;

namespace Content.Server.Fax;

public sealed partial class FaxSystem
{
    [Dependency] private SharedTransformSystem _faxTransform = default!;
    [Dependency] private Robust.Shared.Containers.SharedContainerSystem _faxContainer = default!;

    [SubscribeLocalEvent]
    private void OnEntityPayload(Entity<FaxMachineComponent> ent, ref DeviceNetworkPacketEvent<FaxEntityPayload> args)
    {
        var received = args.Data.Entity;
        if (TerminatingOrDeleted(received)
            || !TryComp<FaxMachineComponent>(args.Sender.Owner, out var sender)
            || sender.PaperSlot.Item != received
            || !args.Data.CrossGrid && _faxTransform.GetGrid(ent.Owner) != _faxTransform.GetGrid(received))
            return;

        _faxTransform.SetCoordinates(received, Transform(ent).Coordinates);
        _faxContainer.AttachParentToContainerOrGrid((received, Transform(received)));
        Receive(ent.Owner, null, args.SenderAddress);
    }
}
