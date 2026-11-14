// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared.DeviceLinking.Components;
using Robust.Shared.Prototypes;

namespace Content.Shared.DeviceLinking.Systems;

/// <summary>
///     Goob automation (robotic arms, factory slots, fillbots) adds and removes ports at runtime.
///     Wizden's refactored DeviceLinkSystem only adds them; these keep the removal API.
/// </summary>
public sealed partial class DeviceLinkSystem
{
    /// <summary>
    ///     Goobstation - Removes a port from a source.
    /// </summary>
    public void RemoveSourcePort(EntityUid uid, ProtoId<SourcePortPrototype> port)
    {
        if (!TryComp<DeviceLinkSourceComponent>(uid, out var comp))
            return;

        comp.Ports.Remove(port);
        if (comp.Ports.Count == 0)
            RemCompDeferred<DeviceLinkSourceComponent>(uid);
        else
            Dirty(uid, comp);
    }

    /// <summary>
    ///     Goobstation - Removes a port from a sink.
    /// </summary>
    public void RemoveSinkPort(EntityUid uid, ProtoId<SinkPortPrototype> port)
    {
        if (!TryComp<DeviceLinkSinkComponent>(uid, out var comp))
            return;

        comp.Ports.Remove(port);
        if (comp.Ports.Count == 0)
            RemCompDeferred<DeviceLinkSinkComponent>(uid);
        else
            Dirty(uid, comp);
    }

    /// <summary>
    ///     Goobstation - Removes every link from a source, optionally only to sinks matching a filter.
    /// </summary>
    public void RemoveAllFromSource(EntityUid sourceUid, DeviceLinkSourceComponent? sourceComponent = null, Predicate<EntityUid>? filter = null)
    {
        if (!Resolve(sourceUid, ref sourceComponent, false))
            return;

        foreach (var sinkUid in sourceComponent.LinkedPorts.Keys.Where(sink => filter == null || filter(sink)).ToList())
        {
            RemoveSinkFromSource((sourceUid, sourceComponent), sinkUid);
        }
    }
}
