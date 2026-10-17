// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Doors.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Silicons.StationAi;

namespace Content.Shared._IS14.Cyborgs.Systems;

/// <summary>
/// tgstation's silicon remote control for cyborgs: with an empty manipulator, a cyborg works doors
/// and AI-controllable machines it can see, the way the AI does, instead of having to drive up to them.
/// </summary>
public sealed class CyborgRemoteControlSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedInteractionSystem _interaction = default!;

    /// <summary>
    /// How far a cyborg reaches remotely, about one screen like tg's view range.
    /// </summary>
    public const float RemoteRange = 7f;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgComponent, InRangeOverrideEvent>(OnInRange);
    }

    private void OnInRange(Entity<CyborgComponent> ent, ref InRangeOverrideEvent args)
    {
        if (!CanRemoteControl(ent, args.Target))
            return;

        var target = Transform(args.Target);
        args.Handled = true;
        args.InRange = _interaction.InRangeUnobstructed(ent.Owner, args.Target, target.Coordinates,
            target.LocalRotation, RemoteRange);
    }

    /// <summary>
    /// Whether the cyborg may work this target from a distance at all: an active cyborg with an empty
    /// manipulator, and a door or something the AI could control.
    /// </summary>
    public bool CanRemoteControl(Entity<CyborgComponent> cyborg, EntityUid target)
    {
        // Other cyborgs carry the AI whitelist too, but are not something to work from across the room.
        if (!cyborg.Comp.Active || target == cyborg.Owner || HasComp<CyborgComponent>(target))
            return false;

        if (_hands.GetActiveItem(cyborg.Owner) != null)
            return false;

        return HasComp<DoorComponent>(target) ||
               TryComp<StationAiWhitelistComponent>(target, out var whitelist) && whitelist.Enabled;
    }

    /// <summary>
    /// Range check for a cyborg keeping a remote interface open.
    /// </summary>
    public bool InRemoteRange(EntityUid cyborg, EntityUid target)
    {
        var xform = Transform(target);
        return _interaction.InRangeUnobstructed(cyborg, target, xform.Coordinates, xform.LocalRotation, RemoteRange);
    }
}
