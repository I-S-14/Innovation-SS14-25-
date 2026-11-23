// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Examine;
using Content.Shared.Interaction;
using Content.Shared.Doors.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Xenoborgs.Components;

namespace Content.Shared._IS14.Cyborgs.Systems;

/// <summary>Uses the eye's position for the core's existing silicon remote controls.</summary>
public sealed class MothershipRemoteInteractionSystem : EntitySystem
{
    [Dependency] private readonly ExamineSystemShared _examine = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<MothershipEyeComponent, InRangeOverrideEvent>(OnInRange,
            after: new[] { typeof(CyborgRemoteControlSystem) });
    }

    private void OnInRange(Entity<MothershipEyeComponent> ent, ref InRangeOverrideEvent args)
    {
        if (ent.Comp.Eye is not { } eye || !Exists(eye))
            return;

        args.Handled = true;
        args.InRange = (HasComp<DoorComponent>(args.Target) || HasComp<XenoborgComponent>(args.Target) ||
                       TryComp<StationAiWhitelistComponent>(args.Target, out var whitelist) && whitelist.Enabled) &&
                      _examine.InRangeUnOccluded(eye, args.Target, ent.Comp.ViewRange);
    }
}
