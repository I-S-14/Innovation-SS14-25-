using Content.Shared._IS.Qualification;
using Content.Shared._IS.Qualification.Components;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.Examine;
using Content.Shared.GameTicking;
using Content.Shared.PDA;
using Content.Shared.Players.PlayTimeTracking;
using Content.Shared.Roles;
using Robust.Shared.Prototypes;

namespace Content.Server._IS.Qualification;

public sealed partial class QualificationSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private readonly ISharedPlaytimeManager _playtimeManager = default!;
    [Dependency] private readonly AccessReaderSystem _accessReader = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerJobAssigned);
        SubscribeLocalEvent<QualificationComponent, ExaminedEvent>(OnExamine);
    }

    private void OnPlayerJobAssigned(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == null
            || args.Player.AttachedEntity == null)
            return;

        var jobId = args.JobId;

        if (!_prototype.TryIndex<JobPrototype>(jobId, out var jobProto)
            || jobProto == null)
            return;

        var jobTimeTracker = jobProto.PlayTimeTracker;
        var session = args.Player;
        var playTimes = _playtimeManager.GetPlayTimes(session);

        playTimes.TryGetValue(jobTimeTracker, out var time);

        var qgPrototypes = _prototype.EnumeratePrototypes<QualificationGroupPrototype>();

        foreach (var qgPrototype in qgPrototypes)
        {
            if (qgPrototype.JobPrototypes.Contains(jobId))
            {
                var qPrototypes = qgPrototype.QualificationHashSet;

                QualificationPrototype? qp = null;

                foreach (var qPrototype in qPrototypes)
                {
                    if (!_prototype.TryIndex(qPrototype, out var proto))
                        continue;

                    var hourTimeRquirement = time.TotalHours;

                    if (hourTimeRquirement >= proto.Requirement)
                        qp = proto;
                }

                if (qp == null)
                    continue;

                var qComp = EnsureComp<QualificationComponent>(session.AttachedEntity.Value);
                qComp.QualificationIcon = qp;
            }
        }
    }

    private void OnExamine(Entity<QualificationComponent> entity, ref ExaminedEvent args)
    {
        if (!CheckIDCard(entity)
            || !args.IsInDetailsRange)
            return;

        ProtoId<QualificationPrototype>? iconId = entity.Comp.QualificationIcon;
        _prototype.TryIndex(iconId, out var iconPrototype);

        if (iconPrototype == null)
            return;

        var locale = iconPrototype.QualificationTitle;

        args.PushText(Loc.GetString(locale),
            5);
    }

    private bool CheckIDCard(EntityUid entity)
    {
        if (_accessReader.FindAccessItemsInventory(entity, out var items))
        {
            foreach (var item in items)
            {
                // ID Card
                if (HasComp<IdCardComponent>(item))
                {
                    return true;
                }

                // PDA
                if (TryComp<PdaComponent>(item, out var pda)
                    && pda.ContainedId != null
                    && HasComp<IdCardComponent>(pda.ContainedId))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
