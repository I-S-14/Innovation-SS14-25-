using Content.Shared.Cargo;

namespace Content.Server.Cargo.Systems;

public sealed partial class CargoSystem
{
    private EntityUid PackageCargoProduct(CargoOrderData order, EntityUid product)
    {
        if (order.Container is not { } packaging)
            return product;

        var transform = Transform(product);
        var parcel = Spawn(packaging.Entity, transform.Coordinates);
        _transformSystem.SetLocalRotation(parcel, transform.LocalRotation);
        if (_container.TryGetContainer(parcel, packaging.ContainerId, out var container)
            && _container.Insert(product, container, force: true))
            return parcel;

        // Keep the purchased product if the prototype's packaging is invalid.
        _sawmill.Error($"Cannot package cargo product {order.ProductId} in {packaging.Entity}/{packaging.ContainerId}.");
        QueueDel(parcel);
        return product;
    }
}
