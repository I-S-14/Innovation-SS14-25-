// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Client.Graphics;
using Robust.Shared.Timing;

namespace Content.Client._IS14.Cord;

/// <summary>Hangs the cord overlay for as long as the client is running.</summary>
public sealed class CordOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlay = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        _overlay.AddOverlay(new CordOverlay(EntityManager, _timing));
    }

    public override void Shutdown()
    {
        base.Shutdown();

        _overlay.RemoveOverlay<CordOverlay>();
    }
}
