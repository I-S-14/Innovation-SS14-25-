// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Power.EntitySystems;
using Content.Server.Radio.EntitySystems;
using Content.Shared._IS14.Research;
using Content.Shared.Damage;
using Content.Shared.DoAfter;
using Content.Shared.Humanoid;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Materials;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.Tools.Components;
using Content.Shared.Verbs;
using Content.Shared.Whitelist;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server._IS14.Research;

/// <summary>
/// The measuring core of the NIC: one place where a sample becomes a profile, the archive
/// decides how new that profile is, and the station gets paid.
/// </summary>
/// <remarks>
/// Deliberately generic. A new source of research data should be a prototype with a
/// whitelist and a rate, or at most a component answering
/// <see cref="IS14GetExperimentProfileEvent"/> — not another system.
/// </remarks>
public sealed class IS14ExperimentSystem : EntitySystem
{
    [Dependency] private readonly IS14ResearchPointSystem _points = default!;
    [Dependency] private readonly IS14ResearchModifierSystem _modifiers = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly RadioSystem _radio = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14ExperimentBenchComponent, InteractUsingEvent>(OnBenchInteractUsing);
        SubscribeLocalEvent<IS14ExperimentBenchComponent, GetVerbsEvent<UtilityVerb>>(OnBenchGetVerbs);
        SubscribeLocalEvent<IS14ExperimentScannerComponent, AfterInteractEvent>(OnScannerAfterInteract);
        SubscribeLocalEvent<IS14ExperimentScannerComponent, IS14ExperimentScanDoAfterEvent>(OnScanFinished);
    }

    #region Bench

    private void OnBenchInteractUsing(Entity<IS14ExperimentBenchComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !Accepts(ent, args.Used))
            return;

        // Tools are left alone: swallowing them here would make the bench impossible to
        // unbolt or repair. They can still be measured through the verb below.
        if (HasComp<ToolComponent>(args.Used))
            return;

        args.Handled = TryMeasureSample(ent, args.Used, args.User);
    }

    /// <summary>
    /// "Analyse" on right-click while holding something. This is the path that works for
    /// samples the bench deliberately ignores on a plain click, such as tools.
    /// </summary>
    private void OnBenchGetVerbs(Entity<IS14ExperimentBenchComponent> ent, ref GetVerbsEvent<UtilityVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.Using is not { } used)
            return;

        if (!Accepts(ent, used))
            return;

        var user = args.User;
        var sample = used;

        args.Verbs.Add(new UtilityVerb
        {
            Text = Loc.GetString("is14-research-bench-verb"),
            Act = () => TryMeasureSample(ent, sample, user),
        });
    }

    private bool Accepts(Entity<IS14ExperimentBenchComponent> ent, EntityUid sample)
    {
        return !_whitelist.IsWhitelistFail(ent.Comp.Whitelist, sample)
               && !_whitelist.IsWhitelistPass(ent.Comp.Blacklist, sample);
    }

    /// <summary>Measures a sample handed to a bench. False when the bench did nothing at all.</summary>
    private bool TryMeasureSample(Entity<IS14ExperimentBenchComponent> ent, EntityUid sample, EntityUid user)
    {
        var bench = ent.Comp;

        if (!this.IsPowered(ent.Owner, EntityManager))
            return false;

        if (_timing.CurTime < bench.NextReady)
        {
            _popup.PopupEntity(Loc.GetString("is14-research-bench-busy"), ent.Owner, user);
            return true;
        }

        bench.NextReady = _timing.CurTime + bench.Cooldown;

        var result = Measure(
            ent.Owner,
            sample,
            bench.PointType,
            bench.BaseValue,
            bench.Mode,
            bench.ProfileNamespace,
            operator_: user);

        if (result.Refused)
        {
            _popup.PopupEntity(
                Loc.GetString(result.RefuseReason ?? "is14-research-sample-useless"),
                ent.Owner,
                user);
            return true;
        }

        _audio.PlayPvs(bench.Sound, ent.Owner);
        ReportResult(ent.Owner, user, result, bench.Channel);

        if (bench.Consume && !result.PreventConsume)
            QueueDel(sample);

        return true;
    }

    #endregion

    #region Scanner

    private void OnScannerAfterInteract(Entity<IS14ExperimentScannerComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target == args.User)
            return;

        var scanner = ent.Comp;

        if (_whitelist.IsWhitelistFail(scanner.Whitelist, target)
            || _whitelist.IsWhitelistPass(scanner.Blacklist, target))
        {
            return;
        }

        args.Handled = true;

        if (scanner.RequireDead && !(HasComp<MobStateComponent>(target) && _mobState.IsDead(target)))
        {
            _popup.PopupEntity(Loc.GetString("is14-research-scanner-needs-dead"), ent.Owner, args.User);
            return;
        }

        if (scanner.RequireAlive && (!HasComp<MobStateComponent>(target) || _mobState.IsDead(target)))
        {
            _popup.PopupEntity(Loc.GetString("is14-research-scanner-needs-alive"), ent.Owner, args.User);
            return;
        }

        var doAfter = new DoAfterArgs(EntityManager, args.User, scanner.Delay, new IS14ExperimentScanDoAfterEvent(), ent.Owner, target, ent.Owner)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfter))
            _popup.PopupEntity(Loc.GetString("is14-research-scanner-start"), ent.Owner, args.User);
    }

    private void OnScanFinished(Entity<IS14ExperimentScannerComponent> ent, ref IS14ExperimentScanDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled || args.Target is not { } target)
            return;

        args.Handled = true;

        var scanner = ent.Comp;

        var result = Measure(
            ent.Owner,
            target,
            scanner.PointType,
            scanner.BaseValue,
            scanner.Mode,
            scanner.ProfileNamespace);

        if (result.Refused)
        {
            _popup.PopupEntity(
                Loc.GetString(result.RefuseReason ?? "is14-research-sample-useless"),
                ent.Owner,
                args.User);
            return;
        }

        _audio.PlayPvs(scanner.Sound, ent.Owner);
        ReportResult(ent.Owner, args.User, result, scanner.Channel);
    }

    #endregion

    #region Core

    /// <summary>
    /// Runs one measurement: builds the profile, asks the archive how new it is, pays out.
    /// Public so other systems (the doppler array, future experiments) reuse the same rule.
    /// </summary>
    public IS14MeasurementResult Measure(
        EntityUid device,
        EntityUid sample,
        ProtoId<ResearchPointTypePrototype> pointType,
        int baseValue,
        IS14ExperimentProfileMode mode,
        string profileNamespace,
        float extraMultiplier = 1f,
        EntityUid? operator_ = null)
    {
        var ev = new IS14GetExperimentProfileEvent(device);
        RaiseLocalEvent(sample, ev);

        if (ev.Refuse)
            return IS14MeasurementResult.Refuse(ev.RefuseReason);

        if (_points.ResolveStation(device) is not { } station)
            return IS14MeasurementResult.Refuse("is14-research-no-station");

        var profileBody = ev.ProfileKey ?? BuildProfile(sample, mode);

        if (profileBody == null)
            return IS14MeasurementResult.Refuse("is14-research-sample-useless");

        var key = $"{profileNamespace}:{profileBody}";
        var prior = _points.RecordMeasurement(station, key);

        var type = ev.PointTypeOverride ?? pointType;

        var payout = (int) MathF.Round(baseValue
                                       * ev.Multiplier
                                       * extraMultiplier
                                       * _points.PayoutMultiplier
                                       * _modifiers.GetPayoutMultiplier(station, type)
                                       * NoveltyMultiplier(station, prior));

        // Even a pure repeat is worth confirming, so a measured result never pays nothing.
        payout = Math.Max(1, payout);

        var failed = Failed(device, station, key, operator_);

        if (failed)
        {
            var risk = Comp<IS14ExperimentRiskComponent>(device);

            payout = Math.Max(1, (int) MathF.Round(payout * risk.NegativeShare));

            // The sample is lost whatever the instrument's usual manners: that is the cost of
            // the bad roll, and it is why procedure is worth following.
            ev.PreventConsume = false;
        }

        _points.AddPoints(station, type, payout);

        return new IS14MeasurementResult
        {
            Payout = payout,
            PointType = type,
            PriorCount = prior,
            ProfileKey = key,
            PreventConsume = ev.PreventConsume,
            Failed = failed,
        };
    }

    /// <summary>
    /// Rolls the instrument's risk, if it has any. Gear and earlier failures of this very
    /// measurement cut the odds; a failure is remembered, which is what makes a negative
    /// result worth something.
    /// </summary>
    private bool Failed(EntityUid device, EntityUid station, string profileKey, EntityUid? operator_)
    {
        if (!TryComp<IS14ExperimentRiskComponent>(device, out var risk) || risk.Risk <= 0f)
            return false;

        var chance = risk.Risk;

        if (operator_ != null && risk.Safety != null)
        {
            var protection = 0;

            foreach (var item in _inventory.GetHandOrInventoryEntities(operator_.Value))
            {
                if (_whitelist.IsWhitelistPass(risk.Safety, item))
                    protection++;
            }

            chance *= MathF.Pow(1f - Math.Clamp(risk.SafetyReduction, 0f, 1f), protection);
        }

        // Every earlier failure of this measurement is one way it breaks that is now known.
        var failures = _points.PeekMeasurement(station, FailureKey(profileKey));
        chance *= MathF.Max(0f, 1f - risk.LearningReduction * failures);

        if (!_random.Prob(Math.Clamp(chance, 0f, 1f)))
            return false;

        _points.RecordMeasurement(station, FailureKey(profileKey));

        if (risk.Effect is { } effect)
            Spawn(effect, Transform(device).Coordinates);

        _audio.PlayPvs(risk.Sound, device);

        return true;
    }

    /// <summary>Where failures of a measurement are counted, in the same archive as the results.</summary>
    public static string FailureKey(string profileKey) => $"risk:{profileKey}";

    /// <summary>
    /// How much a repeat pays. Research into methodology raises the floor, which is the point
    /// of the "ExperimentNoveltyRetention" modifier: better procedure salvages more from a
    /// measurement that has been made before.
    /// </summary>
    public float NoveltyMultiplier(EntityUid station, int priorCount)
    {
        var baseMultiplier = IS14Novelty.Multiplier(priorCount);

        if (priorCount == 0)
            return baseMultiplier;

        var retention = _modifiers.GetModifier(station, "ExperimentNoveltyRetention");
        return MathF.Min(1f, baseMultiplier + retention);
    }

    /// <summary>Profile key body, or null when the sample carries no usable data.</summary>
    private string? BuildProfile(EntityUid sample, IS14ExperimentProfileMode mode)
    {
        switch (mode)
        {
            case IS14ExperimentProfileMode.Prototype:
                return MetaData(sample).EntityPrototype?.ID ?? "unknown";

            case IS14ExperimentProfileMode.Mob:
            {
                if (!HasComp<MobStateComponent>(sample))
                    return null;

                var species = TryComp<HumanoidAppearanceComponent>(sample, out var humanoid)
                    ? humanoid.Species.Id
                    : MetaData(sample).EntityPrototype?.ID ?? "unknown";

                var cause = "intact";

                if (TryComp<DamageableComponent>(sample, out var damageable) && !damageable.Damage.Empty)
                {
                    var dominant = damageable.Damage.DamageDict
                        .Where(pair => pair.Value > 0)
                        .OrderByDescending(pair => pair.Value)
                        .Select(pair => pair.Key)
                        .FirstOrDefault();

                    cause = dominant ?? "intact";
                }

                var state = _mobState.IsDead(sample) ? "dead" : "alive";
                return $"{species}|{cause}|{state}";
            }

            case IS14ExperimentProfileMode.Material:
            {
                if (!TryComp<PhysicalCompositionComponent>(sample, out var composition)
                    || composition.MaterialComposition.Count == 0)
                {
                    return null;
                }

                var materials = composition.MaterialComposition.Keys.OrderBy(m => m);
                return string.Join('+', materials);
            }

            case IS14ExperimentProfileMode.Custom:
                // Nothing answered the profile event, so there is nothing to measure.
                return null;

            default:
                return null;
        }
    }

    /// <summary>Tells the operator what came out, and the department if the device is wired for radio.</summary>
    public void ReportResult(
        EntityUid device,
        EntityUid? user,
        IS14MeasurementResult result,
        ProtoId<RadioChannelPrototype>? channel)
    {
        var typeName = _proto.TryIndex(result.PointType, out ResearchPointTypePrototype? typeProto)
            ? Loc.GetString(typeProto.Name)
            : result.PointType.Id;

        var message = result.Failed
            ? Loc.GetString("is14-research-measured-failed",
                ("amount", result.Payout),
                ("type", typeName))
            : Loc.GetString("is14-research-measured",
                ("amount", result.Payout),
                ("type", typeName),
                ("novelty", Loc.GetString(IS14Novelty.Label(result.PriorCount))));

        if (user != null)
            _popup.PopupEntity(message, device, user.Value);

        if (channel != null)
            _radio.SendRadioMessage(device, message, channel.Value, device, escapeMarkup: false);
    }

    #endregion
}

/// <summary>Outcome of one measurement.</summary>
public struct IS14MeasurementResult
{
    public int Payout;
    public ProtoId<ResearchPointTypePrototype> PointType;

    /// <summary>How many times this profile had been measured before this one.</summary>
    public int PriorCount;

    public string ProfileKey;
    public bool PreventConsume;

    /// <summary>The instrument's risk caught up with it: a negative result.</summary>
    public bool Failed;
    public bool Refused;
    public string? RefuseReason;

    public static IS14MeasurementResult Refuse(string? reason) => new()
    {
        Refused = true,
        RefuseReason = reason,
    };
}
