// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Shared.DeviceLinking.Events;
using Content.Shared.Singularity.Components;
using Content.Shared.Singularity.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using System.Numerics;
using System.Threading;
using Content.Server.Administration.Logs;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Projectiles;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Lock;
using Content.Shared.Popups;
using Content.Shared.Power;
using Content.Shared.Projectiles;
using Robust.Shared.Map;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server.Singularity.EntitySystems;

/// <inheritdoc/>
public sealed partial class EmitterSystem : SharedEmitterSystem
{
    [SubscribeLocalEvent]
    private void OnSignalReceived(Entity<EmitterComponent> ent, ref SignalReceivedEvent args)
    {
        if (ent.Comp.SetTypePorts.TryGetValue(args.Port, out var boltType)
            && TryComp<NetworkPoweredAmmoProviderComponent>(ent, out var ammoProvider))
        {
            ammoProvider.Prototype = boltType;
            Dirty(ent, ammoProvider);
        }
    }
}
