using Content.Server._CorvaxGoob.Skills;
using Content.Shared.Antag;
using Content.Shared.Inventory;

namespace Content.Server.Antag;

public sealed partial class ServerAntagSelectionSystem
{
    [Dependency] private readonly InventorySystem _compatInventory = default!;
    [Dependency] private readonly SkillsSystem _compatSkills = default!;

    protected override void BeforeAntagEquip(EntityUid antag, AntagSpecifierPrototype prototype)
    {
        if (!prototype.UnequipOldGear || !_compatInventory.TryGetSlots(antag, out var slots))
            return;

        foreach (var slot in slots)
            _compatInventory.TryUnequip(antag, slot.Name, true, true);
    }

    protected override void AfterAntagEquip(EntityUid antag, AntagSpecifierPrototype prototype)
    {
        foreach (var special in prototype.Special)
            special.AfterEquip(antag);

        if (prototype.Skills.Count > 0)
            _compatSkills.GrantSkill(antag, prototype.Skills);
    }
}
