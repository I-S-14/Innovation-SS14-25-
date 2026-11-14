// SPDX-License-Identifier: MIT

using Content.Shared.Whitelist;

namespace Content.Shared.Warps;

public sealed partial class WarpPointComponent
{
    /// <summary>
    /// Goob warp points can exclude themselves from living players' teleport menus.
    /// This is independent of each teleporter's Wizards whitelist and blacklist.
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;
}
