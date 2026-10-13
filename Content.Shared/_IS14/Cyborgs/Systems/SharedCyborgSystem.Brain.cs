// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Database;
using Content.Shared.Interaction;
using Content.Shared.Mind.Components;
using Content.Shared.Silicons.Borgs.Components;
using Content.Shared.Verbs;
using Content.Shared.Wires;
using Robust.Shared.Player;

namespace Content.Shared._IS14.Cyborgs.Systems;

public abstract partial class SharedCyborgSystem
{
    private void InitializeBrain()
    {
        SubscribeLocalEvent<CyborgComponent, AfterInteractUsingEvent>(OnInteractUsing);
        SubscribeLocalEvent<CyborgComponent, GetVerbsEvent<AlternativeVerb>>(OnGetAltVerbs);
        SubscribeLocalEvent<CyborgInstalledBrainComponent, MindAddedMessage>(OnInstalledBrainMindAdded);
    }

    private void OnGetAltVerbs(Entity<CyborgComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract || args.User == ent.Owner || ent.Comp.BrainEntity == null)
            return;

        if (!TryComp<WiresPanelComponent>(ent, out var panel) || !panel.Open)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("cyborg-verb-eject-brain"),
            Act = () => TryEjectBrain(ent, user),
            Priority = -1,
        });
    }

    private void OnInteractUsing(Entity<CyborgComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (!args.CanReach || args.Handled || args.User == ent.Owner)
            return;

        if (TryComp<CyborgUpgradeComponent>(args.Used, out var upgrade))
        {
            args.Handled = true;
            TryInstallUpgrade(ent, (args.Used, upgrade), args.User);
            return;
        }

        if (!HasComp<BorgBrainComponent>(args.Used))
            return;

        args.Handled = true;

        if (TryComp<WiresPanelComponent>(ent, out var panel) && !panel.Open)
        {
            _popup.PopupClient(Loc.GetString("borg-panel-not-open"), ent, args.User);
            return;
        }

        if (ent.Comp.BrainEntity != null || _whitelist.IsWhitelistFail(ent.Comp.BrainWhitelist, args.Used))
            return;

        if (TryComp<ActorComponent>(args.Used, out var actor) && !CanPlayerBeBorged(actor.PlayerSession))
        {
            // Not predicted, CanPlayerBeBorged only knows the answer on the server.
            _popup.PopupEntity(Loc.GetString("borg-player-not-allowed"), args.Used, args.User);
            return;
        }

        _container.Insert(args.Used, ent.Comp.BrainContainer);
        _adminLog.Add(LogType.Action, LogImpact.Medium,
            $"{ToPrettyString(args.User):player} installed brain {ToPrettyString(args.Used)} into cyborg {ToPrettyString(ent.Owner)}");
    }

    private void OnBrainInserted(Entity<CyborgComponent> ent, EntityUid brain)
    {
        if (!HasComp<BorgBrainComponent>(brain))
            return;

        EnsureComp<CyborgInstalledBrainComponent>(brain).Cyborg = ent.Owner;
        _appearance.SetData(ent.Owner, CyborgVisuals.HasBrain, true);

        if (_mind.TryGetMind(brain, out var mindId, out var mind))
            _mind.TransferTo(mindId, ent.Owner, mind: mind);
    }

    private void OnBrainRemoved(Entity<CyborgComponent> ent, EntityUid brain)
    {
        RemComp<CyborgInstalledBrainComponent>(brain);
        _appearance.SetData(ent.Owner, CyborgVisuals.HasBrain, false);

        if (HasComp<BorgBrainComponent>(brain) && _mind.TryGetMind(ent.Owner, out var mindId, out var mind))
            _mind.TransferTo(mindId, brain, mind: mind);
    }

    // A positronic brain that gets a ghost while installed moves it into the chassis.
    private void OnInstalledBrainMindAdded(Entity<CyborgInstalledBrainComponent> brain, ref MindAddedMessage args)
    {
        if (!TryComp<CyborgComponent>(brain.Comp.Cyborg, out var cyborg) || cyborg.BrainEntity != brain.Owner)
            return;

        if (!_mind.TryGetMind(brain.Owner, out var mindId, out var mind) ||
            !_player.TryGetSessionById(mind.UserId, out var session))
            return;

        if (!CanPlayerBeBorged(session))
        {
            // Not predicted, CanPlayerBeBorged only knows the answer on the server.
            _popup.PopupEntity(Loc.GetString("borg-player-not-allowed-eject"), brain);
            _container.RemoveEntity(brain.Comp.Cyborg, brain);
            return;
        }

        _mind.TransferTo(mindId, brain.Comp.Cyborg, mind: mind);
    }

    /// <summary>
    /// Ejects the brain, moving the mind back into it.
    /// </summary>
    public bool TryEjectBrain(Entity<CyborgComponent> ent, EntityUid? user = null)
    {
        if (ent.Comp.BrainEntity is not { } brain)
            return false;

        _container.RemoveEntity(ent.Owner, brain);
        if (user != null)
            _hands.PickupOrDrop(user.Value, brain);

        if (user != null)
            _adminLog.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(user.Value):player} removed brain {ToPrettyString(brain)} from cyborg {ToPrettyString(ent.Owner)}");
        return true;
    }
}
