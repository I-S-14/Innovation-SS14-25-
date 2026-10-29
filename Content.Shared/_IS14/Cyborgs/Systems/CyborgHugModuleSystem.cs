// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Bed.Sleep;
using Content.Shared.Damage;
using Content.Shared.Damage.Systems;
using Content.Shared.Database;
using Content.Shared.Administration.Logs;
using Content.Shared.Examine;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.PowerCell;
using Content.Shared.Timing;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._IS14.Cyborgs.Systems;

public sealed class CyborgHugModuleSystem : EntitySystem
{
    [Dependency] private readonly ISharedAdminLogManager _adminLog = default!;
    [Dependency] private readonly DamageableSystem _damageable = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly SharedStaminaSystem _stamina = default!;
    [Dependency] private readonly SleepingSystem _sleeping = default!;
    [Dependency] private readonly UseDelaySystem _useDelay = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CyborgHugModuleComponent, UseInHandEvent>(OnUseInHand);
        SubscribeLocalEvent<CyborgHugModuleComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<CyborgHugModuleComponent, ExaminedEvent>(OnExamined);
    }

    private bool IsEmagged(EntityUid tool)
    {
        return CompOrNull<CyborgToolComponent>(tool)?.Cyborg is { } cyborg &&
               CompOrNull<CyborgComponent>(cyborg)?.EmagToolsUnlocked == true;
    }

    private bool IsModeAvailable(Entity<CyborgHugModuleComponent> ent, CyborgHugMode mode)
    {
        return mode switch
        {
            CyborgHugMode.Hug => true,
            CyborgHugMode.Shock => ent.Comp.ShockAllowed || IsEmagged(ent),
            CyborgHugMode.Crush => IsEmagged(ent),
            _ => false,
        };
    }

    private void OnUseInHand(Entity<CyborgHugModuleComponent> ent, ref UseInHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;

        var next = ent.Comp.Mode;
        for (var i = 0; i < 3; i++)
        {
            next = (CyborgHugMode) (((int) next + 1) % 3);
            if (IsModeAvailable(ent, next))
                break;
        }

        if (next == ent.Comp.Mode)
            return;

        ent.Comp.Mode = next;
        Dirty(ent);
        _popup.PopupClient(Loc.GetString("cyborg-hug-mode", ("mode", ModeName(next))), ent, args.User);
    }

    private void OnExamined(Entity<CyborgHugModuleComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("cyborg-hug-examine-mode", ("mode", ModeName(ent.Comp.Mode))));
    }

    private string ModeName(CyborgHugMode mode)
    {
        return Loc.GetString($"cyborg-hug-mode-{mode.ToString().ToLowerInvariant()}");
    }

    private void OnAfterInteract(Entity<CyborgHugModuleComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target || target == args.User ||
            !HasComp<MobStateComponent>(target))
            return;

        args.Handled = true;

        if (TryComp<UseDelayComponent>(ent, out var delay) && !_useDelay.TryResetDelay((ent, delay), true))
            return;

        // A cyborg that lost its emag tools falls back to hugging.
        var mode = IsModeAvailable(ent, ent.Comp.Mode) ? ent.Comp.Mode : CyborgHugMode.Hug;
        var user = args.User;
        var name = Identity.Name(target, EntityManager, user);
        var userName = Identity.Name(user, EntityManager);

        switch (mode)
        {
            case CyborgHugMode.Hug:
                // tg hugs people out of sleep.
                _sleeping.TryWaking(target, user: user);
                _audio.PlayPredicted(ent.Comp.HugSound, target, user);
                _popup.PopupPredicted(Loc.GetString("cyborg-hug-hug", ("user", userName), ("target", name)),
                    target, user);
                break;

            case CyborgHugMode.Shock:
                if (!_powerCell.TryUseCharge(user, ent.Comp.ShockCharge, user, predicted: true))
                {
                    _popup.PopupClient(Loc.GetString("cyborg-tools-no-power"), ent, user);
                    return;
                }

                _stamina.TakeStaminaDamage(target, ent.Comp.ShockStamina, source: user, with: ent);
                _audio.PlayPredicted(ent.Comp.ShockSound, target, user);
                _popup.PopupPredicted(Loc.GetString("cyborg-hug-shock", ("user", userName), ("target", name)),
                    target, user, PopupType.MediumCaution);
                _adminLog.Add(LogType.Stamina, LogImpact.Medium,
                    $"{ToPrettyString(user):player} shocked {ToPrettyString(target):target} with a hug module");
                break;

            case CyborgHugMode.Crush:
                if (!_powerCell.TryUseCharge(user, ent.Comp.CrushCharge, user, predicted: true))
                {
                    _popup.PopupClient(Loc.GetString("cyborg-tools-no-power"), ent, user);
                    return;
                }

                _damageable.TryChangeDamage(target, ent.Comp.CrushDamage, origin: user);
                _audio.PlayPredicted(ent.Comp.CrushSound, target, user);
                _popup.PopupPredicted(Loc.GetString("cyborg-hug-crush", ("user", userName), ("target", name)),
                    target, user, PopupType.LargeCaution);
                _adminLog.Add(LogType.Damaged, LogImpact.Medium,
                    $"{ToPrettyString(user):player} crushed {ToPrettyString(target):target} with a hug module");
                break;
        }
    }
}
