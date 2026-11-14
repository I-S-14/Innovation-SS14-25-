// SPDX-License-Identifier: MIT
using System.Diagnostics.CodeAnalysis;
using Robust.Shared.Utility;
namespace Content.Shared.DeviceNetwork;

/// <summary>
/// A data class for information passing through a Device Network.
/// </summary>
[ImplicitDataRecord]
public partial interface INetworkPayload;
