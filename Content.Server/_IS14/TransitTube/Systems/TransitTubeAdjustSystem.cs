using Content.Server.Popups;
using Content.Shared._IS14.TransitTube;
using Content.Shared._IS14.TransitTube.Components;
using Content.Shared.Interaction;
using Content.Shared.Tools.Systems;

namespace Content.Server._IS14.TransitTube.Systems;

/// <summary>
/// What a tool does to a tube section. A bend is walked round an eighth of a circle at a
/// time, which is how it reaches the four corners the placement ghost cannot; a stop or a
/// junction instead has its preferred way out swung over, since turning those would point
/// the whole stop somewhere else. A plain section has nothing to adjust: its four turns
/// already give it all four lines.
/// </summary>
public sealed class TransitTubeAdjustSystem : EntitySystem
{
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly SharedToolSystem _tool = default!;
    [Dependency] private readonly TransitTubeJointSystem _joints = default!;
    [Dependency] private readonly SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransitTubeComponent, InteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<TransitTubeComponent, TransitTubeAdjustDoAfterEvent>(OnAdjusted);
    }

    private void OnInteractUsing(Entity<TransitTubeComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        // A pod sitting in the section would be left in a tube pointing somewhere else.
        if (HasPod(ent))
        {
            _popup.PopupEntity(Loc.GetString("is14-transit-tube-adjust-occupied"), ent, args.User);
            return;
        }

        if (!_tool.UseTool(args.Used,
                args.User,
                ent,
                ent.Comp.AdjustDelay,
                ent.Comp.AdjustTool,
                new TransitTubeAdjustDoAfterEvent()))
        {
            return;
        }

        args.Handled = true;
    }

    private void OnAdjusted(Entity<TransitTubeComponent> ent, ref TransitTubeAdjustDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        if (HasPod(ent))
        {
            _popup.PopupEntity(Loc.GetString("is14-transit-tube-adjust-occupied"), ent, args.User);
            return;
        }

        Adjust(ent, args.User);
    }

    /// <summary>
    /// Nudges a section: a plain one swaps between its straight and diagonal reckoning, a
    /// stop or junction swings the way it prefers to send pods.
    /// </summary>
    public void Adjust(Entity<TransitTubeComponent> ent, EntityUid? user = null)
    {
        if (TryComp<TransitTubeBendComponent>(ent, out var bend))
        {
            // Eight corners, four turns: each nudge moves on by 45 degrees, swapping to the
            // diagonal half of the pair and, when that wraps, turning a quarter as well.
            bend.Diagonal = !bend.Diagonal;
            Dirty(ent.Owner, bend);
            _appearance.SetData(ent, TransitTubeVisuals.Diagonal, bend.Diagonal);

            if (!bend.Diagonal)
            {
                var rotation = Transform(ent).LocalRotation + Angle.FromDegrees(90);
                _xform.SetLocalRotation(ent, rotation.Reduced());
            }
            else
            {
                // Swapping halves without turning moves no one, so nothing else would
                // notice that this section now offers a different pair of ends.
                _joints.MarkStale(ent);
            }

            Say(ent, user, "is14-transit-tube-turned");
            return;
        }

        if (TryComp<TransitTubeStationComponent>(ent, out var station) && !station.Terminus)
        {
            station.PreferCounterClockwise = !station.PreferCounterClockwise;
            Dirty(ent.Owner, station);
            Say(ent, user, "is14-transit-tube-station-redirected");
            return;
        }

        if (TryComp<TransitTubeJunctionComponent>(ent, out var junction))
        {
            junction.PreferCounterClockwise = !junction.PreferCounterClockwise;
            Dirty(ent.Owner, junction);
            _appearance.SetData(ent, TransitTubeVisuals.Flipped, junction.PreferCounterClockwise);
            Say(ent, user, "is14-transit-tube-junction-redirected");
            return;
        }

        Say(ent, user, "is14-transit-tube-adjust-nothing");
    }

    private void Say(EntityUid uid, EntityUid? user, string message)
    {
        if (user != null)
            _popup.PopupEntity(Loc.GetString(message), uid, user.Value);
    }

    /// <summary>
    /// Whether a pod is parked in or travelling through this section.
    /// </summary>
    private bool HasPod(EntityUid tube)
    {
        var query = EntityQueryEnumerator<TransitTubePodComponent>();
        while (query.MoveNext(out _, out var pod))
        {
            if (pod.CurrentTube == tube)
                return true;
        }

        return false;
    }
}
