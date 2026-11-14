// SPDX-License-Identifier: MIT

namespace Content.Shared.Sound;

public abstract partial class SharedSpamEmitSoundRequirePowerSystem : EntitySystem
{
    [Dependency] protected SharedEmitSoundSystem EmitSound = default!;
}
