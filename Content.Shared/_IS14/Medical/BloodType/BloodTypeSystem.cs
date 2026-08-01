// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Goobstation.Maths.FixedPoint;
using Content.Shared.Body.Components;
using Content.Shared.Body.Events;
using Content.Shared.Chemistry.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Forensics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._IS14.Medical.BloodType;

/// <summary>
/// Who can be given whose blood, and what happens when the answer was no.
/// </summary>
/// <remarks>
/// Everything here hangs off one rule: blood is accepted when it is the same substance and
/// carries no antigen the recipient lacks. Species, groups and rhesus are all the same
/// check — a moth is rejected because <c>InsectBlood</c> is not <c>Blood</c>, an A donor is
/// rejected by an O recipient because of the A antigen, and neither case needs its own code
/// path. Species nobody has written groups for keep behaving exactly as they did before.
/// </remarks>
public sealed class BloodTypeSystem : EntitySystem
{
    /// <summary>What rejected blood turns into when a type has nothing else to say.</summary>
    public static readonly ProtoId<ReagentPrototype> DefaultRejectedReagent = "IS14HemolysedBlood";

    /// <summary>Stand-in for blood nobody has stamped: nothing on it to object to.</summary>
    private static readonly HashSet<ProtoId<BloodAntigenPrototype>> EmptyAntigens = new();

    [Dependency] private readonly IPrototypeManager _protos = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutions = default!;

    /// <summary>Groups per blood reagent, i.e. the pool a member of that species rolls in.</summary>
    private readonly Dictionary<string, List<BloodTypePrototype>> _typesByReagent = new();

    /// <summary>Antigens that appear anywhere in a reagent's groups, in card order.</summary>
    private readonly Dictionary<string, List<BloodAntigenPrototype>> _antigensByReagent = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PrototypesReloadedEventArgs>(_ => BuildRegistry());

        // On our own component rather than on BloodstreamComponent: the engine allows exactly
        // one subscription per component-event pair, and upstream already holds that one.
        SubscribeLocalEvent<BloodTypeComponent, MetabolismExclusionEvent>(OnMetabolismExclusion);

        BuildRegistry();
    }

    // ── The registry ──────────────────────────────────────────────────────────

    private void BuildRegistry()
    {
        _typesByReagent.Clear();
        _antigensByReagent.Clear();

        foreach (var type in _protos.EnumeratePrototypes<BloodTypePrototype>())
        {
            _typesByReagent.GetOrNew(type.Reagent).Add(type);
        }

        foreach (var (reagent, types) in _typesByReagent)
        {
            // Stable order matters: the natural roll walks this list and has to land on the
            // same group every time for the same DNA, including across a server restart.
            types.Sort(static (a, b) => string.CompareOrdinal(a.ID, b.ID));

            var antigens = new List<BloodAntigenPrototype>();

            foreach (var type in types)
            {
                foreach (var id in type.Antigens)
                {
                    if (_protos.TryIndex(id, out var antigen) && !antigens.Contains(antigen))
                        antigens.Add(antigen);
                }
            }

            // Card order first, then id, so two antigens that forgot to declare an order
            // still come out the same way on every machine.
            antigens.Sort(static (a, b) =>
            {
                var order = a.Order.CompareTo(b.Order);
                return order != 0 ? order : string.CompareOrdinal(a.ID, b.ID);
            });

            _antigensByReagent[reagent] = antigens;
        }
    }

    /// <summary>Every group built on this reagent, in stable order.</summary>
    public IReadOnlyList<BloodTypePrototype> GetTypes(ProtoId<ReagentPrototype> reagent)
    {
        return _typesByReagent.TryGetValue(reagent, out var types)
            ? types
            : Array.Empty<BloodTypePrototype>();
    }

    /// <summary>
    /// Every antigen a test could find in this reagent, left to right. This is what a test
    /// card draws its wells from, so a new antigen prototype shows up on the card by itself.
    /// </summary>
    public IReadOnlyList<BloodAntigenPrototype> GetAntigens(ProtoId<ReagentPrototype> reagent)
    {
        return _antigensByReagent.TryGetValue(reagent, out var antigens)
            ? antigens
            : Array.Empty<BloodAntigenPrototype>();
    }

    /// <summary>
    /// Whether this reagent is somebody's blood, and therefore whether transfusing it is a
    /// medical act rather than injecting a chemical.
    /// </summary>
    public bool IsBloodReagent(ProtoId<ReagentPrototype> reagent)
    {
        return _typesByReagent.ContainsKey(reagent);
    }

    // ── Reading a type ────────────────────────────────────────────────────────

    /// <summary>What this mob's veins are filled with, or null if it has no blood at all.</summary>
    public ProtoId<ReagentPrototype>? GetBloodReagent(Entity<BloodstreamComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return null;

        foreach (var (reagent, _) in ent.Comp.BloodReferenceSolution)
        {
            return reagent.Prototype;
        }

        return null;
    }

    /// <summary>
    /// This mob's blood group. Asked of the world first, then the component, and only then
    /// rolled from DNA — so an override always beats the roll and the roll is the default
    /// rather than something every mob prototype has to opt into.
    /// </summary>
    public ProtoId<BloodTypePrototype>? GetBloodType(Entity<BloodTypeComponent?> ent)
    {
        var ev = new GetBloodTypeEvent(ent.Owner);
        RaiseLocalEvent(ent.Owner, ref ev);

        if (ev.Type != null)
            return ev.Type;

        if (Resolve(ent, ref ent.Comp, false) && ent.Comp.Type != null)
            return ent.Comp.Type;

        return RollBloodType(ent.Owner);
    }

    /// <summary>The group stamped on a specific reagent, if it carries one.</summary>
    public ProtoId<BloodTypePrototype>? GetBloodType(ReagentId reagent)
    {
        if (reagent.Data == null)
            return null;

        foreach (var data in reagent.Data)
        {
            if (data is BloodTypeData blood)
                return blood.Type;
        }

        return null;
    }

    /// <summary>The kind of blood a solution holds the most of, ignoring which groups.</summary>
    public ProtoId<ReagentPrototype>? GetBloodReagent(Solution solution)
    {
        var totals = new Dictionary<string, FixedPoint2>();
        ProtoId<ReagentPrototype>? best = null;
        var most = FixedPoint2.Zero;

        foreach (var (reagent, quantity) in solution.Contents)
        {
            if (!IsBloodReagent(reagent.Prototype))
                continue;

            var total = totals.GetValueOrDefault(reagent.Prototype) + quantity;
            totals[reagent.Prototype] = total;

            if (total <= most)
                continue;

            best = reagent.Prototype;
            most = total;
        }

        return best;
    }

    /// <summary>
    /// Every antigen present anywhere in a solution's blood of one kind.
    /// </summary>
    /// <remarks>
    /// The union, not the majority. Two donors poured into one bag cannot be separated again,
    /// so what a recipient meets is everything in there at once — an O-negative half does not
    /// stop the A-positive half from being A-positive. This is what makes a mixed bag behave
    /// as its own worst component instead of as whichever donor happened to go in first.
    /// </remarks>
    public HashSet<ProtoId<BloodAntigenPrototype>> GetAntigens(Solution solution, ProtoId<ReagentPrototype> reagent)
    {
        var antigens = new HashSet<ProtoId<BloodAntigenPrototype>>();

        foreach (var (id, _) in solution.Contents)
        {
            if (id.Prototype == reagent && TryGetAntigens(GetBloodType(id), out var found))
                antigens.UnionWith(found);
        }

        return antigens;
    }

    /// <summary>The group carrying exactly this set of antigens, if one is written down.</summary>
    public ProtoId<BloodTypePrototype>? FindType(
        ProtoId<ReagentPrototype> reagent,
        IReadOnlySet<ProtoId<BloodAntigenPrototype>> antigens)
    {
        foreach (var type in GetTypes(reagent))
        {
            if (type.Antigens.SetEquals(antigens))
                return type.ID;
        }

        return null;
    }

    /// <summary>
    /// What a solution reads as. Unmixed blood answers with the group stamped on it; a bag
    /// two donors went into answers with whichever group carries both their antigens.
    /// </summary>
    /// <remarks>
    /// Unmixed blood is returned as-is rather than looked up, because two groups can share an
    /// antigen set — synthetic blood and O-negative both have none — and a bag of synthetic
    /// should keep saying so instead of being renamed to whichever match came first.
    /// </remarks>
    public ProtoId<BloodTypePrototype>? GetBloodType(Solution solution)
    {
        if (GetBloodReagent(solution) is not { } reagent)
            return null;

        ProtoId<BloodTypePrototype>? single = null;
        var seen = false;
        var uniform = true;

        foreach (var (id, _) in solution.Contents)
        {
            if (id.Prototype != reagent)
                continue;

            var type = GetBloodType(id);

            if (!seen)
            {
                single = type;
                seen = true;
            }
            else if (!Equals(single, type))
            {
                uniform = false;
                break;
            }
        }

        return uniform ? single : FindType(reagent, GetAntigens(solution, reagent));
    }

    /// <summary>Antigens on a group, or false when the group is unknown or unreadable.</summary>
    public bool TryGetAntigens(
        ProtoId<BloodTypePrototype>? type,
        [NotNullWhen(true)] out IReadOnlySet<ProtoId<BloodAntigenPrototype>>? antigens)
    {
        if (type != null && _protos.TryIndex(type.Value, out var proto))
        {
            antigens = proto.Antigens;
            return true;
        }

        antigens = null;
        return false;
    }

    /// <summary>
    /// The natural roll: a weighted pick keyed on DNA rather than on a die.
    /// </summary>
    /// <remarks>
    /// Keying it on DNA means the group is stable for the whole round without storing it,
    /// survives cloning the way it should, and follows a scrambled genome the way it should.
    /// A mob with no DNA falls back to its prototype, so every cow agrees with every other cow.
    /// </remarks>
    private ProtoId<BloodTypePrototype>? RollBloodType(EntityUid uid)
    {
        if (GetBloodReagent(uid) is not { } reagent)
            return null;

        var pool = GetTypes(reagent);
        var total = 0f;

        foreach (var type in pool)
        {
            total += MathF.Max(type.Weight, 0f);
        }

        if (total <= 0f)
            return null;

        var seed = CompOrNull<DnaComponent>(uid)?.DNA
                   ?? MetaData(uid).EntityPrototype?.ID
                   ?? string.Empty;

        var roll = StableHash(seed) % 1_000_000u / 1_000_000f * total;

        foreach (var type in pool)
        {
            roll -= MathF.Max(type.Weight, 0f);

            if (roll < 0f)
                return type.ID;
        }

        return pool[^1].ID;
    }

    /// <summary>
    /// FNV-1a. <see cref="string.GetHashCode()"/> is randomised per process, which would hand
    /// everybody a new blood group every time the server came up.
    /// </summary>
    private static uint StableHash(string value)
    {
        var hash = 2166136261u;

        foreach (var c in value)
        {
            hash = (hash ^ c) * 16777619u;
        }

        return hash;
    }

    // ── Sampling ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Finds the blood on something. A mob answers from its veins; anything else from
    /// whichever solution it is carrying the most blood in.
    /// </summary>
    /// <remarks>
    /// Deliberately not fussy about what it is looking at, so every way of reading blood —
    /// an analyser pressed to a patient, a strip somebody bled on, whatever gets written
    /// next — agrees about what counts as a sample.
    /// </remarks>
    public bool TryGetSample(EntityUid target, out BloodSample sample)
    {
        sample = default;

        if (TryComp<BloodstreamComponent>(target, out var bloodstream))
        {
            if (GetBloodReagent((target, bloodstream)) is not { } veins)
                return false;

            var type = GetBloodType(target);
            TryGetAntigens(type, out var antigens);

            sample = new BloodSample(veins, antigens ?? EmptyAntigens, type);
            return true;
        }

        Solution? best = null;
        ProtoId<ReagentPrototype>? kind = null;
        var most = FixedPoint2.Zero;

        foreach (var (_, soln) in _solutions.EnumerateSolutions(target))
        {
            var solution = soln.Comp.Solution;

            if (GetBloodReagent(solution) is not { } found)
                continue;

            var blood = solution.GetTotalPrototypeQuantity(found);

            if (blood <= most)
                continue;

            best = solution;
            most = blood;
            kind = found;
        }

        if (best == null || kind == null)
            return false;

        sample = new BloodSample(kind.Value, GetAntigens(best, kind.Value), GetBloodType(best));
        return true;
    }

    /// <summary>
    /// One well per antibody worth dropping on this kind of blood, in card order.
    /// </summary>
    /// <remarks>
    /// Driven by the sample's antigens rather than by its group, so a mixture with no name
    /// still lights up every well it should. Reading it off the name would quietly draw a
    /// nameless mixture as an all-negative card, which is a card that says "O negative".
    /// </remarks>
    public List<BloodTestWellState> BuildWells(BloodSample sample)
    {
        var wells = new List<BloodTestWellState>();

        foreach (var antigen in GetAntigens(sample.Reagent))
        {
            wells.Add(new BloodTestWellState(
                Loc.GetString(antigen.ShortName),
                Loc.GetString(antigen.Name),
                sample.Antigens.Contains(antigen.ID)));
        }

        return wells;
    }

    // ── Stamping ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes a group onto every blood reagent in a solution, leaving all other reagent data
    /// — DNA above all — exactly where it was.
    /// </summary>
    public void StampBloodType(Solution solution, ProtoId<BloodTypePrototype> type)
    {
        foreach (var content in solution.Contents)
        {
            if (!IsBloodReagent(content.Reagent.Prototype))
                continue;

            var data = content.Reagent.EnsureReagentData();
            data.RemoveAll(static entry => entry is BloodTypeData);
            data.Add(new BloodTypeData(type));
        }
    }

    // ── Compatibility ─────────────────────────────────────────────────────────

    /// <summary>Whether the recipient's immune system already knows this antigen.</summary>
    public bool IsSensitized(Entity<BloodTypeComponent?> ent, ProtoId<BloodAntigenPrototype> antigen)
    {
        return Resolve(ent, ref ent.Comp, false) && ent.Comp.Sensitized.Contains(antigen);
    }

    /// <summary>
    /// The verdict on donor blood of a named group.
    /// </summary>
    public BloodCompatibility GetCompatibility(
        EntityUid recipient,
        ProtoId<ReagentPrototype> donorReagent,
        ProtoId<BloodTypePrototype>? donorType)
    {
        TryGetAntigens(donorType, out var antigens);

        return GetCompatibility(
            recipient,
            donorReagent,
            antigens ?? EmptyAntigens,
            donorType);
    }

    /// <summary>
    /// The verdict on donor blood carrying a given set of antigens, and the reasoning behind it.
    /// </summary>
    /// <remarks>
    /// Antigens rather than a group are what this is decided on, because a mixed bag has a set
    /// but need not have a name. Deciding on the name would mean an unnameable mixture read as
    /// "unknown", and unknown blood is treated as a universal donor — which is the one wrong
    /// answer that gets somebody killed quietly.
    /// </remarks>
    public BloodCompatibility GetCompatibility(
        EntityUid recipient,
        ProtoId<ReagentPrototype> donorReagent,
        IReadOnlySet<ProtoId<BloodAntigenPrototype>> donorAntigens,
        ProtoId<BloodTypePrototype>? donorType = null)
    {
        var result = new BloodCompatibility();

        if (GetBloodReagent(recipient) is { } ours && ours != donorReagent)
        {
            // Not even the same substance. Nothing about groups applies.
            result.WrongSpecies = true;
            result.Compatible = false;
        }
        else
        {
            TryGetAntigens(GetBloodType(recipient), out var ourAntigens);

            foreach (var antigen in donorAntigens)
            {
                if (ourAntigens != null && ourAntigens.Contains(antigen))
                    continue;

                // An antigen the body already makes antibodies for is rejected now; one it has
                // never met is tolerated exactly once, and remembered.
                if (!_protos.TryIndex(antigen, out var proto) || proto.Preformed || IsSensitized(recipient, antigen))
                    result.Rejected.Add(antigen);
                else
                    result.Sensitizing.Add(antigen);
            }

            result.Compatible = result.Rejected.Count == 0;
        }

        var ev = new GetBloodCompatibilityEvent(recipient, donorType, result);
        RaiseLocalEvent(recipient, ref ev);

        return ev.Compatibility;
    }

    /// <summary>Records that a body has learned to recognise an antigen.</summary>
    public void Sensitize(EntityUid uid, ProtoId<BloodAntigenPrototype> antigen)
    {
        var comp = EnsureComp<BloodTypeComponent>(uid);

        if (!comp.Sensitized.Add(antigen))
            return;

        Dirty(uid, comp);

        var ev = new BloodSensitizedEvent(uid, antigen);
        RaiseLocalEvent(uid, ref ev);
    }

    // ── Transfusion ───────────────────────────────────────────────────────────

    /// <summary>
    /// Turns a donated solution into what actually reaches the veins, in place.
    /// </summary>
    /// <remarks>
    /// Rejected blood is not deleted — it is rewritten into hemolysate, keeping the donor's
    /// DNA and group. It no longer counts as blood, so the transfusion did nothing for the
    /// patient's volume, and it is poisonous until the kidneys clear it. Anything that is not
    /// blood at all passes through untouched, which is what keeps a bag of saline a bag of
    /// saline.
    /// </remarks>
    public void PrepareTransfusion(EntityUid recipient, Solution donated, EntityUid? source = null)
    {
        if (donated.Volume <= 0)
            return;

        var attempt = new BloodTransfusionAttemptEvent(recipient, donated, source);
        RaiseLocalEvent(recipient, ref attempt);

        var accepted = new Solution();
        var rejected = new Solution();
        var sensitizing = new HashSet<ProtoId<BloodAntigenPrototype>>();
        var verdicts = new Dictionary<string, BloodCompatibility>();

        foreach (var (reagent, quantity) in donated.Contents.ToArray())
        {
            if (!IsBloodReagent(reagent.Prototype))
                continue;

            // One verdict per kind of blood, reached on everything of that kind in the bag at
            // once. Judging each portion on its own would let a mixed bag be half accepted,
            // and half a transfusion is not what happens when you cannot unmix a bag.
            if (!verdicts.TryGetValue(reagent.Prototype, out var verdict))
            {
                verdict = attempt.Cancelled
                    ? new BloodCompatibility { Compatible = false }
                    : GetCompatibility(
                        recipient,
                        reagent.Prototype,
                        GetAntigens(donated, reagent.Prototype),
                        GetBloodType(donated));

                verdicts[reagent.Prototype] = verdict;
                sensitizing.UnionWith(verdict.Sensitizing);
            }

            if (verdict.Compatible)
            {
                accepted.AddReagent(reagent, quantity);
                continue;
            }

            // Each portion still hemolyses into whatever its own group says it should, so a
            // strange blood keeps failing strangely even when it failed as part of a mixture.
            donated.RemoveReagent(reagent, quantity);
            donated.AddReagent(new ReagentId(GetRejectedReagent(GetBloodType(reagent)), reagent.Data), quantity);
            rejected.AddReagent(reagent, quantity);
        }

        // Only on the way out: sensitising mid-loop would let the second reagent in the same
        // bag be rejected for an antigen the first one only just introduced.
        foreach (var antigen in sensitizing)
        {
            Sensitize(recipient, antigen);
        }

        if (accepted.Volume <= 0 && rejected.Volume <= 0)
            return;

        var ev = new BloodTransfusedEvent(recipient, accepted, rejected, source);
        RaiseLocalEvent(recipient, ref ev);
    }

    private ProtoId<ReagentPrototype> GetRejectedReagent(ProtoId<BloodTypePrototype>? type)
    {
        return type != null && _protos.TryIndex(type.Value, out var proto) && proto.RejectedReagent != null
            ? proto.RejectedReagent.Value
            : DefaultRejectedReagent;
    }

    // ── Keeping donated blood alive ───────────────────────────────────────────

    /// <summary>
    /// Blood that belongs in a body should not be digested by it.
    /// </summary>
    /// <remarks>
    /// Upstream already protects a mob's own blood, but it matches on the whole reagent id —
    /// donor DNA included — so anybody else's blood reads as a stray chemical and the heart
    /// eats it. We extend that protection to donated blood the patient actually accepts,
    /// which is what lets a transfusion hold and lets the donor's DNA stay findable in them.
    ///
    /// Only while they are short of blood, though. Once a patient is topped up the surplus is
    /// fair game again, so blood cannot be stockpiled in a vein — and anything that lives off
    /// drinking the stuff still digests what it drinks.
    /// </remarks>
    private void OnMetabolismExclusion(Entity<BloodTypeComponent> ent, ref MetabolismExclusionEvent args)
    {
        if (!TryComp<BloodstreamComponent>(ent, out var bloodstream))
            return;

        var reference = bloodstream.BloodReferenceSolution;

        if (!_solutions.TryGetSolution(ent.Owner, bloodstream.BloodSolutionName, out _, out var blood)
            || reference.Volume <= 0
            || blood.Volume >= reference.Volume)
        {
            return;
        }

        foreach (var (reagent, _) in blood.Contents)
        {
            if (args.Reagents.Contains(reagent) || !reference.ContainsPrototype(reagent.Prototype))
                continue;

            if (GetCompatibility(ent.Owner, reagent.Prototype, GetBloodType(reagent)).Compatible)
                args.Reagents.Add(reagent);
        }
    }
}
