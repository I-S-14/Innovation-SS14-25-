// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Medical.Organs;

namespace Content.Client._IS14.Medical.Organs;

/// <summary>
/// Client half of organ function: arithmetic only.
/// </summary>
/// <remarks>
/// Levels arrive over the network already computed. What the client needs from the shared base
/// is the per-organ efficiency, which it works out itself from the organ's own state so that
/// an analyser pointed at a patient stays live between scans.
/// </remarks>
public sealed class OrganFunctionSystem : SharedOrganFunctionSystem;
