// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Gravity;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;

namespace Content.Shared._IS14.Modular.Behaviours;

/// <summary>
///     Runs the chassis' thrusters.
///
///     A hand-held jetpack is a thing you light and put out yourself, and lighting one
///     with your boots on the floor puts the wearer into <c>BodyStatus.InAir</c> — which
///     is why walking stops working. A suit module cannot behave that way: its switch is
///     in a panel, and nobody is going to open the panel on the way out of the airlock.
///
///     So the module's switch arms the thrusters, and weightlessness fires them. Step off
///     the station and they light; come back down and they cut out; the switch stays where
///     the wearer left it either way.
/// </summary>
public sealed class ModuleJetpackSystem : ModuleBehaviourSystem<ModuleJetpackComponent>
{
    [Dependency] private readonly SharedGravitySystem _gravity = default!;
    [Dependency] private readonly SharedJetpackSystem _jetpack = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ChassisJetpackUserComponent, WeightlessnessChangedEvent>(OnWeightlessnessChanged);
    }

    protected override bool RequiresActive(Entity<ModuleJetpackComponent> ent) => true;

    protected override void Start(Entity<ModuleJetpackComponent> ent, EntityUid chassis)
    {
        if (ent.Comp.Applied)
            return;

        var jetpack = EnsureComp<JetpackComponent>(chassis);

        jetpack.MoleUsage = ent.Comp.MoleUsage;
        jetpack.Acceleration = ent.Comp.Acceleration;
        jetpack.Friction = ent.Comp.Friction;
        jetpack.WeightlessModifier = ent.Comp.WeightlessModifier;

        ent.Comp.Applied = true;
        Dirty(chassis, jetpack);

        Arm(chassis, GetChassisUser(chassis));
    }

    protected override void Stop(Entity<ModuleJetpackComponent> ent, EntityUid chassis)
    {
        if (!ent.Comp.Applied)
            return;

        ent.Comp.Applied = false;

        if (TerminatingOrDeleted(chassis) || !TryComp<JetpackComponent>(chassis, out var jetpack))
            return;

        Disarm(chassis, jetpack);
        RemComp<JetpackComponent>(chassis);
    }

    protected override void UserChanged(Entity<ModuleJetpackComponent> ent, EntityUid chassis, EntityUid? user)
    {
        if (!ent.Comp.Applied || !TryComp<JetpackComponent>(chassis, out var jetpack))
            return;

        Disarm(chassis, jetpack);
        Arm(chassis, user);
    }

    /// <summary>
    ///     Marks the wearer as someone whose suit is waiting for weightlessness, and
    ///     decides on the spot whether it should already be firing.
    /// </summary>
    private void Arm(EntityUid chassis, EntityUid? user)
    {
        if (user is not { } wearer)
            return;

        EnsureComp<ChassisJetpackUserComponent>(wearer, out var armed);
        armed.Chassis = chassis;
        Dirty(wearer, armed);

        Evaluate(chassis, wearer);
    }

    /// <summary>
    ///     Takes the thrusters off whoever they were set up on. Deliberately uses the
    ///     jetpack's own record of its user rather than the suit's current wearer: with
    ///     the second, handing the suit over leaves the previous wearer flying.
    /// </summary>
    private void Disarm(EntityUid chassis, JetpackComponent jetpack)
    {
        if (jetpack.JetpackUser is not { } user)
            return;

        RemComp<ChassisJetpackUserComponent>(user);
        _jetpack.SetEnabled(chassis, jetpack, false, user);
    }

    private void OnWeightlessnessChanged(Entity<ChassisJetpackUserComponent> ent, ref WeightlessnessChangedEvent args)
    {
        Evaluate(ent.Comp.Chassis, ent.Owner);
    }

    private void Evaluate(EntityUid chassis, EntityUid user)
    {
        if (TerminatingOrDeleted(chassis) || !TryComp<JetpackComponent>(chassis, out var jetpack))
            return;

        var shouldFire = _gravity.IsWeightless(user);

        if (shouldFire == HasComp<ActiveJetpackComponent>(chassis))
            return;

        _jetpack.SetEnabled(chassis, jetpack, shouldFire, user);

        // Refusing to light is the server's call — only it can see how much gas is in the
        // bottle. Say so, rather than leaving the wearer drifting and guessing.
        if (shouldFire && !HasComp<ActiveJetpackComponent>(chassis))
            _popup.PopupClient(Loc.GetString("chassis-jetpack-no-gas"), chassis, user);
    }
}
