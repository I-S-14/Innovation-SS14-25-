// SPDX-License-Identifier: MIT
using Content.Server.Body.Systems;
using Content.Shared.Atmos;
using Content.Shared.Disposal.Unit;
using Content.Server.Atmos.EntitySystems;

namespace Content.Server.Disposal.Unit;

/// <inheritdoc/>
public sealed partial class BeingDisposedSystem : SharedBeingDisposedSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<BeingDisposedComponent, InhaleLocationEvent>(OnInhaleLocation);
        SubscribeLocalEvent<BeingDisposedComponent, ExhaleLocationEvent>(OnExhaleLocation);
        SubscribeLocalEvent<BeingDisposedComponent, AtmosExposedGetAirEvent>(OnGetAir);
    }

    private void OnGetAir(Entity<BeingDisposedComponent> ent, ref AtmosExposedGetAirEvent args)
    {
        if (TryComp<DisposalHolderComponent>(ent.Comp.Holder, out var holder))
        {
            args.Gas = holder.Air;
            args.Handled = true;
        }
    }

    private void OnInhaleLocation(EntityUid uid, BeingDisposedComponent component, ref InhaleLocationEvent args) // Goob - by-ref
    {
        if (TryComp<DisposalHolderComponent>(ent.Comp.Holder, out var holder))
        {
            args.Gas = holder.Air;
        }
    }

    private void OnExhaleLocation(EntityUid uid, BeingDisposedComponent component, ref ExhaleLocationEvent args) // Goob - by-ref
    {
        if (TryComp<DisposalHolderComponent>(ent.Comp.Holder, out var holder))
        {
            args.Gas = holder.Air;
        }
    }
}
