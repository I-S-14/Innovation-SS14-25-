// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Modsuit.Components;
using Content.Shared.Clothing.EntitySystems;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Modsuit.Systems;

[Serializable, NetSerializable]
public enum ModsuitVisuals : byte
{
    /// <summary>True while a part is pressure-sealed.</summary>
    Sealed,
}

[Serializable, NetSerializable]
public enum ModsuitVisualLayers : byte
{
    Base,
}

/// <summary>
///     Keeps the suit looking like what it is: sealed parts swap to their armoured
///     sprite, both as items and on the wearer.
/// </summary>
public sealed class ModsuitVisualsSystem : EntitySystem
{
    [Dependency] private readonly ClothingSystem _clothing = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    /// <summary>
    ///     <c>ClientClothingSystem</c> builds worn states as "{prefix}-equipped-{slot}",
    ///     so the sealed art has to be reached through a prefix rather than a suffix.
    /// </summary>
    private const string SealedPrefix = "sealed";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ModsuitPartComponent, ModsuitPartSealedEvent>(OnPartSealed);
    }

    private void OnPartSealed(Entity<ModsuitPartComponent> ent, ref ModsuitPartSealedEvent args)
    {
        SetSealedLook(ent, args.Sealed);
    }

    /// <summary>
    ///     The control unit is not a part, but it has a sealed look of its own.
    ///     Driven from the suit system's existing chassis-state handler, because the
    ///     engine allows only one handler per (component, event) pair.
    /// </summary>
    public void SetSealedLook(EntityUid uid, bool sealedUp)
    {
        _clothing.SetEquippedPrefix(uid, sealedUp ? SealedPrefix : null);
        _appearance.SetData(uid, ModsuitVisuals.Sealed, sealedUp);
    }
}
