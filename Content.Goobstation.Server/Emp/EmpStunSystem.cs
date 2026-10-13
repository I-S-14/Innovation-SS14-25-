// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Emp;
using Content.Shared._EinsteinEngines.Silicon.Components;
using Content.Shared.Emp;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Stunnable;
using Content.Shared._IS14.Cyborgs.Components; // IS14-change

namespace Content.Goobstation.Server.Emp;

public sealed class EmpStunSystem : EntitySystem
{
    [Dependency] private readonly SharedStunSystem _stun = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SiliconComponent, EmpPulseEvent>(OnEmpParalyze);
        SubscribeLocalEvent<BorgChassisComponent, EmpPulseEvent>(OnEmpParalyze);
        //IS14-change start - tgstation-style cyborgs
        SubscribeLocalEvent<CyborgComponent, EmpPulseEvent>(OnEmpParalyze);
        //IS14-change end
    }

    private void OnEmpParalyze(EntityUid uid, Component component, ref EmpPulseEvent args)
    {
        args.Affected = true;
        args.Disabled = true;
        var duration = args.Duration;
        if (duration > TimeSpan.FromSeconds(15))
            duration = TimeSpan.FromSeconds(15);
        _stun.TryUpdateParalyzeDuration(uid, duration);
    }
}
