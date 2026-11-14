using Content.Shared._IS14.Atmos;

namespace Content.Server.Atmos.Piping.EntitySystems
{
    public sealed partial class AtmosPipeColorSystem : EntitySystem
    {
        // IS14-change: keep the painter/admin API; shared state supplies client item visuals.
        [Dependency] private readonly SharedAtmosPipeColorSystem _colors = default!;

        public void SetColor(EntityUid uid, AtmosPipeColorComponent component, Color color)
        {
            _colors.SetColor((uid, component), color);
        }
    }
}
