// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Globalization;
using System.Linq;
using System.Text;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Client._IS14.Research.Editor;

/// <summary>
/// Turns a draft into the three files the game actually reads, and back again.
/// </summary>
/// <remarks>
/// The output is byte-for-byte the same shape as <c>Tools/_IS14/generate_research_tree.py</c>
/// writes, so a tree built in the editor is a drop-in replacement for a generated one: copy the
/// files over the ones in <c>Resources/</c> and nothing else has to change. Reading them back is
/// possible only because we wrote them — the parser understands our own fixed layout, not YAML
/// in general, which is the trade that keeps it a hundred lines instead of a dependency.
/// </remarks>
public static class IS14TreeExport
{
    /// <summary>Folder inside the client's user data where drafts and exports live.</summary>
    public static readonly ResPath Folder = new("/is14_research_editor");

    public static readonly ResPath TechnologiesFile = Folder / "technologies.yml";

    public static readonly ResPath TechDataFile = Folder / "tech_data.yml";

    public static readonly ResPath LocaleFile = Folder / "research_tree.ftl";

    private const string Header = """
                                  # SPDX-FileCopyrightText: 2025 IS14
                                  #
                                  # SPDX-License-Identifier: AGPL-3.0-or-later
                                  """;

    /// <summary>Currencies in the order the generator writes them, so diffs stay readable.</summary>
    private static readonly string[] Currencies = { "Science", "Industrial", "Military", "Biological", "Social" };

    public static void Save(IResourceManager resources, IS14TreeDraft draft)
    {
        resources.UserData.CreateDir(Folder);
        resources.UserData.WriteAllText(TechnologiesFile, Technologies(draft));
        resources.UserData.WriteAllText(TechDataFile, TechData(draft));
        resources.UserData.WriteAllText(LocaleFile, Locale(draft));
    }

    public static bool TryLoad(IResourceManager resources, out IS14TreeDraft draft)
    {
        draft = new IS14TreeDraft();

        if (!resources.UserData.Exists(TechnologiesFile))
            return false;

        var text = new Dictionary<string, string>();

        if (resources.UserData.TryReadAllText(LocaleFile, out var locale))
        {
            foreach (var line in locale.Split('\n'))
            {
                var split = line.IndexOf('=');

                if (split <= 0 || line.StartsWith('#'))
                    continue;

                text[line[..split].Trim()] = line[(split + 1)..].Trim();
            }
        }

        draft = IS14TreeParser.Parse(
            resources.UserData.ReadAllText(TechnologiesFile),
            resources.UserData.TryReadAllText(TechDataFile, out var data) ? data : string.Empty,
            text);

        return true;
    }

    public static string Technologies(IS14TreeDraft draft)
    {
        var builder = new StringBuilder(Header);

        builder.Append("""

                       # The IS14 research tree. WRITTEN BY THE IN-GAME EDITOR (is14treeeditor).
                       #
                       # Nodes are upstream `technology` prototypes, so the research server, lathes and tech
                       # disks keep working unchanged. Positions are cells of the console map. Prices,
                       # effects, breakthroughs and forks live in tech_data.yml.

                       """);

        foreach (var node in Ordered(draft))
        {
            builder.Append("\n- type: technology\n");
            builder.Append($"  id: {node.Id}\n");
            builder.Append($"  name: is14-tech-{node.Key}\n");
            builder.Append($"  discipline: {node.Discipline}\n");
            builder.Append($"  tier: {node.Tier}\n");
            builder.Append($"  cost: {node.Costs.Values.Sum() * 100}\n");
            builder.Append($"  position: {node.Position.X},{node.Position.Y}\n");

            if (node.Hidden)
                builder.Append("  hidden: true\n");

            builder.Append("  icon:\n");
            builder.Append($"    sprite: {node.IconSprite}\n");
            builder.Append($"    state: {node.IconState}\n");

            if (node.Prerequisites.Count > 0)
            {
                builder.Append("  technologyPrerequisites:\n");

                foreach (var prereq in node.Prerequisites)
                {
                    builder.Append($"  - {prereq}\n");
                }
            }

            if (node.Recipes.Count == 0)
                continue;

            builder.Append("  recipeUnlocks:\n");

            foreach (var recipe in node.Recipes)
            {
                builder.Append($"  - {recipe}\n");
            }
        }

        return builder.ToString();
    }

    public static string TechData(IS14TreeDraft draft)
    {
        var builder = new StringBuilder(Header);

        builder.Append("""

                       # Prices, effects, breakthrough samples and forks of the IS14 tree.
                       # WRITTEN BY THE IN-GAME EDITOR (is14treeeditor).

                       """);

        foreach (var node in Ordered(draft))
        {
            builder.Append("\n- type: is14Technology\n");
            builder.Append($"  id: {node.Id}\n");

            if (!string.IsNullOrWhiteSpace(node.Summary))
                builder.Append($"  summary: is14-techsummary-{node.Key}\n");

            builder.Append("  costs:\n");

            foreach (var currency in Currencies)
            {
                if (node.Costs.TryGetValue(currency, out var amount) && amount > 0)
                    builder.Append($"    {currency}: {amount}\n");
            }

            if (!string.IsNullOrWhiteSpace(node.ExclusiveGroup))
                builder.Append($"  exclusiveGroup: {node.ExclusiveGroup}\n");

            Sample(builder, node, node.Breakthrough, "breakthrough", "is14-sample", "is14-samplehint");
            Sample(builder, node, node.Contraband, "contraband", "is14-contraband", "is14-contrabandhint");

            if (node.Effects.Count == 0)
                continue;

            builder.Append("  effects:\n");

            for (var index = 0; index < node.Effects.Count; index++)
            {
                var effect = node.Effects[index];

                builder.Append($"  - !type:{TypeOf(effect.Kind)}\n");
                builder.Append($"    description: is14-effect-{node.Key}-{index}\n");

                switch (effect.Kind)
                {
                    case IS14DraftEffectKind.Points:
                        builder.Append($"    pointType: {effect.Target}\n");
                        builder.Append($"    amount: {(int) effect.Value}\n");
                        break;
                    case IS14DraftEffectKind.Modifier:
                        builder.Append($"    modifier: {effect.Target}\n");
                        builder.Append($"    delta: {effect.Value.ToString("0.###", CultureInfo.InvariantCulture)}\n");
                        break;
                    case IS14DraftEffectKind.Entity:
                        builder.Append($"    prototype: {effect.Target}\n");

                        if ((int) effect.Value != 1)
                            builder.Append($"    count: {(int) effect.Value}\n");

                        break;
                }
            }
        }

        return builder.ToString();
    }

    public static string Locale(IS14TreeDraft draft)
    {
        var builder = new StringBuilder("# WRITTEN BY THE IN-GAME EDITOR (is14treeeditor)\n");

        foreach (var node in Ordered(draft))
        {
            builder.Append($"\nis14-tech-{node.Key} = {node.Name}\n");

            if (!string.IsNullOrWhiteSpace(node.Summary))
                builder.Append($"is14-techsummary-{node.Key} = {node.Summary}\n");

            for (var index = 0; index < node.Effects.Count; index++)
            {
                var description = node.Effects[index].Description;

                if (!string.IsNullOrWhiteSpace(description))
                    builder.Append($"is14-effect-{node.Key}-{index} = {description}\n");
            }

            SampleLocale(builder, node, node.Breakthrough, "is14-sample", "is14-samplehint");
            SampleLocale(builder, node, node.Contraband, "is14-contraband", "is14-contrabandhint");
        }

        return builder.ToString();
    }

    private static void Sample(
        StringBuilder builder,
        IS14DraftNode node,
        IS14DraftSample? sample,
        string field,
        string namePrefix,
        string hintPrefix)
    {
        if (sample == null)
            return;

        builder.Append($"  {field}:\n");
        builder.Append($"    name: {namePrefix}-{node.Key}\n");
        builder.Append("    samples:\n");

        foreach (var prototype in sample.Samples)
        {
            builder.Append($"    - {prototype}\n");
        }

        if (!string.IsNullOrWhiteSpace(sample.Icon))
            builder.Append($"    icon: {sample.Icon}\n");

        if (!string.IsNullOrWhiteSpace(sample.Hint))
            builder.Append($"    hint: {hintPrefix}-{node.Key}\n");

        builder.Append($"    pointType: {sample.PointType}\n");
        builder.Append($"    payout: {sample.Payout}\n");
    }

    private static void SampleLocale(
        StringBuilder builder,
        IS14DraftNode node,
        IS14DraftSample? sample,
        string namePrefix,
        string hintPrefix)
    {
        if (sample == null)
            return;

        builder.Append($"{namePrefix}-{node.Key} = {sample.Name}\n");

        if (!string.IsNullOrWhiteSpace(sample.Hint))
            builder.Append($"{hintPrefix}-{node.Key} = {sample.Hint}\n");
    }

    private static string TypeOf(IS14DraftEffectKind kind) => kind switch
    {
        IS14DraftEffectKind.Points => "IS14GrantPointsEffect",
        IS14DraftEffectKind.Modifier => "IS14ModifierEffect",
        _ => "IS14GrantEntityEffect",
    };

    /// <summary>Grouped by branch, then by map position: the order a reader expects.</summary>
    private static IEnumerable<IS14DraftNode> Ordered(IS14TreeDraft draft)
    {
        return draft.Nodes
            .OrderBy(node => node.Discipline)
            .ThenBy(node => node.Position.X)
            .ThenBy(node => node.Position.Y);
    }
}
