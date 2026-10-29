// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Speech.EntitySystems;
using Robust.Shared.GameStates;

namespace Content.Shared.Speech.Components;

/// <summary>
/// Mios dios!
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(SpanishAccentSystem))]
public sealed partial class SpanishAccentComponent : BaseAccentComponent;
