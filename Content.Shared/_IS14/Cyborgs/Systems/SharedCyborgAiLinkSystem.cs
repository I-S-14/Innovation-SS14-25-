// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Examine;
using Content.Shared.Interaction.Events;
using Content.Shared.Movement.Systems;

namespace Content.Shared._IS14.Cyborgs.Systems;

/// <summary>
/// The predicted half of the AI link: lockdown and examine. Linking and law sync are on the server.
/// </summary>
public abstract class SharedCyborgAiLinkSystem : EntitySystem
{
    [Dependency] private readonly MovementSpeedModifierSystem _movementSpeed = default!;
    [Dependency] protected readonly SharedCyborgSystem Cyborg = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgAiLinkComponent, RefreshMovementSpeedModifiersEvent>(OnRefreshSpeed);
        SubscribeLocalEvent<CyborgAiLinkComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<CyborgAiLinkComponent, ExaminedEvent>(OnExamined);
    }

    private void OnRefreshSpeed(Entity<CyborgAiLinkComponent> ent, ref RefreshMovementSpeedModifiersEvent args)
    {
        if (ent.Comp.Lockdown)
            args.ModifySpeed(0f, 0f);
    }

    private void OnInteractionAttempt(Entity<CyborgAiLinkComponent> ent, ref InteractionAttemptEvent args)
    {
        if (ent.Comp.Lockdown)
            args.Cancelled = true;
    }

    private void OnExamined(Entity<CyborgAiLinkComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        if (ent.Comp.Lockdown)
            args.PushMarkup(Loc.GetString("cyborg-examine-lockdown"));

        args.PushMarkup(ent.Comp.Ai is { } ai && Exists(ai)
            ? Loc.GetString("cyborg-examine-ai-linked", ("ai", Name(ai)))
            : Loc.GetString("cyborg-examine-ai-unlinked"));
    }

    public void SetLockdown(Entity<CyborgAiLinkComponent> ent, bool lockdown)
    {
        if (ent.Comp.Lockdown == lockdown)
            return;

        ent.Comp.Lockdown = lockdown;
        Dirty(ent);
        _movementSpeed.RefreshMovementSpeedModifiers(ent);

        if (lockdown && TryComp<CyborgComponent>(ent, out var cyborg))
            Cyborg.StoreAllTools((ent, cyborg));
    }
}
