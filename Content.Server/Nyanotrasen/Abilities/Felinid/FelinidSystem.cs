// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Actions;
using Content.Shared.Actions.Events;
using Content.Shared.Audio;
using Content.Shared.StatusEffect;
using Content.Shared.Throwing;
using Content.Shared.Item;
using Content.Shared.Inventory;
using Content.Shared.Hands;
using Content.Shared.IdentityManagement;
using Content.Shared.Nutrition.Components;
using Content.Shared.Nutrition.EntitySystems;
using Content.Server.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Server.Medical;
using Content.Server.Nutrition.Components;
using Content.Server.Popups;
using Content.Shared.Body.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Content.Shared.Charges.Systems;
using Content.Shared.Medical;
using Content.Server.Chemistry.EntitySystems;

namespace Content.Server.Nyanotrasen.Abilities.Felinid;

public sealed partial class FelinidSystem : EntitySystem
{

    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly HungerSystem _hungerSystem = default!;
    [Dependency] private readonly VomitSystem _vomitSystem = default!;
    [Dependency] private readonly SolutionContainerSystem _solutionSystem = default!;
    [Dependency] private readonly IRobustRandom _robustRandom = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly InventorySystem _inventorySystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedChargesSystem _sharedChargesSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FelinidComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<FelinidComponent, HairballActionEvent>(OnHairball);
        SubscribeLocalEvent<FelinidComponent, EatMouseActionEvent>(OnEatMouse);
        SubscribeLocalEvent<FelinidComponent, DidEquipHandEvent>(OnEquipped);
        SubscribeLocalEvent<FelinidComponent, DidUnequipHandEvent>(OnUnequipped);
        SubscribeLocalEvent<HairballComponent, ThrowDoHitEvent>(OnHairballHit);
        SubscribeLocalEvent<HairballComponent, GettingPickedUpAttemptEvent>(OnHairballPickupAttempt);
    }

    private Queue<EntityUid> RemQueue = new();

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        foreach (var cat in RemQueue)
        {
            RemComp<CoughingUpHairballComponent>(cat);
        }
        RemQueue.Clear();

        foreach (var (hairballComp, catComp) in EntityQuery<CoughingUpHairballComponent, FelinidComponent>())
        {
            hairballComp.Accumulator += frameTime;
            if (hairballComp.Accumulator < hairballComp.CoughUpTime.TotalSeconds)
                continue;

            hairballComp.Accumulator = 0;
            SpawnHairball(hairballComp.Owner, catComp);
            RemQueue.Enqueue(hairballComp.Owner);
        }
    }

    private void OnInit(EntityUid uid, FelinidComponent component, ComponentInit args)
    {
        if (component.HairballAction != null)
            return;

        //component.HairballAction = Spawn("ActionHairball");
        _actionsSystem.AddAction(uid, ref component.HairballAction, component.HairballActionId);
    }

    private void OnEquipped(EntityUid uid, FelinidComponent component, DidEquipHandEvent args)
    {
        if (!HasComp<FelinidFoodComponent>(args.Equipped))
            return;

        component.EatActionTarget = args.Equipped;

        //component.EatAction = Spawn("ActionEatMouse");
        _actionsSystem.AddAction(uid, ref component.EatAction, component.EatActionId);
    }

    private void OnUnequipped(EntityUid uid, FelinidComponent component, DidUnequipHandEvent args)
    {
        if (args.Unequipped == component.EatActionTarget)
        {
            component.EatActionTarget = null;
            if (component.EatAction != null)
                _actionsSystem.RemoveAction(uid, component.EatAction.Value);
        }
    }

    private void OnHairball(EntityUid uid, FelinidComponent component, HairballActionEvent args)
    {
        if (_inventorySystem.TryGetSlotEntity(uid, "mask", out var maskUid) &&
        //IS14-change start
        TryComp<IngestionBlockerComponent>(maskUid, out var blocker) &&
        //IS14-change end
        blocker.Enabled)
        {
            _popupSystem.PopupEntity(Loc.GetString("hairball-mask", ("mask", maskUid)), uid, uid);
            return;
        }

        _popupSystem.PopupEntity(Loc.GetString("hairball-cough", ("name", Identity.Entity(uid, EntityManager))), uid);
        _audio.PlayPvs("/Audio/Nyanotrasen/Effects/Species/hairball.ogg", uid, AudioHelpers.WithVariation(0.15f));

        EnsureComp<CoughingUpHairballComponent>(uid);
        args.Handled = true;
    }

    private void OnEatMouse(EntityUid uid, FelinidComponent component, EatMouseActionEvent args)
    {
        if (component.EatActionTarget == null)
            return;

        if (!TryComp<SatiationComponent>(uid, out var hunger))
            return;

        if (_hungerSystem.GetHungerThreshold(uid, comp: hunger) == Content.Shared.Nutrition.EntitySystems.HungerThreshold.Overfed)
        {
            _popupSystem.PopupEntity(Loc.GetString("ingestion-other-cannot-ingest-any-more"), uid, uid, Shared.Popups.PopupType.SmallCaution);
            return;
        }

        if (_inventorySystem.TryGetSlotEntity(uid, "mask", out var maskUid) &&
        //IS14-change start
        TryComp<IngestionBlockerComponent>(maskUid, out var blocker) &&
        //IS14-change end
        blocker.Enabled)
        {
            _popupSystem.PopupEntity(Loc.GetString("hairball-mask", ("mask", maskUid)), uid, uid, Shared.Popups.PopupType.SmallCaution);
            return;
        }

        if (component.HairballAction != null)
        {
            _sharedChargesSystem.SetCharges(component.HairballAction.Value, 1); // You get the charge back and that's it. Tough.
            _actionsSystem.SetEnabled(component.HairballAction, true);
        }
        Del(component.EatActionTarget.Value);
        component.EatActionTarget = null;

        _audio.PlayPvs("/Audio/_Goobstation/Heretic/eatfood.ogg", uid, AudioHelpers.WithVariation(0.15f)); // IS14: use the retained eating sound.

        _hungerSystem.ModifyHunger(uid, 50f, hunger);

        if (component.EatAction != null)
            _actionsSystem.RemoveAction(uid, component.EatAction.Value);
    }

    private void SpawnHairball(EntityUid uid, FelinidComponent component)
    {
        //IS14-change start
        var hairball = Spawn(component.HairballPrototype, Transform(uid).Coordinates);
        //IS14-change end
        var hairballComp = Comp<HairballComponent>(hairball);

        if (TryComp<BloodstreamComponent>(uid, out var bloodstream) && bloodstream.BloodSolution.HasValue)
        {
            var temp = _solutionSystem.SplitSolution(bloodstream.BloodSolution.Value, 20);

            if (_solutionSystem.TryGetSolution(hairball, hairballComp.SolutionName, out var hairballSolution))
            {
                _solutionSystem.TryAddSolution(hairballSolution.Value, temp);
            }
        }
    }
    private void OnHairballHit(EntityUid uid, HairballComponent component, ref ThrowDoHitEvent args)
    {
        if (HasComp<FelinidComponent>(args.Target) || !HasComp<StatusEffectsComponent>(args.Target))
            return;
        if (_robustRandom.Prob(0.2f))
            _vomitSystem.Vomit(args.Target);
    }

    private void OnHairballPickupAttempt(EntityUid uid, HairballComponent component, GettingPickedUpAttemptEvent args)
    {
        if (HasComp<FelinidComponent>(args.User) || !HasComp<StatusEffectsComponent>(args.User))
            return;

        if (_robustRandom.Prob(0.2f))
        {
            _vomitSystem.Vomit(args.User);
            args.Cancel();
        }
    }
}
