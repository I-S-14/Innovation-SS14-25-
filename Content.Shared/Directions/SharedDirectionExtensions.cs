// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Map;
using System.Collections;
using System.Linq;
using Robust.Shared.Random;

namespace Content.Shared.Directions;

public static class SharedDirectionExtensions
{
    public static EntityCoordinates Offset(this EntityCoordinates coordinates, Direction direction)
    {
        return coordinates.Offset(direction.ToVec());
    }
}