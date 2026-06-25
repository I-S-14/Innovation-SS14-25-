using Content.Shared._IS14.Economy;
using Content.Shared.Examine;

namespace Content.Server._IS14.Economy;

public sealed class BankAccountHolderSystem : EntitySystem
{
    [Dependency] private readonly BankManagerSystem _bankManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<BankAccountHolderComponent, ExaminedEvent>(OnExamine);
    }

    private void OnExamine(Entity<BankAccountHolderComponent> ent, ref ExaminedEvent args)
    {
        var account = _bankManager.GetAccount(ent.Comp.AccountNumber);
        if (account == null)
            return;

        args.PushMarkup(Loc.GetString("bank-holder-examine-balance", ("balance", account.Balance)));
    }
}
