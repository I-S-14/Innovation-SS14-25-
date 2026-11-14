using Robust.Shared.Configuration;

namespace Content.Shared.CCVar;

public sealed partial class CCVars
{
    public static readonly CVarDef<bool> GatewayGeneratorEnabled =
        CVarDef.Create("gateway.generator_enabled", true);
}
