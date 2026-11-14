using Content.IntegrationTests.Fixtures;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Chemistry.Reagent;
using Content.Shared.Interaction;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class FoodSequenceSolutionCompatibilityTest : GameTest
{
    private static readonly ProtoId<ReagentPrototype> Nutriment = "Nutriment";
    private static readonly ProtoId<ReagentPrototype> Water = "Water";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  id: IS14TestCheeseWithExtraSolution
  parent: FoodCheeseSlice
  components:
  - type: SolutionContainerManager
    solutions:
      extra:
        maxVol: 7
        reagents:
        - ReagentId: Water
          Quantity: 7
";

    [TestCase("FoodCheeseSlice", 0)]
    [TestCase("IS14TestCheeseWithExtraSolution", 7)]
    public async Task AddingAnIngredientPreservesItsReagents(string prototype, int extraWater)
    {
        var map = await Pair.CreateTestMap();
        await Server.WaitAssertion(() =>
        {
            var entities = Server.EntMan;
            var user = entities.SpawnEntity("MobHuman", map.GridCoords);
            var burger = entities.SpawnEntity("FoodBreadBunBottom", map.GridCoords);
            var cheese = entities.SpawnEntity(prototype, map.GridCoords);
            var solutions = Server.System<SharedSolutionContainerSystem>();
            Assert.That(solutions.TryGetSolution(burger, "food", out _, out var burgerSolution), Is.True);
            Assert.That(solutions.TryGetSolution(cheese, "food", out _, out var cheeseSolution), Is.True);
            var volume = burgerSolution!.Volume + cheeseSolution!.Volume + extraWater;
            var nutrients = burgerSolution.GetTotalPrototypeQuantity(Nutriment) + cheeseSolution.GetTotalPrototypeQuantity(Nutriment);
            var water = burgerSolution.GetTotalPrototypeQuantity(Water) + extraWater;

            var interaction = new InteractUsingEvent(user, cheese, burger, map.GridCoords);
            entities.EventBus.RaiseLocalEvent(burger, interaction);
            Assert.That(interaction.Handled, Is.True);
            Assert.That(entities.IsQueuedForDeletion(cheese), Is.True);
            Assert.That(burgerSolution.Volume, Is.EqualTo(volume), "Deleting the ingredient must not delete its reagents.");
            Assert.That(burgerSolution.GetTotalPrototypeQuantity(Nutriment), Is.EqualTo(nutrients));
            Assert.That(burgerSolution.GetTotalPrototypeQuantity(Water), Is.EqualTo(water));
        });
    }
}
