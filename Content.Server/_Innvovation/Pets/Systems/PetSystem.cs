using System.Diagnostics;
using System.Numerics;
using System.Text.RegularExpressions;
using Content.Server.NPC;
using Content.Server.NPC.HTN;
using Content.Server.NPC.Systems;
using Content.Server.Speech;
using Content.Server.Speech.Components;
using Content.Shared._Innovation.Pets;
using Content.Shared._Innovation.Pets.PetCommands;
using Content.Shared.Mobs;
using Content.Shared.Pointing;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Robust.Server._Innovation.Pets.Components;
using Robust.Shared._Innovation.Pets;
using Robust.Shared._Innovation.Pets.Components;
using Robust.Shared._Innovation.Pets.Systems;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Robust.Server._Innovation.Pets.Systems;

public sealed partial class PetSystem : SharedPetSystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly NPCSystem _npc = default!;
    [Dependency] private readonly HTNSystem _htn = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<PetOwnerComponent, MapInitEvent>(OnOwnerInit);

        SubscribeLocalEvent<PetComponent, MapInitEvent>(OnPetInit);
        SubscribeLocalEvent<PetComponent, ListenEvent>(OnListen);
        SubscribeLocalEvent<PetComponent, PetUpdateHtnTask>(OnUpdateHtnTask);
        SubscribeLocalEvent<PetOwnerComponent, AfterPointedAtEvent>(OnPointedAt);
    }

    public void UpdatePetNpc(EntityUid uid, PetOrderType orderType)
    {
        if (!TryComp<HTNComponent>(uid, out var htn))
            return;

        if (htn.Plan != null)
            _htn.ShutdownPlan(htn);

        _npc.SetBlackboard(uid, NPCBlackboard.CurrentOrders, orderType);
        _htn.Replan(htn);
    }

    private void OnOwnerInit(EntityUid uid, PetOwnerComponent component, MapInitEvent args)
    {
        foreach (var pet in component.Pets)
        {
            if (!_prototypeManager.TryIndex<PetPrototype>(pet.Id, out var petProto)) continue;
            var petEnt = SpawnAtPosition(petProto.Pet, Transform(uid).Coordinates);
            MakePet(petEnt, uid, petProto.Commands);
        }
    }

    private void OnPetInit(EntityUid uid, PetComponent component, MapInitEvent args)
    {
        EnsureComp<ActiveListenerComponent>(uid);
        if (component.PetOwner != null)
            _npc.SetBlackboard(uid, NPCBlackboard.FollowTarget, new EntityCoordinates(component.PetOwner.Value, Vector2.Zero));
        UpdatePetNpc(uid, component.CurrentOrderType);
    }

    private void OnListen(EntityUid uid, PetComponent component, ListenEvent args)
    {
        if (args.Source != component.PetOwner) return;

        if (SetCurrentCommand(uid, args.Message))
            ExecuteCommand(uid, component);
    }

    private void ExecuteCommand(EntityUid uid, PetComponent component)
    {
        var args = new PetBaseArgs(uid, EntityManager);
        component.Commands[component.CurrentCommand].Execute(args);
    }

    private void OnUpdateHtnTask(EntityUid uid, PetComponent component, PetUpdateHtnTask args)
    {
        switch (component.CurrentOrderType)
        {
            case PetOrderType.Follow:
                _npc.SetBlackboard(uid, NPCBlackboard.FollowTarget, new EntityCoordinates(component.PetOwner!.Value, Vector2.Zero));
                break;
            case PetOrderType.Attack:
                _npc.SetBlackboard(uid, NPCBlackboard.CurrentOrderedTarget, new EntityCoordinates(component.PetOwner!.Value, Vector2.Zero));
                break;
            case PetOrderType.Stay:
                _npc.SetBlackboard(uid, NPCBlackboard.CurrentOrders, PetOrderType.Stay);
                break;
        }

        UpdatePetNpc(uid, component.CurrentOrderType);
    }


    private void OnPointedAt(EntityUid uid, PetOwnerComponent component, AfterPointedAtEvent args)
    {
        var query = EntityQueryEnumerator<PetComponent>();

        while (query.MoveNext(out var petUid, out var petComponent))
        {
            if (petComponent.PetOwner != uid)// || petComponent.CurrentOrderType != PetOrderType.Attack)
                continue;

            _npc.SetBlackboard(petUid, NPCBlackboard.CurrentOrderedTarget, args.Pointed);
            Logger.Debug()
        }
    }

}
