// SPDX-License-Identifier: AGPL-3.0-or-later
using Content.Shared.Damage.Systems;
using Content.Server.Administration.Logs;
using Content.Server.Weapons.Ranged.Systems;
using Content.Shared.Camera;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Database;
using Content.Shared.Effects;
using Content.Shared.Mobs.Components;
using Content.Shared.Throwing;
using Robust.Shared.Physics.Components;
using Robust.Shared.Player;

namespace Content.Server.Damage.Systems;

// Trauma - moved everything here to shared
public sealed class DamageOtherOnHitSystem : SharedDamageOtherOnHitSystem;
