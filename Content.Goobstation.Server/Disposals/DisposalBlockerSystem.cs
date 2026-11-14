using Content.Shared.Disposal.Unit;
using Content.Goobstation.Server.Disposals.Tube;
using Content.Shared.Disposal.Holder;
using Content.Shared.Disposal.Tube;

namespace Content.Goobstation.Server.Disposals;

/// <summary>Forces a blocked tube's holder toward its first exit, retaining the Goob one-way barrier.</summary>
public sealed partial class DisposalBlockerSystem : EntitySystem
{
    [Dependency] private DisposalTubeSystem _tubes = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DisposalBlockerComponent, GetDisposalsNextDirectionEvent>(OnNext,
            before: new[] { typeof(DisposalTubeSystem) });
    }

    private void OnNext(Entity<DisposalBlockerComponent> ent, ref GetDisposalsNextDirectionEvent args)
    {
        if (!TryComp<DisposalTubeComponent>(ent, out var tube))
            return;
        var exits = _tubes.GetTubeConnectableDirections((ent.Owner, tube));
        if (exits.Length == 0)
            return;
        args.Next = exits[0];
        args.Handled = true;
    }
}
