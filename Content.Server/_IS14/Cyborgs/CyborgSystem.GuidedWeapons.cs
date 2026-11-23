// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Linq;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.DeviceLinking.Systems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Shared.Containers;

namespace Content.Server._IS14.Cyborgs;

/// <summary>Links a built-in range finder to the launcher supplied by the same upgrade, not another borg.</summary>
public sealed partial class CyborgSystem
{
    [Dependency] private readonly DeviceLinkSystem _guidedLinks = default!;

    protected override void OnInserted(Entity<CyborgComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        base.OnInserted(ent, ref args);
        if (!TryComp<CyborgToolComponent>(args.Entity, out var inserted) || inserted.SourceUpgrade is not { } board ||
            (!HasComp<TargetFinderComponent>(args.Entity) && !HasComp<TargetAssignComponent>(args.Entity)))
            return;

        var tools = EnumerateTools(ent).Where(tool =>
            Comp<CyborgToolComponent>(tool).SourceUpgrade == board).ToArray();
        var launcher = tools.FirstOrDefault(HasComp<TargetAssignComponent>);
        if (!launcher.IsValid())
            return;

        foreach (var tool in tools)
        {
            if (!TryComp<TargetFinderComponent>(tool, out var finder))
                continue;
            var sinks = _guidedLinks.GetLinkedSinks(tool, finder.LinkingPort);
            if (finder.TargetAssigner == launcher && sinks.SetEquals(new[] { launcher }))
                continue;
            foreach (var sink in sinks)
                _guidedLinks.RemoveSinkFromSource(tool, sink);
            _guidedLinks.LinkDefaults(null, tool, launcher);
        }
    }
}
