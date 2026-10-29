// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading.Tasks;
using Content.Server.Database;
using Robust.Shared.Network;
namespace Content.Server.Connection.Whitelist.Conditions;

/// <summary>
/// Condition that matches if the player is in the manual blacklist.
/// </summary>
public sealed partial class ConditionManualBlacklistMembership : WhitelistCondition
{
}