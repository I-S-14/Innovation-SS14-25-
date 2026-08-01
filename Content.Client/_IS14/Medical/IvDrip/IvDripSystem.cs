// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._IS14.Medical.IvDrip;

namespace Content.Client._IS14.Medical.IvDrip;

/// <summary>
/// The client half of a drip stand. Empty on purpose: everything it needs is in the
/// shared system, and it exists so the client has it at all — the cursor asks whether a
/// stand may be dropped on a patient before the server ever hears about the drag.
/// </summary>
public sealed class IvDripSystem : SharedIvDripSystem;
