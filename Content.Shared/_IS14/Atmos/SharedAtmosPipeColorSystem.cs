using Content.Shared.Atmos.Piping;

namespace Content.Shared._IS14.Atmos;

public sealed partial class SharedAtmosPipeColorSystem : EntitySystem
{
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<AtmosPipeColorComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<AtmosPipeColorComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnStartup(Entity<AtmosPipeColorComponent> ent, ref ComponentStartup args) => SetAppearance(ent, ent.Comp.Color);

    private void OnShutdown(Entity<AtmosPipeColorComponent> ent, ref ComponentShutdown args) => SetAppearance(ent, Color.White);

    private void SetAppearance(EntityUid uid, Color color)
    {
        if (TryComp<AppearanceComponent>(uid, out var appearance))
            _appearance.SetData(uid, PipeColorVisuals.Color, color, appearance);
    }

    public void SetColor(Entity<AtmosPipeColorComponent> ent, Color color)
    {
        ent.Comp.Color = color;
        SetAppearance(ent, color);
        Dirty(ent);
        // Goob atmos-monitoring consoles use this notification to refresh their network diagrams.
        var changed = new AtmosPipeColorChangedEvent(color);
        RaiseLocalEvent(ent, ref changed);
    }
}
