using Content.Shared.Interaction;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Network;
using Robust.Shared.Network.Messages;
using Robust.Shared.Prototypes;

namespace Content.Shared._IS.CardSticker;


public sealed class CardStickerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly EntityManager _entityManager = default!;

    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CardStickerTargetComponent, InteractUsingEvent>(OnStickerUsed);
    }

    private void OnStickerUsed(Entity<CardStickerTargetComponent> ent, ref InteractUsingEvent args)
    {
        if (!TryComp<CardStickerComponent>(args.Used, out var sticker))
            return;

        if (!_prototypeManager.TryIndex(sticker.CardStickerProto, out var stickerPrototype))
            return;

        args.Handled = true;
        AttachStickerOnCard(ent, stickerPrototype);

        if (_net.IsServer)
            _entityManager.DeleteEntity(args.Used);
    }


    public void AttachStickerOnCard(Entity<CardStickerTargetComponent> ent, CardStickerPrototype sticker)
    {
        ent.Comp.Current = sticker.ID;
        Dirty(ent);
    }
}

// ReSharper disable once InconsistentNaming
public enum IdCardVisualLayers : byte
{
    Base,
    Sticker,
}
