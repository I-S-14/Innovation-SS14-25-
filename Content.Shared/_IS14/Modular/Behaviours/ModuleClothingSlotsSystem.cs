// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Lets a module change where the chassis may be worn.
/// </summary>
public sealed class ModuleClothingSlotsSystem : ModuleBehaviourSystem<ModuleClothingSlotsComponent>
{
    [Dependency] private readonly ClothingSystem _clothing = default!;

    protected override void Start(Entity<ModuleClothingSlotsComponent> ent, EntityUid chassis)
    {
        if (ent.Comp.Previous != null || !TryComp<ClothingComponent>(chassis, out var clothing))
            return;

        ent.Comp.Previous = clothing.Slots;
        _clothing.SetSlots(chassis, clothing.Slots | ent.Comp.Slots, clothing);
    }

    protected override void Stop(Entity<ModuleClothingSlotsComponent> ent, EntityUid chassis)
    {
        if (ent.Comp.Previous is not { } previous)
            return;

        ent.Comp.Previous = null;

        if (!TerminatingOrDeleted(chassis) && TryComp<ClothingComponent>(chassis, out var clothing))
            _clothing.SetSlots(chassis, previous, clothing);
    }
}
