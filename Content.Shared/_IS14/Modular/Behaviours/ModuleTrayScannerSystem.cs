// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Eye;
using Content.Shared.SubFloor;
using Robust.Shared.Network;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Puts a t-ray on the chassis and the subfloor mask on whoever is wearing it.
///
///     Both halves are needed and neither is enough. The scanner on the chassis is what
///     the client's sweep finds when it walks the player's slots; the mask is what makes
///     the pipes and cables render once found. Upstream ties the two together through the
///     equip events on a handheld scanner, and a module installed into a suit somebody is
///     already wearing never raises those.
/// </summary>
public sealed class ModuleTrayScannerSystem : ModuleBehaviourSystem<ModuleTrayScannerComponent>
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedEyeSystem _eye = default!;

    protected override bool RequiresActive(Entity<ModuleTrayScannerComponent> ent) => true;

    protected override void Start(Entity<ModuleTrayScannerComponent> ent, EntityUid chassis)
    {
        Stop(ent, chassis);

        var scanner = EnsureComp<TrayScannerComponent>(chassis);
        scanner.Range = ent.Comp.Range;
        scanner.Enabled = true;
        Dirty(chassis, scanner);

        ent.Comp.Scanning = chassis;

        AddViewer(ent, GetChassisUser(chassis));
    }

    protected override void Stop(Entity<ModuleTrayScannerComponent> ent, EntityUid chassis)
    {
        if (ent.Comp.Scanning is { } scanning)
        {
            ent.Comp.Scanning = null;

            if (!TerminatingOrDeleted(scanning))
                RemComp<TrayScannerComponent>(scanning);
        }

        RemoveViewer(ent);
    }

    /// <summary>
    ///     The suit changing hands has to move the mask with it, or the previous wearer
    ///     keeps seeing through floors from across the station.
    /// </summary>
    protected override void UserChanged(Entity<ModuleTrayScannerComponent> ent, EntityUid chassis, EntityUid? user)
    {
        if (ent.Comp.Scanning == null)
            return;

        RemoveViewer(ent);
        AddViewer(ent, user);
    }

    /// <summary>
    ///     Joins the count upstream already keeps, so a wearer carrying a handheld scanner
    ///     as well does not lose the mask when one of the two stops.
    /// </summary>
    private void AddViewer(Entity<ModuleTrayScannerComponent> ent, EntityUid? user)
    {
        // The mask lives on the eye, which is server-authoritative; the client is told
        // about it rather than deciding it.
        if (_net.IsClient || user is not { } uid || TerminatingOrDeleted(uid))
            return;

        var viewer = EnsureComp<TrayScannerUserComponent>(uid);
        viewer.Count++;
        ent.Comp.Viewer = uid;

        if (viewer.Count == 1)
            _eye.RefreshVisibilityMask(uid);
    }

    private void RemoveViewer(Entity<ModuleTrayScannerComponent> ent)
    {
        if (ent.Comp.Viewer is not { } uid)
            return;

        ent.Comp.Viewer = null;

        if (_net.IsClient || TerminatingOrDeleted(uid) || !TryComp<TrayScannerUserComponent>(uid, out var viewer))
            return;

        viewer.Count--;

        if (viewer.Count > 0)
            return;

        RemComp<TrayScannerUserComponent>(uid);
        _eye.RefreshVisibilityMask(uid);
    }
}
