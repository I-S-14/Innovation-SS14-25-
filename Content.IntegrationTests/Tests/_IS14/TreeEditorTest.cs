// SPDX-FileCopyrightText: 2025 IS14
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Content.Client._IS14.Research.Editor;
using Robust.Shared.Maths;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._IS14;

/// <summary>
/// The research tree editor, checked where it matters: what it writes has to come back
/// unchanged, and its rules have to refuse the same trees the generator refuses.
/// </summary>
/// <remarks>
/// No window is opened here — the UI needs a client with a renderer. These are the parts that
/// would silently corrupt a tree: the writer, the reader and the rule check.
/// </remarks>
[TestFixture]
public sealed class TreeEditorTest
{
    /// <summary>A tree with every feature on it: effects, a breakthrough, a fork, a secret.</summary>
    private static IS14TreeDraft Sample()
    {
        var draft = new IS14TreeDraft();

        draft.Nodes.Add(new IS14DraftNode
        {
            Id = "IS14EditorCentre",
            Name = "Центр ветки",
            Summary = "С чего начинается ветка.",
            Discipline = "IS14Fundamental",
            Tier = 1,
            Costs = { ["Science"] = 15 },
            Position = new Vector2i(5, 5),
            Recipes = { "AnomalyScanner" },
        });

        draft.Nodes.Add(new IS14DraftNode
        {
            Id = "IS14EditorBreakthrough",
            Name = "Тема с прорывом",
            Discipline = "IS14Fundamental",
            Tier = 3,
            Costs = { ["Science"] = 90, ["Industrial"] = 30 },
            Position = new Vector2i(5, 4),
            Prerequisites = { "IS14EditorCentre" },
            IconSprite = "Objects/Materials/materials.rsi",
            IconState = "bscrystal",
            Breakthrough = new IS14DraftSample
            {
                Name = "блюспейс-кристалл",
                Samples = { "MaterialBSCrystal" },
                Icon = "MaterialBSCrystal1",
                Hint = "Снабжение возит под заказ.",
                PointType = "Science",
                Payout = 60,
            },
            Effects =
            {
                new IS14DraftEffect
                {
                    Kind = IS14DraftEffectKind.Entity,
                    Target = "IS14PathologyScanner",
                    Value = 2,
                    Description = "Выдаёт два сканера",
                },
            },
        });

        draft.Nodes.Add(new IS14DraftNode
        {
            Id = "IS14EditorSecret",
            Name = "Трофейная тема",
            Discipline = "IS14Fundamental",
            Tier = 3,
            Costs = { ["Military"] = 100, ["Science"] = 50 },
            Position = new Vector2i(6, 5),
            Hidden = true,
            Prerequisites = { "IS14EditorCentre" },
            Contraband = new IS14DraftSample
            {
                Name = "энергетический клинок",
                Samples = { "EnergySword" },
                Hint = "Изымает СБ.",
                PointType = "Military",
                Payout = 50,
            },
            Effects =
            {
                new IS14DraftEffect
                {
                    Kind = IS14DraftEffectKind.Modifier,
                    Target = "ResearchPayoutMilitary",
                    Value = 0.2f,
                    Description = "Военные измерения приносят на 20% больше",
                },
            },
        });

        return draft;
    }

    [Test]
    public void WrittenTreeReadsBackTheSame()
    {
        var draft = Sample();

        var locale = new Dictionary<string, string>();

        foreach (var line in IS14TreeExport.Locale(draft).Split('\n'))
        {
            var split = line.IndexOf('=');

            if (split <= 0 || line.StartsWith("#"))
                continue;

            locale[line[..split].Trim()] = line[(split + 1)..].Trim();
        }

        var parsed = IS14TreeParser.Parse(
            IS14TreeExport.Technologies(draft),
            IS14TreeExport.TechData(draft),
            locale);

        Assert.That(parsed.Nodes, Has.Count.EqualTo(draft.Nodes.Count), "все темы вернулись");

        foreach (var original in draft.Nodes)
        {
            var copy = parsed.Find(original.Id);

            Assert.That(copy, Is.Not.Null, $"{original.Id} потерялась при записи");
            Assert.Multiple(() =>
            {
                Assert.That(copy!.Name, Is.EqualTo(original.Name), "название");
                Assert.That(copy.Summary, Is.EqualTo(original.Summary), "описание");
                Assert.That(copy.Discipline, Is.EqualTo(original.Discipline), "ветка");
                Assert.That(copy.Tier, Is.EqualTo(original.Tier), "тир");
                Assert.That(copy.Position, Is.EqualTo(original.Position), "клетка карты");
                Assert.That(copy.Hidden, Is.EqualTo(original.Hidden), "гриф");
                Assert.That(copy.IconSprite, Is.EqualTo(original.IconSprite), "спрайт");
                Assert.That(copy.IconState, Is.EqualTo(original.IconState), "состояние спрайта");
                Assert.That(copy.Costs, Is.EquivalentTo(original.Costs), "цена");
                Assert.That(copy.Prerequisites, Is.EquivalentTo(original.Prerequisites), "предпосылки");
                Assert.That(copy.Recipes, Is.EquivalentTo(original.Recipes), "рецепты");
                Assert.That(copy.Effects, Has.Count.EqualTo(original.Effects.Count), "эффекты");
            });

            if (original.Breakthrough is { } breakthrough)
            {
                Assert.That(copy!.Breakthrough, Is.Not.Null, "прорыв");
                Assert.Multiple(() =>
                {
                    Assert.That(copy.Breakthrough!.Name, Is.EqualTo(breakthrough.Name), "имя образца");
                    Assert.That(copy.Breakthrough.Samples, Is.EquivalentTo(breakthrough.Samples), "образцы");
                    Assert.That(copy.Breakthrough.Hint, Is.EqualTo(breakthrough.Hint), "подсказка");
                    Assert.That(copy.Breakthrough.Payout, Is.EqualTo(breakthrough.Payout), "выплата");
                });
            }

            if (original.Contraband is { } contraband)
            {
                Assert.That(copy!.Contraband, Is.Not.Null, "контрабанда");
                Assert.That(copy.Contraband!.Samples, Is.EquivalentTo(contraband.Samples), "образцы контрабанды");
            }

            for (var index = 0; index < original.Effects.Count; index++)
            {
                var left = original.Effects[index];
                var right = copy!.Effects[index];

                Assert.Multiple(() =>
                {
                    Assert.That(right.Kind, Is.EqualTo(left.Kind), "тип эффекта");
                    Assert.That(right.Target, Is.EqualTo(left.Target), "цель эффекта");
                    Assert.That(right.Value, Is.EqualTo(left.Value).Within(0.001f), "значение эффекта");
                    Assert.That(right.Description, Is.EqualTo(left.Description), "описание эффекта");
                });
            }
        }
    }

    /// <summary>The written YAML has to be the shape the game's own prototypes expect.</summary>
    [Test]
    public void WrittenYamlNamesRealPrototypes()
    {
        var draft = Sample();
        var technologies = IS14TreeExport.Technologies(draft);
        var data = IS14TreeExport.TechData(draft);

        Assert.Multiple(() =>
        {
            Assert.That(technologies, Does.Contain("- type: technology"), "блоки технологий");
            Assert.That(technologies, Does.Contain("  discipline: IS14Fundamental"), "ветка");
            Assert.That(technologies, Does.Contain("  position: 5,4"), "позиция");
            Assert.That(technologies, Does.Contain("  hidden: true"), "гриф");
            Assert.That(technologies, Does.Contain("  cost: 12000"), "цена в апстримных очках");
            Assert.That(data, Does.Contain("- type: is14Technology"), "блоки данных");
            Assert.That(data, Does.Contain("  - !type:IS14ModifierEffect"), "эффект с типом");
            Assert.That(data, Does.Contain("    payout: 60"), "выплата прорыва");
        });
    }

    [Test]
    public async Task RulesRefuseABrokenTree()
    {
        await using var pair = await PoolManager.GetServerClient();
        var protoManager = pair.Client.ResolveDependency<IPrototypeManager>();

        await pair.Client.WaitAssertion(() =>
        {
            Assert.That(IS14TreeValidator.Validate(Sample(), protoManager).Any(problem => problem.Fatal),
                Is.False, "правильное дерево проходит");

            // A topic that hands back the data it was bought with is a discount in disguise.
            var rebate = Sample();
            rebate.Find("IS14EditorCentre")!.Effects.Add(new IS14DraftEffect
            {
                Kind = IS14DraftEffectKind.Points,
                Target = "Science",
                Value = 10,
                Description = "Разово начисляет 10 научных данных",
            });

            Assert.That(Fatal(rebate, protoManager), Does.Contain("возврат очков"), "возврат очков запрещён");

            // A percent buff outside a fork and outside the secret branch.
            var buff = Sample();
            buff.Find("IS14EditorCentre")!.Effects.Add(new IS14DraftEffect
            {
                Kind = IS14DraftEffectKind.Modifier,
                Target = "ResearchPayout",
                Value = 0.1f,
                Description = "Все измерения приносят на 10% больше",
            });

            Assert.That(Fatal(buff, protoManager), Does.Contain("вне развилки"), "бафф только на развилке");

            // Data spent, nothing given back at all.
            var empty = Sample();
            empty.Nodes.Add(new IS14DraftNode
            {
                Id = "IS14EditorDeadEnd",
                Name = "Пустая тема",
                Discipline = "IS14Fundamental",
                Tier = 1,
                Costs = { ["Science"] = 40 },
                Position = new Vector2i(7, 7),
                Prerequisites = { "IS14EditorCentre" },
            });

            Assert.That(Fatal(empty, protoManager), Does.Contain("ничего не даёт"), "пустая тема запрещена");

            // Two topics on one cell of the same branch.
            var collision = Sample();
            collision.Find("IS14EditorSecret")!.Position = new Vector2i(5, 5);

            Assert.That(Fatal(collision, protoManager), Does.Contain("та же клетка"), "дубль клетки запрещён");
        });

        await pair.CleanReturnAsync();
    }

    private static string Fatal(IS14TreeDraft draft, IPrototypeManager protoManager)
    {
        return string.Join('\n', IS14TreeValidator.Validate(draft, protoManager)
            .Where(problem => problem.Fatal)
            .Select(problem => problem.Message));
    }
}
