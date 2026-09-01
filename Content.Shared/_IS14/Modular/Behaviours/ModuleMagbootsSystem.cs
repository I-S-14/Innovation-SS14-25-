// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Clothing;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Runs the magnetic stability module.
///
///     This cannot be a plain component grant: switching magboots on is more than owning a
///     component — pressure immunity, the weightlessness refresh and the status alert all
///     come from <see cref="SharedMagbootsSystem.UpdateMagbootEffects"/>, and without that
///     call the wearer would get no indication the module was doing anything.
/// </summary>
public sealed class ModuleMagbootsSystem : ModuleBehaviourSystem<ModuleMagbootsComponent>
{
    [Dependency] private readonly SharedMagbootsSystem _magboots = default!;

    protected override bool RequiresActive(Entity<ModuleMagbootsComponent> ent) => true;

    /// <summary>
    ///     The clamp follows the operator: a suit torn off mid-stride must not leave the
    ///     previous wearer glued to the deck.
    /// </summary>
    protected override void UserChanged(Entity<ModuleMagbootsComponent> ent, EntityUid chassis, EntityUid? user)
    {
        if (ent.Comp.AppliedTo == null)
            return;

        Revoke(ent);

        if (user != null)
            Start(ent, chassis);
    }

    protected override void Start(Entity<ModuleMagbootsComponent> ent, EntityUid chassis)
    {
        if (GetChassisUser(chassis) is not { } user)
            return;

        if (ent.Comp.AppliedTo == user)
            return;

        Revoke(ent);

        // Someone in real magboots keeps their own component; we only borrow it.
        ent.Comp.Granted = !HasComp<MagbootsComponent>(user);

        var comp = EnsureComp<MagbootsComponent>(user);
        _magboots.UpdateMagbootEffects(user, (user, comp), true);

        ent.Comp.AppliedTo = user;
    }

    protected override void Stop(Entity<ModuleMagbootsComponent> ent, EntityUid chassis)
    {
        Revoke(ent);
    }

    private void Revoke(Entity<ModuleMagbootsComponent> ent)
    {
        if (ent.Comp.AppliedTo is not { } user)
            return;

        ent.Comp.AppliedTo = null;

        if (TerminatingOrDeleted(user) || !TryComp<MagbootsComponent>(user, out var comp))
            return;

        _magboots.UpdateMagbootEffects(user, (user, comp), false);

        if (ent.Comp.Granted)
            RemComp<MagbootsComponent>(user);

        ent.Comp.Granted = false;
    }
}
