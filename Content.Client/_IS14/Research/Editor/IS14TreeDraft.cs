// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._IS14.Research;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._IS14.Research.Editor;

/// <summary>
/// What a technology looks like while it is being edited: plain fields, free text instead of
/// locale ids, and no prototype behind it yet.
/// </summary>
/// <remarks>
/// The console reads prototypes; the editor reads and writes this. Keeping them separate is the
/// whole point — a draft may be half-finished, duplicated or renamed, none of which a prototype
/// survives. Export turns a draft back into the same three files the generator writes.
/// </remarks>
public sealed class IS14DraftNode
{
    public string Id = "IS14NewTopic";

    /// <summary>Plain Russian name. Export writes it to the locale under a generated id.</summary>
    public string Name = "Новая тема";

    public string Summary = string.Empty;

    public string Discipline = "IS14Fundamental";

    public int Tier = 1;

    /// <summary>Price per currency, by <see cref="ResearchPointTypePrototype"/> id.</summary>
    public Dictionary<string, int> Costs = new();

    /// <summary>Cell on the console map. One cell is one node plus its gap.</summary>
    public Vector2i Position;

    public string IconSprite = "Interface/Misc/research_disciplines.rsi";

    public string IconState = "experimental";

    public List<string> Prerequisites = new();

    public List<string> Recipes = new();

    public List<IS14DraftEffect> Effects = new();

    public IS14DraftSample? Breakthrough;

    public IS14DraftSample? Contraband;

    public string? ExclusiveGroup;

    public bool Hidden;

    /// <summary>Locale key the export generates ids from: `IS14FieldTheory` -> `fieldtheory`.</summary>
    public string Key => Id.StartsWith("IS14") ? Id[4..].ToLowerInvariant() : Id.ToLowerInvariant();

    /// <summary>
    /// The icon as the renderer wants it. YAML stores the path relative to the texture root, so
    /// the prefix goes back on here and nowhere else.
    /// </summary>
    public SpriteSpecifier Icon => new SpriteSpecifier.Rsi(new ResPath("/Textures/" + IconSprite), IconState);

    public IS14DraftNode Clone()
    {
        return new IS14DraftNode
        {
            Id = Id,
            Name = Name,
            Summary = Summary,
            Discipline = Discipline,
            Tier = Tier,
            Costs = new Dictionary<string, int>(Costs),
            Position = Position,
            IconSprite = IconSprite,
            IconState = IconState,
            Prerequisites = new List<string>(Prerequisites),
            Recipes = new List<string>(Recipes),
            Effects = Effects.Select(effect => effect.Clone()).ToList(),
            Breakthrough = Breakthrough?.Clone(),
            Contraband = Contraband?.Clone(),
            ExclusiveGroup = ExclusiveGroup,
            Hidden = Hidden,
        };
    }
}

/// <summary>Which of the three effect classes a draft effect stands for.</summary>
public enum IS14DraftEffectKind : byte
{
    /// <summary>A one-off grant of data: <see cref="IS14GrantPointsEffect"/>.</summary>
    Points,

    /// <summary>A station-wide number: <see cref="IS14ModifierEffect"/>.</summary>
    Modifier,

    /// <summary>An item printed at the console: <see cref="IS14GrantEntityEffect"/>.</summary>
    Entity,
}

public sealed class IS14DraftEffect
{
    public IS14DraftEffectKind Kind = IS14DraftEffectKind.Points;

    /// <summary>Point type, modifier id or entity prototype, depending on <see cref="Kind"/>.</summary>
    public string Target = "Science";

    /// <summary>Amount of data, modifier delta, or the number of items.</summary>
    public float Value = 10f;

    public string Description = string.Empty;

    public IS14DraftEffect Clone() => new()
    {
        Kind = Kind,
        Target = Target,
        Value = Value,
        Description = Description,
    };
}

/// <summary>A breakthrough sample or a piece of contraband: the same bargain in two coats.</summary>
public sealed class IS14DraftSample
{
    public string Name = "образец";

    public List<string> Samples = new();

    public string? Icon;

    public string Hint = string.Empty;

    public string PointType = "Science";

    public int Payout = 40;

    public IS14DraftSample Clone() => new()
    {
        Name = Name,
        Samples = new List<string>(Samples),
        Icon = Icon,
        Hint = Hint,
        PointType = PointType,
        Payout = Payout,
    };
}

/// <summary>The whole tree being edited.</summary>
public sealed class IS14TreeDraft
{
    public List<IS14DraftNode> Nodes = new();

    public IS14DraftNode? Find(string id) => Nodes.FirstOrDefault(node => node.Id == id);

    public IEnumerable<IS14DraftNode> InBranch(string discipline)
        => Nodes.Where(node => node.Discipline == discipline);

    /// <summary>
    /// Reads the tree that is live in this client: every technology of an IS14 discipline, with
    /// its price table merged in and its locale strings resolved back into plain text.
    /// </summary>
    public static IS14TreeDraft FromPrototypes(IPrototypeManager protoManager)
    {
        var draft = new IS14TreeDraft();

        foreach (var tech in protoManager.EnumeratePrototypes<TechnologyPrototype>())
        {
            if (!tech.Discipline.Id.StartsWith("IS14"))
                continue;

            var node = new IS14DraftNode
            {
                Id = tech.ID,
                Name = Loc.GetString(tech.Name),
                Discipline = tech.Discipline.Id,
                Tier = tech.Tier,
                Position = tech.Position,
                Prerequisites = tech.TechnologyPrerequisites.Select(prereq => prereq.Id).ToList(),
                Recipes = tech.RecipeUnlocks.Select(recipe => recipe.Id).ToList(),
                Hidden = tech.Hidden,
            };

            if (tech.Icon is SpriteSpecifier.Rsi rsi)
            {
                var path = rsi.RsiPath.ToString().Replace('\\', '/').TrimStart('/');

                node.IconSprite = path.StartsWith("Textures/") ? path["Textures/".Length..] : path;
                node.IconState = rsi.RsiState;
            }

            if (protoManager.TryIndex(tech.ID, out IS14TechDataPrototype? data))
                Merge(node, data);

            draft.Nodes.Add(node);
        }

        draft.Nodes.Sort((left, right) => string.Compare(left.Id, right.Id, StringComparison.Ordinal));

        return draft;
    }

    private static void Merge(IS14DraftNode node, IS14TechDataPrototype data)
    {
        node.Costs = data.Costs.ToDictionary(pair => pair.Key.Id, pair => pair.Value);
        node.ExclusiveGroup = data.ExclusiveGroup;

        if (data.Summary != null)
            node.Summary = Loc.GetString(data.Summary.Value);

        node.Breakthrough = Convert(data.Breakthrough);
        node.Contraband = Convert(data.Contraband);

        foreach (var effect in data.Effects)
        {
            var draft = new IS14DraftEffect
            {
                Description = effect.Description != null ? Loc.GetString(effect.Description.Value) : string.Empty,
            };

            switch (effect)
            {
                case IS14GrantPointsEffect points:
                    draft.Kind = IS14DraftEffectKind.Points;
                    draft.Target = points.PointType.Id;
                    draft.Value = points.Amount;
                    break;
                case IS14ModifierEffect modifier:
                    draft.Kind = IS14DraftEffectKind.Modifier;
                    draft.Target = modifier.Modifier.Id;
                    draft.Value = modifier.Delta;
                    break;
                case IS14GrantEntityEffect entity:
                    draft.Kind = IS14DraftEffectKind.Entity;
                    draft.Target = entity.Prototype.Id;
                    draft.Value = entity.Count;
                    break;
                default:
                    continue;
            }

            node.Effects.Add(draft);
        }
    }

    private static IS14DraftSample? Convert(IS14BreakthroughRequirement? requirement)
    {
        if (requirement == null)
            return null;

        return new IS14DraftSample
        {
            Name = Loc.GetString(requirement.Name),
            Samples = requirement.Samples.Select(sample => sample.Id).ToList(),
            Icon = requirement.Icon?.Id,
            Hint = requirement.Hint != null ? Loc.GetString(requirement.Hint.Value) : string.Empty,
            PointType = requirement.PointType.Id,
            Payout = requirement.Payout,
        };
    }
}
