// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Goobstation.Maths.FixedPoint;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.DoAfter;
using Content.Shared.Fluids;
using Content.Shared.Popups;
using Content.Shared.Verbs;
using Robust.Shared.Network;

namespace Content.Shared._IS14.Cyborgs.Systems;

public sealed class CyborgTankDumpSystem : EntitySystem
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedDoAfterSystem _doAfter = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedPuddleSystem _puddle = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solution = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgTankDumpComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CyborgTankDumpComponent, CyborgTankDumpDoAfterEvent>(OnDoAfter);
    }

    // An alternative verb, so alt+Z on the held tool empties it.
    private void OnGetVerbs(Entity<CyborgTankDumpComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || !HasContents(ent))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cyborg-tank-dump-verb"),
            Act = () => StartDump(ent, user),
            Priority = 3,
        });
    }

    private void StartDump(Entity<CyborgTankDumpComponent> ent, EntityUid user)
    {
        var args = new DoAfterArgs(EntityManager, user, ent.Comp.Delay, new CyborgTankDumpDoAfterEvent(), ent, ent)
        {
            BreakOnMove = true,
            BreakOnDamage = true,
        };

        if (_doAfter.TryStartDoAfter(args))
            _popup.PopupClient(Loc.GetString("cyborg-tank-dump-start", ("tool", ent.Owner)), user, user);
    }

    private void OnDoAfter(Entity<CyborgTankDumpComponent> ent, ref CyborgTankDumpDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        args.Handled = true;

        // Spilling puddles is server only.
        if (_net.IsClient ||
            !_solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out var soln, out var solution) ||
            solution.Volume <= FixedPoint2.Zero)
            return;

        var spilled = _solution.SplitSolution(soln.Value, solution.Volume);
        _puddle.TrySpillAt(Transform(args.User).Coordinates, spilled, out _);
        _popup.PopupEntity(Loc.GetString("cyborg-tank-dump-done", ("tool", ent.Owner)), args.User, args.User);
    }

    private bool HasContents(Entity<CyborgTankDumpComponent> ent)
    {
        return _solution.TryGetSolution(ent.Owner, ent.Comp.Solution, out _, out var solution) &&
            solution.Volume > FixedPoint2.Zero;
    }
}
