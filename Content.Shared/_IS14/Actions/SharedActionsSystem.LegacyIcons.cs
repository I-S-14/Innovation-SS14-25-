using Content.Shared.Actions.Components;

namespace Content.Shared.Actions;

public abstract partial class SharedActionsSystem
{
    private void OnLegacyIconInit(Entity<ActionComponent> ent, ref MapInitEvent args)
    {
        if (ent.Comp.Icon is not { } icon)
            return;
        EnsureComp<AppearanceComponent>(ent);
        SetIcon(ent.AsNullable(), icon);
    }
}
