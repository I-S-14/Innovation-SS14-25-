// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._IS14.Cyborgs.Components;
using System.Linq;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Interaction.Events;
using Content.Shared.Nutrition.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Verbs;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._IS14.Cyborgs.Systems;

/// <summary>
/// Fills cyborg hyposprays and shakers with the chosen reagent, paid from the cyborg's power cell.
/// </summary>
public sealed class CyborgReagentSynthesizerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    private static readonly VerbCategory SynthesizeCategory = new("cyborg-reagent-verb-category", null);

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgReagentSynthesizerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CyborgReagentSynthesizerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);

        // Before the injector, which would toggle the hypospray mode, and before drinking from the shaker.
        SubscribeLocalEvent<CyborgReagentSynthesizerComponent, UseInHandEvent>(OnUseInHand,
            before: [typeof(InjectorSystem), typeof(OpenableSystem), typeof(IngestionSystem)]);
        Subs.BuiEvents<CyborgReagentSynthesizerComponent>(CyborgReagentUiKey.Key, subs =>
        {
            subs.Event<CyborgSelectReagentMessage>(OnSelectMessage);
        });
    }

    // Like tg's borghypo: using it opens the reagent picker.
    private void OnUseInHand(Entity<CyborgReagentSynthesizerComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        args.ApplyDelay = false;
        _ui.TryToggleUi(ent.Owner, CyborgReagentUiKey.Key, args.User);
    }

    private void OnSelectMessage(Entity<CyborgReagentSynthesizerComponent> ent, ref CyborgSelectReagentMessage args)
    {
        if (!TryComp<CyborgToolComponent>(ent, out var tool) || tool.Cyborg != args.Actor)
            return;

        if (!GetAvailableReagents(ent, tool).Contains(args.Reagent))
            return;

        Select(ent, args.Reagent, args.Actor);
    }

    /// <summary>
    /// Reagents the owner can pick right now, emag ones included once unlocked.
    /// </summary>
    public List<ProtoId<ReagentPrototype>> GetAvailableReagents(Entity<CyborgReagentSynthesizerComponent> ent)
    {
        return TryComp<CyborgToolComponent>(ent, out var tool)
            ? GetAvailableReagents(ent, tool).ToList()
            : ent.Comp.Reagents.ToList();
    }

    private void OnMapInit(Entity<CyborgReagentSynthesizerComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Selected == null && ent.Comp.Reagents.Count > 0)
        {
            ent.Comp.Selected = ent.Comp.Reagents[0];
            Dirty(ent);
        }

        ent.Comp.NextRegen = _timing.CurTime + ent.Comp.RegenDelay;
    }

    private void OnGetVerbs(Entity<CyborgReagentSynthesizerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !TryComp<CyborgToolComponent>(ent, out var tool) ||
            tool.Cyborg != args.User)
            return;

        var user = args.User;
        foreach (var reagent in GetAvailableReagents(ent, tool))
        {
            if (!_proto.TryIndex(reagent, out var proto))
                continue;

            args.Verbs.Add(new AlternativeVerb
            {
                Text = proto.LocalizedName,
                Category = SynthesizeCategory,
                Disabled = ent.Comp.Selected == reagent,
                Act = () => Select(ent, reagent, user),
            });
        }
    }

    private IEnumerable<ProtoId<ReagentPrototype>> GetAvailableReagents(
        Entity<CyborgReagentSynthesizerComponent> ent,
        CyborgToolComponent tool)
    {
        foreach (var reagent in ent.Comp.Reagents)
        {
            yield return reagent;
        }

        if (!TryComp<CyborgComponent>(tool.Cyborg, out var cyborg) || !cyborg.EmagToolsUnlocked)
            yield break;

        foreach (var reagent in ent.Comp.EmagReagents)
        {
            yield return reagent;
        }
    }

    /// <summary>
    /// Switches to another reagent. What was synthesized so far is flushed.
    /// </summary>
    public void Select(Entity<CyborgReagentSynthesizerComponent> ent, ProtoId<ReagentPrototype> reagent, EntityUid? user)
    {
        if (ent.Comp.Selected == reagent)
            return;

        ent.Comp.Selected = reagent;
        Dirty(ent);

        if (_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out var solution, out _))
            _solution.RemoveAllSolution(solution.Value);

        if (user != null && _proto.TryIndex(reagent, out var proto))
            _popup.PopupClient(Loc.GetString("cyborg-reagent-selected", ("reagent", proto.LocalizedName)), ent, user.Value);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Charge isn't networked, the solution is synced from the server.
        if (_net.IsClient)
            return;

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<CyborgReagentSynthesizerComponent, CyborgToolComponent>();
        while (query.MoveNext(out var uid, out var synth, out var tool))
        {
            if (curTime < synth.NextRegen)
                continue;

            synth.NextRegen = curTime + synth.RegenDelay;

            if (synth.Selected is not { } reagent || tool.Cyborg is not { } cyborg)
                continue;

            if (!_solution.TryGetSolution(uid, synth.Solution, out var solutionEnt, out var solution) ||
                solution.AvailableVolume <= 0)
                continue;

            var amount = FixedPoint2.Min(synth.RegenAmount, solution.AvailableVolume);
            if (!_powerCell.TryUseCharge(cyborg, synth.ChargePerUnit * amount.Float()))
                continue;

            _solution.TryAddReagent(solutionEnt.Value, reagent, amount);
        }
    }
}
