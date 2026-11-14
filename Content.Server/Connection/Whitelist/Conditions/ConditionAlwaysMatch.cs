// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Threading.Tasks;
using Robust.Shared.Network;
namespace Content.Server.Connection.Whitelist.Conditions;

/// <summary>
/// Condition that always matches
/// </summary>
public sealed partial class ConditionAlwaysMatch : WhitelistCondition
{

}