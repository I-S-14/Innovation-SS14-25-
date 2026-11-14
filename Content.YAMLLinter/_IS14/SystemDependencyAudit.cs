using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.IntegrationTests;
using Robust.Shared.GameObjects;
using Robust.Shared.IoC;

namespace Content.YAMLLinter._IS14;

/// <summary>Checks content system dependencies before creating expensive server/client pairs.</summary>
internal static class SystemDependencyAudit
{
    public static int Run()
    {
        var missing = new HashSet<string>();
        foreach (var client in new[] { false, true })
        {
            var types = PoolManager.GetAssemblies(client, includePoolAssembly: false)
                .SelectMany(assembly => assembly.GetTypes()).ToArray();
            var systems = types.Where(type => !type.IsAbstract && !type.ContainsGenericParameters
                                               && typeof(EntitySystem).IsAssignableFrom(type)).ToArray();
            var available = new Dictionary<Type, bool>();
            foreach (var system in systems)
            {
                for (var declaring = system; declaring != null && declaring != typeof(EntitySystem); declaring = declaring.BaseType)
                {
                    foreach (var field in declaring.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                    {
                        var dependency = field.FieldType;
                        if (!dependency.IsAbstract || !typeof(EntitySystem).IsAssignableFrom(dependency)
                            || !dependency.Assembly.GetName().Name!.StartsWith("Content.")
                            || field.GetCustomAttribute<DependencyAttribute>() == null)
                            continue;
                        if (!available.TryGetValue(dependency, out var exists))
                        {
                            exists = systems.Any(dependency.IsAssignableFrom);
                            available.Add(dependency, exists);
                        }
                        if (!exists)
                            missing.Add($"{(client ? "Client" : "Server")}: {declaring.FullName}.{field.Name} requires {dependency.FullName}");
                    }
                }
            }
        }
        foreach (var entry in missing.OrderBy(entry => entry))
            Console.WriteLine(entry);
        Console.WriteLine($"{missing.Count} abstract content entity-system dependencies without an implementation.");
        return missing.Count == 0 ? 0 : 1;
    }
}
