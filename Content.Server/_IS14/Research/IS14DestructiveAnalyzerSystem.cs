// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Power.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Shared._IS14.Research;
using Content.Shared.Interaction;
using Content.Shared.Materials;
using Content.Shared.Popups;
using Content.Shared.Research.Prototypes;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Research;

/// <summary>
/// Runs the destructive analyzer: the sample goes in, the tube cycles, the sample is gone and
/// the station has data — plus a discount on whatever technology would have built it.
/// </summary>
public sealed class IS14DestructiveAnalyzerSystem : EntitySystem
{
    [Dependency] private readonly IS14ResearchPointSystem _points = default!;
    [Dependency] private readonly IS14ExperimentSystem _experiment = default!;
    [Dependency] private readonly IS14ResearchModifierSystem _modifiers = default!;
    [Dependency] private readonly IS14BreakthroughSystem _breakthroughs = default!;
    [Dependency] private readonly IS14ResearchCostSystem _costs = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>Result prototype of a lathe recipe to the technologies that unlock it.</summary>
    private readonly Dictionary<string, List<string>> _unlockedBy = new();

    private bool _indexDirty = true;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14DestructiveAnalyzerComponent, InteractUsingEvent>(OnInteractUsing);
        _proto.PrototypesReloaded += _ => _indexDirty = true;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<IS14DestructiveAnalyzerComponent>();
        while (query.MoveNext(out var uid, out var analyzer))
        {
            if (analyzer.FinishesAt is not { } finish || _timing.CurTime < finish)
                continue;

            Finish((uid, analyzer));
        }
    }

    private void OnInteractUsing(Entity<IS14DestructiveAnalyzerComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !this.IsPowered(ent.Owner, EntityManager))
            return;

        if (_whitelist.IsWhitelistFail(ent.Comp.Whitelist, args.Used)
            || _whitelist.IsWhitelistPass(ent.Comp.Blacklist, args.Used))
        {
            return;
        }

        args.Handled = true;

        TryAnalyze(ent, args.Used, args.User);
    }

    /// <summary>
    /// Takes a sample in: the object is consumed at once and the cycle that follows is the
    /// analysis. Public and free of the interaction plumbing, so the whole path can be tested.
    /// </summary>
    public bool TryAnalyze(Entity<IS14DestructiveAnalyzerComponent> ent, EntityUid sample, EntityUid? user)
    {
        if (ent.Comp.PendingPrototype != null)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("is14-research-bench-busy"), ent.Owner, user.Value);

            return false;
        }

        var samplePrototype = MetaData(sample).EntityPrototype?.ID;
        var station = _points.ResolveStation(ent.Owner);

        // A breakthrough sample is judged by what it is, not by what it is made of: an anomaly
        // core has no material composition worth speaking of and is still the key to a branch.
        var breakthrough = _breakthroughs.TryMatchSample(station, samplePrototype, out var technologyId, out _)
            ? technologyId
            : null;

        if (breakthrough == null
            && (!TryComp<PhysicalCompositionComponent>(sample, out var composition)
                || composition.MaterialComposition.Count == 0))
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("is14-analyzer-nothing-to-take-apart"), ent.Owner, user.Value);

            return false;
        }

        var materials = 0;

        if (TryComp<PhysicalCompositionComponent>(sample, out var parts))
        {
            foreach (var amount in parts.MaterialComposition.Values)
            {
                materials += amount;
            }
        }

        ent.Comp.PendingPrototype = samplePrototype ?? "unknown";
        ent.Comp.PendingBreakthrough = breakthrough;
        ent.Comp.PendingMaterials = materials;
        ent.Comp.User = user;
        ent.Comp.FinishesAt = _timing.CurTime + ent.Comp.Duration;

        QueueDel(sample);

        _audio.PlayPvs(ent.Comp.StartSound, ent.Owner);

        if (user != null)
            _popup.PopupEntity(Loc.GetString("is14-analyzer-started"), ent.Owner, user.Value);

        return true;
    }

    private void Finish(Entity<IS14DestructiveAnalyzerComponent> ent)
    {
        var analyzer = ent.Comp;

        analyzer.FinishesAt = null;

        var prototypeId = analyzer.PendingPrototype;
        var materials = analyzer.PendingMaterials;
        var user = analyzer.User;
        var breakthrough = analyzer.PendingBreakthrough;

        analyzer.PendingPrototype = null;
        analyzer.PendingBreakthrough = null;
        analyzer.PendingMaterials = 0;
        analyzer.User = null;

        if (prototypeId == null)
            return;

        if (_points.ResolveStation(ent.Owner) is not { } station)
        {
            if (user != null)
                _popup.PopupEntity(Loc.GetString("is14-research-no-station"), ent.Owner, user.Value);

            return;
        }

        if (breakthrough != null)
        {
            Breakthrough(ent, station, breakthrough, user);
            return;
        }

        var scale = Math.Clamp(
            materials / (float) Math.Max(1, analyzer.ReferenceMaterial),
            analyzer.MinScale,
            analyzer.MaxScale);

        var key = $"{analyzer.ProfileNamespace}:{prototypeId}";
        var prior = _points.RecordMeasurement(station, key);

        var payout = Math.Max(1, (int) MathF.Round(
            analyzer.BaseValue
            * scale
            * _points.PayoutMultiplier
            * _modifiers.GetPayoutMultiplier(station, analyzer.PointType)
            * _experiment.NoveltyMultiplier(station, prior)));

        _points.AddPoints(station, analyzer.PointType, payout);

        var result = new IS14MeasurementResult
        {
            Payout = payout,
            PointType = analyzer.PointType,
            PriorCount = prior,
            ProfileKey = key,
        };

        _experiment.ReportResult(ent.Owner, user, result, analyzer.Channel);

        ReverseEngineer(ent, station, prototypeId, user);
    }

    /// <summary>
    /// The sample was the key to a branch: it is gone, the way is open, and the whole station
    /// hears about it. Public on purpose — a breakthrough is the kind of event a shift is
    /// remembered by, and the announcement is also what tells security the crystal was used.
    /// </summary>
    private void Breakthrough(
        Entity<IS14DestructiveAnalyzerComponent> ent,
        EntityUid station,
        string technologyId,
        EntityUid? user)
    {
        if (!_proto.TryIndex<TechnologyPrototype>(technologyId, out var tech))
            return;

        var requirement = _costs.GetBreakthrough(technologyId);

        _breakthroughs.Complete(station, technologyId);

        if (requirement is { Payout: > 0 })
        {
            var payout = Math.Max(1, (int) MathF.Round(
                requirement.Payout
                * _points.PayoutMultiplier
                * _modifiers.GetPayoutMultiplier(station, requirement.PointType)));

            _points.AddPoints(station, requirement.PointType, payout);
        }

        var message = Loc.GetString("is14-analyzer-breakthrough-broadcast",
            ("technology", Loc.GetString(tech.Name)));

        if (ent.Comp.Channel is { } channel)
            _radio.SendRadioMessage(ent.Owner, message, channel, ent.Owner, escapeMarkup: false);

        if (user != null)
            _popup.PopupEntity(message, ent.Owner, user.Value);
    }

    /// <summary>
    /// Taking apart something the station cannot build yet is worth more than the materials:
    /// it knocks a slice off the price of the technology that would unlock it.
    /// </summary>
    private void ReverseEngineer(
        Entity<IS14DestructiveAnalyzerComponent> ent,
        EntityUid station,
        string prototypeId,
        EntityUid? user)
    {
        if (_indexDirty)
            RebuildIndex();

        if (!_unlockedBy.TryGetValue(prototypeId, out var technologies))
            return;

        var discounts = EnsureComp<IS14ResearchDiscountComponent>(station);
        var reported = false;

        foreach (var technologyId in technologies)
        {
            if (!_proto.TryIndex<TechnologyPrototype>(technologyId, out var tech) || tech.Hidden)
                continue;

            discounts.Discounts.TryGetValue(technologyId, out var current);

            if (current >= ent.Comp.MaxDiscount)
                continue;

            discounts.Discounts[technologyId] =
                MathF.Min(ent.Comp.MaxDiscount, current + ent.Comp.ReverseEngineeringDiscount);

            if (reported || user == null)
                continue;

            _popup.PopupEntity(
                Loc.GetString("is14-analyzer-reverse-engineered",
                    ("technology", Loc.GetString(tech.Name)),
                    ("percent", (int) MathF.Round(discounts.Discounts[technologyId] * 100))),
                ent.Owner,
                user.Value);

            reported = true;
        }
    }

    /// <summary>Builds "this item is unlocked by that technology" out of the lathe recipes.</summary>
    private void RebuildIndex()
    {
        _unlockedBy.Clear();
        _indexDirty = false;

        foreach (var tech in _proto.EnumeratePrototypes<TechnologyPrototype>())
        {
            foreach (var recipeId in tech.RecipeUnlocks)
            {
                if (!_proto.TryIndex(recipeId, out LatheRecipePrototype? recipe) || recipe.Result is not { } result)
                    continue;

                if (!_unlockedBy.TryGetValue(result.Id, out var list))
                {
                    list = new List<string>();
                    _unlockedBy[result.Id] = list;
                }

                list.Add(tech.ID);
            }
        }
    }
}
