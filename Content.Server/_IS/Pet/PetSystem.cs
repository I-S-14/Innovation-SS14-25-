using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using Content.Server.Chat.Systems;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Speech;
using Content.Server.Speech.Components;
using Content.Shared._EinsteinEngines.Language.Components;
using Content.Shared._IS.Pet;
using Content.Shared._IS.Pet.Components;
using Content.Shared.Chat;
using Content.Shared.Coordinates;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.NPC.Systems;
using Content.Shared.Pointing;
using Content.Shared.Speech;
using Content.Shared.Speech.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using DependencyAttribute = Robust.Shared.IoC.DependencyAttribute;

namespace Content.Server._IS.Pet;

public sealed partial class PetSystem : SharedPetSystem
{
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private HTNSystem _htn = default!;
    [Dependency] private NPCSystem _npc = default!;
    [Dependency] private ChatSystem _chat = default!;
    [Dependency] private NpcFactionSystem _faction = default!;
    [Dependency] private EntityManager _entityManager = default!;
    [Dependency] private TransformSystem _transformSystem = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PetComponent, ListenEvent>(OnListen);
        SubscribeLocalEvent<PetComponent, InteractUsingEvent>(OnInteractUsing);

        SubscribeLocalEvent<PetOverlordComponent, AfterPointedAtEvent>(OnPointedAt);
        SubscribeLocalEvent<PetMakeOverlordOnOpenComponent, UseInHandEvent>(OnOpen);
    }

    private void OnInteractUsing(Entity<PetComponent> ent, ref InteractUsingEvent args)
    {
        if (!HasComp<PetCreatorComponent>(args.Used))
            return;

        CreatePet(args.User, args.Target);
    }

    public void CreatePet(EntityUid overlord, EntityUid target)
    {
        EnsureComp<ActiveListenerComponent>(target);
        var pComp = EnsureComp<PetComponent>(target);
        var poComp = EnsureComp<PetOverlordComponent>(overlord);
        pComp.Overlord = overlord;
        poComp.Pets.Add(target);
        _npc.SetBlackboard(target, NPCBlackboard.FollowTarget, new EntityCoordinates(overlord, Vector2.Zero));
        if (TryComp<HTNComponent>(target, out var htn))
        {
            _npc.SetBlackboard(target, NPCBlackboard.CurrentOrders, PetOrderType.Follow);
            _htn.Replan(htn);
        }
    }

    private void OnPointedAt(Entity<PetOverlordComponent> ent, ref AfterPointedAtEvent args)
    {
        var component = ent.Comp;
        foreach (var pet in component.Pets)
        {
            if (!TryComp<PetComponent>(pet, out var pComp))
                continue;

            if (pComp.CurrentOrder != PetOrderType.Attack)
                continue;

            _npc.SetBlackboard(pet, NPCBlackboard.CurrentOrderedTarget, args.Pointed);
            _faction.AggroEntity(pet, args.Pointed);
        }
    }

    private void OnListen(Entity<PetComponent> ent, ref ListenEvent args)
    {
        var component = ent.Comp;

        if (ent.Comp.Overlord == null
            || _entityManager.GetComponent<MetaDataComponent>(args.Source).EntityName != _entityManager.GetComponent<MetaDataComponent>(ent.Comp.Overlord.Value).EntityName)
            return;

        if (args.Source != ent.Comp.Overlord)
        {
            if (TryComp<PetOverlordComponent>(ent.Comp.Overlord, out var poCompOv))
                poCompOv.Pets.Remove(ent);
            CreatePet(args.Source, ent);
        }

        var availableOrders = ent.Comp.AvailableOrders.Select(id => _proto.Index(id)).ToHashSet();
        PetOrderPrototype? orderToExecute = null;

        foreach (var order in availableOrders)
        {
            if (!TryComp<LanguageSpeakerComponent>(args.Source, out var lsComp)
                || lsComp.CurrentLanguage != order.Language
                || args.OriginalMessage != order.Message) // language system need
                continue;

            orderToExecute = order;
            break;
        }

        if (orderToExecute == null)
            return;

        var ev = new PetAttemptEvent((ent.Owner, ent.Comp), orderToExecute);
        RaiseLocalEvent(ent, ref ev);

        if (ev.Handled)
            return;

        if (orderToExecute.Order != null)
        {
            UpdatePet(ent, orderToExecute.Order.Value);

        }
        if (orderToExecute.SayOnOrder != null)
            _chat.TrySendInGameICMessage(ent, orderToExecute.SayOnOrder, InGameICChatType.Speak, true, true, checkRadioPrefix: false);

        if (TryComp<PetOverlordComponent>(component.Overlord, out var poComp))
        {
            poComp.CurrentOrder = component.CurrentOrder;
            UpdateActions(component.Overlord.Value, poComp);
        }
    }

    private void OnOpen(Entity<PetMakeOverlordOnOpenComponent> ent, ref UseInHandEvent args)
    {
        var coordinates = _transformSystem.GetMoverCoordinates(ent.Owner);
        var pet = _entityManager.SpawnEntity(ent.Comp.PetToSpawn, coordinates);
        CreatePet(args.User, pet);
        args.Handled = true;
        _entityManager.DeleteEntity(ent.Owner);
    }

    public override void UpdatePet(Entity<PetComponent> ent, PetOrderType order)
    {
        base.UpdatePet(ent, order);

        ent.Comp.CurrentOrder = order;
        _npc.SetBlackboard(ent, NPCBlackboard.CurrentOrders, order);
        if (TryComp<HTNComponent>(ent, out var htn))
            _htn.Replan(htn);
    }

    public override void UpdateActions(EntityUid uid, PetOverlordComponent? component = null)
    {
        base.UpdateActions(uid, component);
    }
}

[ByRefEvent]
public record struct PetAttemptEvent(
    Entity<PetComponent> Pet,
    PetOrderPrototype Order,
    bool Handled = false);
