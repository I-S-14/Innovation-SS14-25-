// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Client._IS14.Research;

/// <summary>
/// Shows which way this machine faces when examined. <seealso cref="IS14FacingArrowSystem"/>
/// </summary>
/// <remarks>
/// Client-only, like Clickable or InteractionOutline: the arrow is presentation and the server has
/// no business knowing about it. That means the name has to be listed in
/// <c>Content.Server/Entry/IgnoredComponents.cs</c>, or the server dies on prototype load —
/// exactly how the engine itself ignores Sprite and AnimationPlayer.
/// </remarks>
[RegisterComponent]
public sealed partial class IS14FacingArrowComponent : Component
{
    /// <summary>Effect spawned in front of the machine. Despawns on its own.</summary>
    [DataField]
    public EntProtoId Arrow = "IS14FacingArrowEffect";
}
