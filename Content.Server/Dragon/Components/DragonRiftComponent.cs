// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Dragon;
using Robust.Shared.Prototypes;
using Content.Shared.EntityTable.EntitySelectors;

namespace Content.Server.Dragon;

// TODO: replace accumulators with timespan logic
[RegisterComponent]
public sealed partial class DragonRiftComponent : SharedDragonRiftComponent
{
    /// <summary>
    /// Dragon that spawned this rift.
    /// </summary>
    [DataField]
    public EntityUid? Dragon;

    /// <summary>
    /// How long the rift has been active.
    /// </summary>
    [DataField]
    public float Accumulator = 0f;

    /// <summary>
    /// The maximum amount we can accumulate before becoming impervious.
    /// </summary>
    [DataField("maxAccumualator")] // load bearing typo...
    public float MaxAccumulator = 300f;

    /// <summary>
    /// Accumulation of the spawn timer.
    /// </summary>
    [DataField]
    public float SpawnAccumulator = 30f;

    /// <summary>
    /// How long it takes for a new spawn to be added.
    /// </summary>
    [ViewVariables(VVAccess.ReadWrite), DataField("spawnCooldown")]
    public float SpawnCooldown = 24f; // Goobstation

    // Goobstation - Buff carp rift (nuked string proto)
    [DataField("spawn")]
    public EntProtoId SpawnPrototype = "MobCarpDragon";

    // <Goobstation> - Buff carp rift
    [DataField]
    public float StrongSpawnChance = 0.15f;

    [DataField("spawnStrong")]
    public EntProtoId SpawnPrototypeStrong = "MobSharkDragon";
    // </Goobstation>
}
