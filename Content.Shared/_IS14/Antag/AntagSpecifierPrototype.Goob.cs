using Content.Shared._CorvaxGoob.Skills;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Shared.Antag;

public sealed partial class AntagSpecifierPrototype
{
    [DataField] public HashSet<Skills> Skills = [];
    [DataField] public List<JobSpecial> Special = [];
    [DataField] public bool UnequipOldGear;
    [DataField] public List<ProtoId<AntagPrototype>> FallbackRoles = [];
    // Null retains Wizards' component-level setting for new definitions.
    [DataField] public bool? LateJoinAdditional;
}
