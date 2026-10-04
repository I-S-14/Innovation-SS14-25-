// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;

namespace Content.Client._IS14.Research.Editor;

/// <summary>
/// Reads back the files <see cref="IS14TreeExport"/> wrote, so an unfinished tree survives a
/// client restart.
/// </summary>
/// <remarks>
/// This is not a YAML parser and must never be pointed at arbitrary YAML. It understands one
/// fixed layout — ours — where every key sits at a known indentation and nothing is quoted,
/// escaped or anchored. That assumption is what keeps the editor free of a serializer.
/// </remarks>
public static class IS14TreeParser
{
    public static IS14TreeDraft Parse(string technologies, string techData, Dictionary<string, string> locale)
    {
        var draft = new IS14TreeDraft();
        var byId = new Dictionary<string, IS14DraftNode>();

        foreach (var block in Blocks(technologies, "- type: technology"))
        {
            var node = ReadTechnology(block, locale);

            if (node == null || byId.ContainsKey(node.Id))
                continue;

            byId[node.Id] = node;
            draft.Nodes.Add(node);
        }

        foreach (var block in Blocks(techData, "- type: is14Technology"))
        {
            var id = Value(block, "id");

            if (id == null || !byId.TryGetValue(id, out var node))
                continue;

            ReadTechData(node, block, locale);
        }

        return draft;
    }

    private static IS14DraftNode? ReadTechnology(List<string> block, Dictionary<string, string> locale)
    {
        var id = Value(block, "id");

        if (id == null)
            return null;

        var node = new IS14DraftNode { Id = id };

        node.Name = Text(locale, Value(block, "name"), node.Id);
        node.Discipline = Value(block, "discipline") ?? node.Discipline;
        node.Tier = Int(Value(block, "tier"), 1);
        node.Hidden = Value(block, "hidden") == "true";
        node.IconSprite = Value(block, "sprite", 4) ?? node.IconSprite;
        node.IconState = Value(block, "state", 4) ?? node.IconState;

        var position = Value(block, "position")?.Split(',');

        if (position is { Length: 2 })
            node.Position = new Vector2i(Int(position[0], 0), Int(position[1], 0));

        node.Prerequisites = Items(block, "technologyPrerequisites");
        node.Recipes = Items(block, "recipeUnlocks");

        return node;
    }

    private static void ReadTechData(IS14DraftNode node, List<string> block, Dictionary<string, string> locale)
    {
        node.Summary = Text(locale, Value(block, "summary"), string.Empty);
        node.ExclusiveGroup = Value(block, "exclusiveGroup");
        node.Costs = Map(block, "costs");
        node.Breakthrough = ReadSample(block, "breakthrough", locale);
        node.Contraband = ReadSample(block, "contraband", locale);
        node.Effects = ReadEffects(block, locale);
    }

    private static IS14DraftSample? ReadSample(
        List<string> block,
        string field,
        Dictionary<string, string> locale)
    {
        var body = Section(block, field);

        if (body.Count == 0)
            return null;

        var sample = new IS14DraftSample
        {
            Name = Text(locale, Value(body, "name", 4), "образец"),
            Hint = Text(locale, Value(body, "hint", 4), string.Empty),
            Icon = Value(body, "icon", 4),
            PointType = Value(body, "pointType", 4) ?? "Science",
            Payout = Int(Value(body, "payout", 4), 40),
        };

        foreach (var line in body)
        {
            if (line.StartsWith("    - "))
                sample.Samples.Add(line[6..].Trim());
        }

        return sample;
    }

    private static List<IS14DraftEffect> ReadEffects(List<string> block, Dictionary<string, string> locale)
    {
        var effects = new List<IS14DraftEffect>();
        var inEffects = false;
        IS14DraftEffect? current = null;

        foreach (var line in block)
        {
            if (line.StartsWith("  effects:"))
            {
                inEffects = true;
                continue;
            }

            if (!inEffects)
                continue;

            if (line.StartsWith("  - !type:"))
            {
                current = new IS14DraftEffect { Kind = KindOf(line["  - !type:".Length..].Trim()) };
                effects.Add(current);
                continue;
            }

            if (current == null || !line.StartsWith("    "))
                continue;

            var split = line.IndexOf(':');

            if (split < 0)
                continue;

            var key = line[..split].Trim();
            var value = line[(split + 1)..].Trim();

            switch (key)
            {
                case "description":
                    current.Description = Text(locale, value, string.Empty);
                    break;
                case "pointType":
                case "modifier":
                case "prototype":
                    current.Target = value;
                    break;
                case "amount":
                case "count":
                    current.Value = Int(value, 0);
                    break;
                case "delta":
                    current.Value = float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture,
                        out var delta)
                        ? delta
                        : 0f;
                    break;
            }
        }

        return effects;
    }

    private static IS14DraftEffectKind KindOf(string type) => type switch
    {
        "IS14ModifierEffect" => IS14DraftEffectKind.Modifier,
        "IS14GrantEntityEffect" => IS14DraftEffectKind.Entity,
        _ => IS14DraftEffectKind.Points,
    };

    /// <summary>Splits a file into blocks, each starting at the given marker line.</summary>
    private static IEnumerable<List<string>> Blocks(string text, string marker)
    {
        List<string>? block = null;

        foreach (var raw in text.Split('\n'))
        {
            var line = raw.TrimEnd('\r');

            if (line.StartsWith(marker))
            {
                if (block != null)
                    yield return block;

                block = new List<string>();
                continue;
            }

            block?.Add(line);
        }

        if (block != null)
            yield return block;
    }

    private static string? Value(List<string> block, string key, int indent = 2)
    {
        var prefix = new string(' ', indent) + key + ":";

        foreach (var line in block)
        {
            if (line.StartsWith(prefix))
                return line[prefix.Length..].Trim() is { Length: > 0 } value ? value : null;
        }

        return null;
    }

    /// <summary>Lines belonging to a nested key, up to the next key at the same level.</summary>
    private static List<string> Section(List<string> block, string key)
    {
        var body = new List<string>();
        var inside = false;

        foreach (var line in block)
        {
            if (line.StartsWith("  " + key + ":"))
            {
                inside = true;
                continue;
            }

            if (!inside)
                continue;

            if (!line.StartsWith("    "))
                break;

            body.Add(line);
        }

        return body;
    }

    private static List<string> Items(List<string> block, string key)
    {
        var items = new List<string>();
        var inside = false;

        foreach (var line in block)
        {
            if (line.StartsWith("  " + key + ":"))
            {
                inside = true;
                continue;
            }

            if (!inside)
                continue;

            if (!line.StartsWith("  - "))
                break;

            items.Add(line[4..].Trim());
        }

        return items;
    }

    private static Dictionary<string, int> Map(List<string> block, string key)
    {
        var map = new Dictionary<string, int>();

        foreach (var line in Section(block, key))
        {
            var split = line.IndexOf(':');

            if (split < 0)
                continue;

            map[line[..split].Trim()] = Int(line[(split + 1)..].Trim(), 0);
        }

        return map;
    }

    private static int Int(string? value, int fallback)
        => int.TryParse(value, out var parsed) ? parsed : fallback;

    /// <summary>Locale id back to the text the editor showed, falling back to the id itself.</summary>
    private static string Text(Dictionary<string, string> locale, string? locId, string fallback)
    {
        if (locId == null)
            return fallback;

        return locale.TryGetValue(locId, out var text) ? text : fallback;
    }
}
