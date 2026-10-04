// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Content.Shared._IS14.Research;
using Content.Shared.Lathe;
using Content.Shared.Research.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Client._IS14.Research.Editor;

/// <summary>One thing wrong with the tree, and whether it is fatal.</summary>
public readonly record struct IS14TreeProblem(string NodeId, string Message, bool Fatal);

/// <summary>
/// The design rules of the tree, checked while editing instead of after the fact.
/// </summary>
/// <remarks>
/// Same rules the python generator refuses to build on, with one difference: abstract prototypes
/// (<c>BaseAnomalyCore</c> and friends) do not exist at runtime, so a sample the client cannot
/// resolve is a warning rather than an error — it is usually a parent, which is exactly how
/// breakthroughs are meant to be written.
/// </remarks>
public static class IS14TreeValidator
{
    /// <summary>
    /// Modifiers that move the research economy itself. These are a choice or a secret, never a
    /// reward for walking the tree: they belong on a fork or on a classified topic.
    /// </summary>
    private static readonly string[] EconomyPrefixes = { "ResearchPayout", "ExperimentNovelty", "Lathe" };

    public static List<IS14TreeProblem> Validate(IS14TreeDraft draft, IPrototypeManager protoManager)
    {
        var problems = new List<IS14TreeProblem>();
        var seen = new HashSet<string>();
        var cells = new Dictionary<(string, Vector2i), string>();
        var recipes = new Dictionary<string, string>();
        var needed = new HashSet<string>();
        var groups = new Dictionary<string, int>();

        foreach (var node in draft.Nodes)
        {
            foreach (var prereq in node.Prerequisites)
            {
                needed.Add(prereq);
            }

            if (node.ExclusiveGroup is { } group && !string.IsNullOrWhiteSpace(group))
                groups[group] = groups.GetValueOrDefault(group) + 1;
        }

        foreach (var node in draft.Nodes)
        {
            void Fail(string message) => problems.Add(new IS14TreeProblem(node.Id, message, true));
            void Warn(string message) => problems.Add(new IS14TreeProblem(node.Id, message, false));

            if (!seen.Add(node.Id))
                Fail("дубль идентификатора");

            if (string.IsNullOrWhiteSpace(node.Id) || node.Id.Contains(' '))
                Fail("идентификатор пустой или с пробелом");

            if (!protoManager.HasIndex<TechDisciplinePrototype>(node.Discipline))
                Fail($"неизвестная дисциплина {node.Discipline}");

            if (cells.TryGetValue((node.Discipline, node.Position), out var other))
                Fail($"та же клетка карты, что у {other}");
            else
                cells[(node.Discipline, node.Position)] = node.Id;

            var total = node.Costs.Values.Sum();

            if (total <= 0)
                Fail("нет цены");

            var paid = node.Costs.Count(cost => cost.Value > 0);

            if (node.Tier >= 3 && paid < 2)
                Fail("тир 3+ должен стоить минимум двух валют");

            foreach (var currency in node.Costs.Keys)
            {
                if (!protoManager.HasIndex<ResearchPointTypePrototype>(currency))
                    Fail($"неизвестная валюта {currency}");
            }

            foreach (var prereq in node.Prerequisites)
            {
                if (prereq == node.Id)
                    Fail("зависит от самой себя");
                else if (draft.Find(prereq) is not { } parent)
                    Fail($"неизвестная предпосылка {prereq}");
                else if (parent.Hidden && !node.Hidden)
                    Fail($"зависит от скрытой темы {prereq}");
            }

            foreach (var recipe in node.Recipes)
            {
                if (!protoManager.HasIndex<LatheRecipePrototype>(recipe))
                    Fail($"неизвестный рецепт {recipe}");
                else if (recipes.TryGetValue(recipe, out var owner))
                    Fail($"рецепт {recipe} уже открывает {owner}");
                else
                    recipes[recipe] = node.Id;
            }

            CheckSample(node.Breakthrough, "прорыв", protoManager, Fail, Warn);
            CheckSample(node.Contraband, "контрабанда", protoManager, Fail, Warn);

            if (node.Hidden && node.Contraband == null)
                Fail("скрытая тема, которую нечем вскрыть");

            if (!node.Hidden && node.Contraband != null)
                Fail("контрабанда на нескрытой теме");

            if (node.ExclusiveGroup is { } own && !string.IsNullOrWhiteSpace(own) && groups[own] < 2)
                Fail($"в развилке {own} только одна тема");

            CheckEffects(node, protoManager, Fail, Warn);

            if (node.Recipes.Count == 0
                && node.Effects.Count == 0
                && node.Breakthrough == null
                && !needed.Contains(node.Id))
                Fail("тема стоит данных и ничего не даёт");
        }

        return problems;
    }

    private static void CheckSample(
        IS14DraftSample? sample,
        string what,
        IPrototypeManager protoManager,
        Action<string> fail,
        Action<string> warn)
    {
        if (sample == null)
            return;

        if (sample.Samples.Count == 0)
            fail($"{what} без образца");

        foreach (var prototype in sample.Samples)
        {
            if (!protoManager.HasIndex<EntityPrototype>(prototype))
                warn($"{what}: прототип {prototype} не найден (абстрактный родитель — это нормально)");
        }

        if (sample.Icon is { } icon && !protoManager.HasIndex<EntityPrototype>(icon))
            warn($"{what}: иконка {icon} не найдена");

        if (sample.Payout <= 0)
            fail($"{what} ничего не платит за образец");
    }

    private static void CheckEffects(
        IS14DraftNode node,
        IPrototypeManager protoManager,
        Action<string> fail,
        Action<string> warn)
    {
        foreach (var effect in node.Effects)
        {
            if (string.IsNullOrWhiteSpace(effect.Description))
                warn("эффект без описания — игрок не узнает, что произошло");

            switch (effect.Kind)
            {
                case IS14DraftEffectKind.Points:
                    if (!protoManager.HasIndex<ResearchPointTypePrototype>(effect.Target))
                        fail($"эффект начисляет неизвестную валюту {effect.Target}");

                    // Paying back the data the topic was bought with is a discount wearing the
                    // costume of a reward.
                    if (effect.Value <= node.Costs.GetValueOrDefault(effect.Target))
                        fail($"возврат очков: цена {node.Costs.GetValueOrDefault(effect.Target)} "
                             + $"{effect.Target}, выплата {(int) effect.Value}");

                    break;
                case IS14DraftEffectKind.Modifier:
                    if (!protoManager.HasIndex<IS14ResearchModifierPrototype>(effect.Target))
                        fail($"неизвестный модификатор {effect.Target}");

                    if (IsEconomy(effect.Target)
                        && string.IsNullOrWhiteSpace(node.ExclusiveGroup)
                        && !node.Hidden)
                        fail($"бафф исследований {effect.Target} вне развилки и вне грифа");

                    if (effect.Value == 0f)
                        fail($"модификатор {effect.Target} ничего не меняет");

                    break;
                case IS14DraftEffectKind.Entity:
                    if (!protoManager.HasIndex<EntityPrototype>(effect.Target))
                        fail($"эффект выдаёт неизвестный прототип {effect.Target}");

                    break;
            }
        }
    }

    private static bool IsEconomy(string modifier)
        => EconomyPrefixes.Any(prefix => modifier.StartsWith(prefix, StringComparison.Ordinal));
}
