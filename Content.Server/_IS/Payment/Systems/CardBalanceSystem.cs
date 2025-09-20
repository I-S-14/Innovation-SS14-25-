using Content.Shared._IS.Payment;
using Content.Shared._IS.Payment.Components;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory;
using Robust.Shared.Random;

namespace Content.Server._IS.Payment.Systems;

/// <summary>
/// This handles card balance operations
/// </summary>
public sealed class CardBalanceSystem : EntitySystem
{
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<CardBalanceComponent, ComponentInit>(OnBalanceInit);
        SubscribeLocalEvent<CardBalanceComponent, BoundUIOpenedEvent>(OnInterfaceOpened);

        SubscribeLocalEvent<CardBalanceComponent, ExaminedEvent>(OnCardExamined);
        SubscribeLocalEvent<CardMoneyComponent, ExaminedEvent>(OnCardMoneyExamined);

        SubscribeLocalEvent<CardMoneyComponent, AfterInteractEvent>(OnChipInsert);

        SubscribeLocalEvent<CardBalanceComponent, WithdrawWindowWithdrawMessage>(OnWithdrawMessage);
    }

    private void OnInterfaceOpened(Entity<CardBalanceComponent> ent, ref BoundUIOpenedEvent _)
    {
        UpdateWithdrawInterface(ent);
    }

    private void OnBalanceInit(Entity<CardBalanceComponent> ent, ref ComponentInit args)
    {
        if (ent.Comp.PersonalAccount == "")
        {
            ent.Comp.PersonalAccount = GeneratePersonalAccount();
            Dirty(ent);
        }
    }

    private string GeneratePersonalAccount()
    {
        var number = _random.Next(1111_1111, 9999_9999);
        return "2202" + number.ToString();
    }

    private void OnWithdrawMessage(Entity<CardBalanceComponent> ent, ref WithdrawWindowWithdrawMessage args)
    {
        TryWithdrawFromCard(ent.Owner, args.Value, ent.Comp);
    }

    private void OnCardMoneyExamined(Entity<CardMoneyComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("card-money-examined", ("value", ent.Comp.Value.ToString())));
    }

    private void OnCardExamined(Entity<CardBalanceComponent> ent, ref ExaminedEvent args)
    {
        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(Loc.GetString("card-balance-examined", ("value", ent.Comp.Balance.ToString())));
    }

    private void OnChipInsert(Entity<CardMoneyComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled || !args.CanReach || args.Target is not { } target
            || !TryComp<CardBalanceComponent>(target, out var money))
            return;

        InsertChipIntoCard((target, money), ent);
        args.Handled = true;
    }

    public void InsertChipIntoCard(Entity<CardBalanceComponent> cardEntity,
                                   Entity<CardMoneyComponent> chipEntity)
    {
        PredictedQueueDel(chipEntity.Owner);
        ChangeCardBalance(cardEntity.Owner, chipEntity.Comp.Value, cardEntity.Comp);
    }

    public void ChangeCardBalance(EntityUid uid, int delta, CardBalanceComponent? cardBalance = null)
    {
        if (!Resolve(uid, ref cardBalance))
            return;

        // TODO Deatherd: Raise 'before' event here

        // Can't be under 0.
        cardBalance.Balance = Math.Clamp(cardBalance.Balance + delta, 0, int.MaxValue);

        UpdateWithdrawInterface((uid,cardBalance));
    }

    public void UpdateWithdrawInterface(Entity<CardBalanceComponent> ent)
    {
        if (_uiSystem.HasUi(ent.Owner, WithdrawWindowUiKey.Key))
        {
            _uiSystem.SetUiState(ent.Owner,
                WithdrawWindowUiKey.Key,
                new WithdrawWindowInterfaceState(ent.Comp.PersonalAccount, ent.Comp.Balance)
            );
        }
    }

    public Entity<CardMoneyComponent>? TryWithdrawFromCard(EntityUid uid, int value, CardBalanceComponent? cardBalance = null)
    {
        if (!Resolve(uid, ref cardBalance) || cardBalance.Balance < value)
            return null;

        ChangeCardBalance(uid, -value, cardBalance);
        return CreateChipWithValue((uid, cardBalance), value);
    }

    public Entity<CardMoneyComponent> CreateChipWithValue(Entity<CardBalanceComponent> cardId, int value)
    {
        _inventory.TryGetContainingEntity(cardId.Owner, out var owner);

        var xform = Transform(cardId);
        var mapCoords = _transform.GetMapCoordinates(xform);

        var chipToSpawn = Spawn(cardId.Comp.WithdrawPrototype, mapCoords);
        EnsureComp<CardMoneyComponent>(chipToSpawn, out var cardMoney);
        cardMoney.Value = value; // TODO Deatherd :  Change with method updating visuals

        // Try give in hands or drop near.
        _handsSystem.PickupOrDrop(owner, chipToSpawn, false);

        return (chipToSpawn, cardMoney);
    }
}
