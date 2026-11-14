using System.Collections.Generic;
using System.Reflection;
using Robust.Shared.GameObjects;

namespace Content.IntegrationTests.Tests._IS14;

[TestFixture]
public sealed class EntityQueryInitializationTest
{
    [Test]
    public async Task ContentSystemsInitializeCachedComponentQueries()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Server.WaitAssertion(() => AssertInitialized(pair.Server.EntMan.EntitySysManager));
        await pair.Client.WaitAssertion(() => AssertInitialized(pair.Client.EntMan.EntitySysManager));
        await pair.CleanReturnAsync();
    }

    private static void AssertInitialized(IEntitySystemManager manager)
    {
        var missing = new List<string>();
        foreach (var type in manager.GetEntitySystemTypes())
        {
            var system = manager.GetEntitySystem(type);
            for (var declaring = system.GetType(); declaring != null && declaring != typeof(EntitySystem); declaring = declaring.BaseType)
            {
                if (!declaring.Assembly.GetName().Name!.StartsWith("Content."))
                    continue;
                foreach (var field in declaring.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                {
                    var queryType = field.FieldType;
                    if (!queryType.IsGenericType || queryType.GetGenericTypeDefinition() != typeof(EntityQuery<>))
                        continue;
                    if (Equals(field.GetValue(system), Activator.CreateInstance(queryType)))
                        missing.Add($"{declaring.FullName}.{field.Name}");
                }
            }
        }
        Assert.That(missing, Is.Empty, "Uninitialized queries crash when the corresponding gameplay feature first runs.");
    }
}
