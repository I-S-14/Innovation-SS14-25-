// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Server.Administration.Logs;
using Content.Server.Pointing.Components;
using Content.Shared.Database;
using Content.Shared.Examine;
using Content.Shared.Eye;
using Content.Shared.Ghost;
using Content.Shared.Holopad;
using Content.Shared.IdentityManagement;
using Content.Shared.Input;
using Content.Shared.Interaction;
using Content.Shared.Inventory;
using Content.Shared.Mind;
using Content.Shared.Pointing;
using Content.Shared.Popups;
using Content.Shared.Telephone;
using JetBrains.Annotations;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.GameStates;
using Robust.Shared.Input.Binding;
using Robust.Shared.Map;
using Content.Shared.CCVar;
using Robust.Shared.Player;
using Robust.Shared.Replays;
using Robust.Shared.Timing;

namespace Content.Server.Pointing.EntitySystems;

[UsedImplicitly]
internal sealed partial class PointingSystem : SharedPointingSystem // Goobstation: Partial Class
{
    [Dependency] private readonly IConfigurationManager _config = default!;
    [Dependency] private readonly IReplayRecordingManager _replay = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinitionManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly RotateToFaceSystem _rotateToFaceSystem = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly VisibilitySystem _visibilitySystem = default!;
    [Dependency] private readonly SharedMindSystem _minds = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private IAdminLogManager _adminLogger = default!;

    protected override bool ShouldPointingArrowGoRogue()
    {
        return EntityQuery<PointingArrowAngeringComponent>().FirstOrDefault() != null;
    }

    protected override void AfterPoint(
        EntityUid player,
        MapCoordinates mapCoordsPointed,
        EntityUid pointed)
    {
        if (session?.AttachedEntity is not { } player)
        {
            Log.Warning($"Player {session} attempted to point without any attached entity");
            return false;
        }

        if (!coordsPointed.IsValid(EntityManager))
        {
            Log.Warning($"Player {ToPrettyString(player)} attempted to point at invalid coordinates: {coordsPointed}");
            return false;
        }

        if (_pointers.TryGetValue(session, out var lastTime) &&
            _gameTiming.CurTime < lastTime + _pointDelay)
        {
            return false;
        }

        if (HasComp<PointingArrowComponent>(pointed))
        {
            // this is a pointing arrow. no pointing here...
            return false;
        }

        // Goobstation Begin: Disallow pointing at stealthed entities.
        if (!CanPointAtStealthedEntity(player, pointed))
        {
            return false;
        }

        // This is probably an entity relaying through holopad i.e. Station AI. Redirect the verb.
        var relayPointer = EntityUid.Invalid; // need this to check for point origin later. Invalid (zero) if original entity is the one pointing.

        if (TryComp<HolopadUserComponent>(player, out var holopadComp))
        {
            var stationAi = holopadComp.LinkedHolopads.FirstOrDefault(); // This is the station ai square, not brain.
            if (!TryComp<TelephoneComponent>(stationAi, out var telephoneComp))
                return false;

            if (telephoneComp.LinkedTelephones.Count != 1) // unsupported and i cba dealing with this.
                return false;

            player = telephoneComp.LinkedTelephones.Single();// yo if you're reviewing here doublecheck this doesnt fuck shit up and remove this comment if it doesnt thanks.
            relayPointer = player;
        }
        // Goobstation End.

        if (!CanPoint(player))
        {
            return false;
        }

        if (!InRange(player, coordsPointed))
        {
            _popup.PopupEntity(Loc.GetString("pointing-system-try-point-cannot-reach"), player, player);
            return false;
        }
        var mapCoordsPointed = _transform.ToMapCoordinates(coordsPointed);
        _rotateToFaceSystem.TryFaceCoordinates(player, mapCoordsPointed.Position);

        var arrow = Spawn("PointingArrow", coordsPointed);

        if (TryComp<PointingArrowComponent>(arrow, out var pointing))
        {
            if (relayPointer == EntityUid.Invalid) // Goobstation. The reason im doing it this way is due to upstreaming gripes. easier to merge.
                pointing.StartPosition = _transform.ToCoordinates((arrow, Transform(arrow)), _transform.ToMapCoordinates(Transform(player).Coordinates)).Position;
            else // Goobstation start
                pointing.StartPosition = _transform.ToCoordinates((arrow, Transform(arrow)), _transform.ToMapCoordinates(Transform(relayPointer).Coordinates)).Position; // Goobstation end
            pointing.EndTime = _gameTiming.CurTime + PointDuration;

            Dirty(arrow, pointing);
        }

        if (EntityQuery<PointingArrowAngeringComponent>().FirstOrDefault() != null)
        {
            if (TryComp<PointingArrowComponent>(arrow, out var pointingArrowComponent))
            {
                pointingArrowComponent.Rogue = true;
            }
        }

        var layer = (int) VisibilityFlags.Normal;
        if (TryComp(player, out VisibilityComponent? playerVisibility))
        {
            var arrowVisibility = EnsureComp<VisibilityComponent>(arrow);
            layer = playerVisibility.Layer;
            _visibilitySystem.SetLayer((arrow, arrowVisibility), (ushort) layer);
        }

        // Get players that are in range and whose visibility layer matches the arrow's.
        bool ViewerPredicate(ICommonSession playerSession)
        {
            if (!_minds.TryGetMind(playerSession, out _, out var mind) ||
                mind.CurrentEntity is not { Valid: true } ent ||
                !TryComp(ent, out EyeComponent? eyeComp) ||
                (eyeComp.VisibilityMask & layer) == 0)
                return false;

            return _transform.GetMapCoordinates(ent).InRange(_transform.GetMapCoordinates(player), PointingRange);
        }

        var viewers = Filter.Empty()
            .AddWhere(session1 => ViewerPredicate(session1))
            .Recipients;

        string selfMessage;
        string viewerMessage;
        string? viewerPointedAtMessage = null;
        var playerName = Identity.Entity(player, EntityManager);

        if (Exists(pointed))
        {
            var effectivePointed = GetPointingTarget(pointed);
            _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(player):user} pointed at {ToPrettyString(effectivePointed):target} {Transform(effectivePointed).Coordinates}");
        }
        else
        {
            _adminLogger.Add(LogType.Action, LogImpact.Low, $"{ToPrettyString(player):user} pointed at {GetTileName(mapCoordsPointed)} {GetTileLogPosition(mapCoordsPointed)}");
        }
    }

}
