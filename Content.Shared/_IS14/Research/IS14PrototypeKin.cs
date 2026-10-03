// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.Prototypes;

namespace Content.Shared._IS14.Research;

/// <summary>
/// "Is this prototype that prototype, or a child of it?" — the one question both the sample
/// requirements of a breakthrough and the lathe scope of a modifier need to answer.
/// </summary>
/// <remarks>
/// Written against prototype ancestry rather than tags on purpose: naming <c>BaseAnomalyCore</c>
/// covers every anomaly core that exists or will ever be added, and naming <c>Protolathe</c>
/// covers its hyper-convection variant, all without touching a single upstream prototype.
/// </remarks>
public static class IS14PrototypeKin
{
    /// <summary>Whether <paramref name="prototypeId"/> is <paramref name="ancestorId"/> or inherits from it.</summary>
    public static bool IsOrDescends(IPrototypeManager proto, string? prototypeId, string ancestorId, int depth = 0)
    {
        if (string.IsNullOrEmpty(prototypeId))
            return false;

        if (prototypeId == ancestorId)
            return true;

        // A malformed parent chain must not hang the server, so the walk is capped rather
        // than trusted to terminate.
        if (depth > 32 || !proto.TryIndex(prototypeId, out EntityPrototype? entry) || entry.Parents is not { } parents)
            return false;

        foreach (var parent in parents)
        {
            if (IsOrDescends(proto, parent, ancestorId, depth + 1))
                return true;
        }

        return false;
    }

    /// <summary>Whether the prototype matches any entry of the list, inheritance included.</summary>
    public static bool MatchesAny(IPrototypeManager proto, string? prototypeId, IReadOnlyList<EntProtoId> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (IsOrDescends(proto, prototypeId, candidate.Id))
                return true;
        }

        return false;
    }
}
