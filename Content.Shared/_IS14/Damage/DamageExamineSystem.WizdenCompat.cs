// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Utility;

namespace Content.Shared.Damage.Systems;

/// <summary>
///     Wizden's examine API grew a stamina argument; our (Goob) damage examine predates it.
/// </summary>
public sealed partial class DamageExamineSystem
{
    public void AddDamageExamine(FormattedMessage message, DamageSpecifier damageSpecifier, string? type, float? stamina)
    {
        AddDamageExamine(message, damageSpecifier, type);

        if (stamina is null)
            return;

        message.PushNewline();
        message.AddMarkupOrThrow(Loc.GetString("damage-value-stamina", ("amount", stamina)));
    }
}
