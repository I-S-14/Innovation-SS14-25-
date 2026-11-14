// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Linq;
using Content.Server._DV.Objectives.Events;
using Content.Server.Silicons.Laws;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared._IS14.Cyborgs.Systems;
using Content.Shared.Database;
using Content.Shared.Administration.Logs;
using Content.Shared.Emag.Systems;
using Content.Shared.Popups;
using Content.Shared.Silicons.Laws;
using Content.Shared.Silicons.Laws.Components;
using Content.Shared.Silicons.StationAi;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Cyborgs;

/// <summary>
/// tgstation's cyborg-AI link: cyborgs connect to the station AI, follow its laws and can be locked down by it.
/// </summary>
public sealed class CyborgAiLinkSystem : SharedCyborgAiLinkSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly EmagSystem _emag = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SiliconLawSystem _laws = default!;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(2);
    private TimeSpan _nextCheck;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AILawUpdatedEvent>(OnAiLawsUpdated);
        SubscribeLocalEvent<CyborgAiLinkComponent, SiliconEmaggedEvent>(OnEmagged);
        SubscribeLocalEvent<CyborgAiLinkComponent, GetVerbsEvent<Verb>>(OnGetVerbs);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_timing.CurTime < _nextCheck)
            return;

        _nextCheck = _timing.CurTime + CheckInterval;

        var query = EntityQueryEnumerator<CyborgAiLinkComponent, CyborgComponent>();
        while (query.MoveNext(out var uid, out var link, out var cyborg))
        {
            // The AI is gone: carded, destroyed or its core taken apart.
            if (link.Ai is { } ai && (!Exists(ai) || !HasComp<StationAiHeldComponent>(ai)))
            {
                Unlink((uid, link));
                continue;
            }

            if (link.Ai == null && link.AutoLink && cyborg.Active && CanLink((uid, link)))
                TryLinkToBestAi((uid, link));
        }
    }

    private bool CanLink(Entity<CyborgAiLinkComponent> ent)
    {
        return !ent.Comp.LinkCut && !_emag.CheckFlag(ent.Owner, EmagType.Interaction);
    }

    /// <summary>
    /// tg links a new cyborg to the AI with the fewest cyborgs.
    /// </summary>
    public bool TryLinkToBestAi(Entity<CyborgAiLinkComponent> ent)
    {
        var map = _transform.GetMapId(ent.Owner);
        var counts = new Dictionary<EntityUid, int>();
        var ais = EntityQueryEnumerator<StationAiHeldComponent>();
        while (ais.MoveNext(out var ai, out _))
        {
            if (_transform.GetMapId(ai) == map)
                counts[ai] = 0;
        }

        if (counts.Count == 0)
            return false;

        var links = EntityQueryEnumerator<CyborgAiLinkComponent>();
        while (links.MoveNext(out _, out var other))
        {
            if (other.Ai is { } linked && counts.ContainsKey(linked))
                counts[linked]++;
        }

        Link(ent, counts.MinBy(pair => pair.Value).Key);
        return true;
    }

    public void Link(Entity<CyborgAiLinkComponent> ent, EntityUid ai)
    {
        ent.Comp.Ai = ai;
        Dirty(ent);

        _popup.PopupEntity(Loc.GetString("cyborg-ai-linked", ("ai", Name(ai))), ent, ent, PopupType.Medium);
        _popup.PopupEntity(Loc.GetString("cyborg-ai-linked-ai", ("cyborg", Name(ent))), ai, ai, PopupType.Medium);
        _adminLog.Add(LogType.SiliconLaws, LogImpact.Low,
            $"{ToPrettyString(ent):entity} linked to AI {ToPrettyString(ai):entity}");

        if (ent.Comp.LawSync)
            SyncLaws(ent);
    }

    public void Unlink(Entity<CyborgAiLinkComponent> ent)
    {
        if (ent.Comp.Ai is not { } ai)
            return;

        ent.Comp.Ai = null;
        Dirty(ent);

        _popup.PopupEntity(Loc.GetString("cyborg-ai-unlinked"), ent, ent, PopupType.MediumCaution);
        if (Exists(ai))
            _popup.PopupEntity(Loc.GetString("cyborg-ai-unlinked-ai", ("cyborg", Name(ent))), ai, ai,
                PopupType.MediumCaution);
    }

    /// <summary>
    /// Copies the linked AI's laws onto the cyborg, tg's lawsync().
    /// </summary>
    public void SyncLaws(Entity<CyborgAiLinkComponent> ent)
    {
        if (ent.Comp.Ai is not { } ai || !HasComp<SiliconLawBoundComponent>(ai) ||
            !HasComp<SiliconLawProviderComponent>(ent))
            return;

        var laws = _laws.GetLaws(ai).Laws.Select(law => law.ShallowClone()).ToList();
        _laws.SetLaws(laws, ent);
    }

    private void OnAiLawsUpdated(AILawUpdatedEvent ev)
    {
        var query = EntityQueryEnumerator<CyborgAiLinkComponent>();
        while (query.MoveNext(out var uid, out var link))
        {
            if (link.Ai == ev.Target && link.LawSync)
                SyncLaws((uid, link));
        }
    }

    // tg: emagged cyborgs drop their AI and stop syncing.
    private void OnEmagged(Entity<CyborgAiLinkComponent> ent, ref SiliconEmaggedEvent args)
    {
        Unlink(ent);
        ent.Comp.LawSync = false;
        Dirty(ent);
    }

    private void OnGetVerbs(Entity<CyborgAiLinkComponent> ent, ref GetVerbsEvent<Verb> args)
    {
        var user = args.User;
        if (!HasComp<StationAiHeldComponent>(user))
            return;

        if (ent.Comp.Ai != user)
        {
            if (ent.Comp.Ai == null && CanLink(ent))
            {
                args.Verbs.Add(new Verb
                {
                    Text = Loc.GetString("cyborg-ai-verb-link"),
                    Act = () => Link(ent, user),
                });
            }

            return;
        }

        var lockdown = ent.Comp.Lockdown;
        args.Verbs.Add(new Verb
        {
            Text = Loc.GetString(lockdown ? "cyborg-ai-verb-release" : "cyborg-ai-verb-lockdown"),
            Act = () => SetLockdownByAi(ent, !lockdown, user),
            Impact = LogImpact.Medium,
        });

        if (ent.Comp.LawSync)
        {
            args.Verbs.Add(new Verb
            {
                Text = Loc.GetString("cyborg-ai-verb-sync"),
                Act = () =>
                {
                    SyncLaws(ent);
                    _popup.PopupEntity(Loc.GetString("cyborg-ai-synced", ("cyborg", Name(ent))), user, user);
                },
            });
        }
    }

    public void SetLockdownByAi(Entity<CyborgAiLinkComponent> ent, bool lockdown, EntityUid? user)
    {
        SetLockdown(ent, lockdown);
        _audio.PlayPvs(ent.Comp.LockdownSound, ent);
        _popup.PopupEntity(Loc.GetString(lockdown ? "cyborg-lockdown-on" : "cyborg-lockdown-off"), ent, ent,
            lockdown ? PopupType.LargeCaution : PopupType.Medium);

        if (user != null)
        {
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(user.Value):player} set lockdown of {ToPrettyString(ent):entity} to {lockdown}");
        }
    }
}
