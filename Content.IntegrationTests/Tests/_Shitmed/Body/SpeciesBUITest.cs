// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Shared.Humanoid.Prototypes;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._Shitmed.Body;

[TestFixture]
public sealed class SpeciesBUiTest
{
    private readonly EntProtoId CompatibilityBaseMobSpeciesTestEntityId = "BaseMobSpeciesTest";

    [TestPrototypes]
    private const string Prototypes = @"
- type: entity
  name: BaseMobSpeciesTest
  id: BaseMobSpeciesTest
  parent: BaseMobSpecies
";

    private Dictionary<Enum, InterfaceData> GetInterfaces(UserInterfaceComponent comp) =>
        (Dictionary<Enum, InterfaceData>)
            typeof(UserInterfaceComponent).GetField("Interfaces", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(comp);

    [Test]
    public async Task AllSpeciesHaveBaseBUiTest()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Dirty = true,
            Connected = false
        });

        var server = pair.Server;
        var proto = server.ResolveDependency<IPrototypeManager>();
        var factoryComp = server.ResolveDependency<IComponentFactory>();

        await server.WaitAssertion(() =>
        {
            var bUiSys = server.System<SharedUserInterfaceSystem>();

            Assert.That(proto.TryIndex(CompatibilityBaseMobSpeciesTestEntityId, out var baseEnt), Is.True);
            Assert.That(baseEnt, Is.Not.Null);
            Assert.That(baseEnt.TryGetComponent<UserInterfaceComponent>(out var bUiBase, factoryComp), Is.True);
            Assert.That(bUiBase, Is.Not.Null);
            var baseKeys = GetInterfaces(bUiBase).Keys.ToArray();

            foreach (var species in proto.EnumeratePrototypes<SpeciesPrototype>())
            {
                var ent = proto.Index(species.Prototype);
                Assert.That(ent.TryGetComponent<UserInterfaceComponent>(out var bUi, factoryComp), Is.True);
                Assert.That(bUi, Is.Not.Null);
                // IS14: appearance species also describe NPCs such as scurrets,
                // whose ancestor base does not provide crew marking/wanted UIs.
                if (!proto.EnumerateAllParents<EntityPrototype>(ent.ID).Any(parent => parent.id == "BaseMobSpecies"))
                    continue;
                var states = GetInterfaces(bUi); //IS14-change: check the species itself, not the base twice.
                foreach (var key in baseKeys)
                {
                    Assert.That(states.ContainsKey(key), Is.True, $"Species {species.ID} has not UserInterface of type enum.{key.GetType().Name}");
                }
            }
        });
        await pair.CleanReturnAsync();
    }
}
