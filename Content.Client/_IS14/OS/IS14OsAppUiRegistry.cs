using Robust.Shared.Reflection;

namespace Content.Client._IS14.OS;

/// <summary>
///     Maps osApp prototype ids to their client UI classes.
///
///     Content assemblies run in the engine sandbox, so System.Activator and reading custom
///     attributes are both off limits. Instead we enumerate subclasses through
///     <see cref="IReflectionManager"/> and build them with <see cref="IDynamicTypeFactory"/>,
///     reading the id off the instance. App UIs build their controls lazily so the one probe
///     instance per class costs nothing.
/// </summary>
public static class IS14OsAppUiRegistry
{
    private static Dictionary<string, Type>? _types;

    public static IS14OsAppUi? Create(string appId)
    {
        _types ??= Build();

        if (!_types.TryGetValue(appId, out var type))
            return null;

        return IoCManager.Resolve<IDynamicTypeFactory>().CreateInstance<IS14OsAppUi>(type);
    }

    private static Dictionary<string, Type> Build()
    {
        var reflection = IoCManager.Resolve<IReflectionManager>();
        var factory = IoCManager.Resolve<IDynamicTypeFactory>();
        var result = new Dictionary<string, Type>();

        foreach (var type in reflection.GetAllChildren<IS14OsAppUi>())
        {
            var probe = factory.CreateInstance<IS14OsAppUi>(type);
            result[probe.AppId] = type;
        }

        return result;
    }
}
