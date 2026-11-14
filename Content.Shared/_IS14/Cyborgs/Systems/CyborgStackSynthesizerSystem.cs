// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.PowerCell;
using Content.Shared.Stacks;
using Robust.Shared.Network;
using Robust.Shared.Timing;

namespace Content.Shared._IS14.Cyborgs.Systems;

/// <summary>
/// Refills cyborg stack tools from the cyborg's power cell and keeps them from running out.
/// </summary>
public sealed class CyborgStackSynthesizerSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgStackSynthesizerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CyborgStackSynthesizerComponent, StackCountChangedEvent>(OnCountChanged);
    }

    private void OnMapInit(Entity<CyborgStackSynthesizerComponent> ent, ref MapInitEvent args)
    {
        if (TryComp<StackComponent>(ent, out var stack))
            _stack.SetCount((ent.Owner, stack), _stack.GetMaxCount(stack));

        ent.Comp.NextRegen = _timing.CurTime + ent.Comp.RegenDelay;
    }

    // The stack system deletes an empty stack right after raising this event,
    // putting one unit back first keeps the tool alive.
    private void OnCountChanged(Entity<CyborgStackSynthesizerComponent> ent, ref StackCountChangedEvent args)
    {
        if (args.NewCount > 0 || !TryComp<StackComponent>(ent, out var stack))
            return;

        TryPay(ent);
        _stack.SetCount((ent.Owner, stack), 1);
    }

    private bool TryPay(Entity<CyborgStackSynthesizerComponent> ent)
    {
        if (!TryComp<CyborgToolComponent>(ent, out var tool) || tool.Cyborg is not { } cyborg)
            return false;

        return _powerCell.TryUseCharge(cyborg, ent.Comp.ChargePerUnit);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        // Charge isn't networked, so the refill is server only and the count is synced.
        if (_net.IsClient)
            return;

        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<CyborgStackSynthesizerComponent, StackComponent>();
        while (query.MoveNext(out var uid, out var synth, out var stack))
        {
            if (curTime < synth.NextRegen)
                continue;

            synth.NextRegen = curTime + synth.RegenDelay;

            if (stack.Count >= _stack.GetMaxCount(stack))
                continue;

            if (TryPay((uid, synth)))
                _stack.SetCount((uid, stack), stack.Count + 1);
        }
    }
}
