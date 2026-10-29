// SPDX-License-Identifier: AGPL-3.0-or-later

using static Content.Shared.Interaction.SharedInteractionSystem;
using System.Linq;
using Content.Shared.Construction.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared.Construction
{
    public abstract partial class SharedConstructionSystem : EntitySystem
    {
        [Dependency] private SharedMapSystem _map = default!;
        [Dependency] protected SharedTransformSystem TransformSystem = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;

        /// <summary>
        ///     Get predicate for construction obstruction checks.
        /// </summary>
        public Ignored? GetPredicate(bool canBuildInImpassable, MapCoordinates coords)
        {
            if (!canBuildInImpassable)
                return null;

            if (!_map.TryFindGridAt(coords, out var gridUid, out var grid))
                return null;

            var ignored = _mapSystem.GetAnchoredEntities(grid.Owner, grid, coords).ToHashSet();
            return e => ignored.Contains(e);
        }

        public string GetExamineName(GenericPartInfo info)
        {
            if (info.ExamineName is not null)
                return Loc.GetString(info.ExamineName.Value);

            return ProtoMan.Index(info.DefaultPrototype).Name;
        }
    }
}