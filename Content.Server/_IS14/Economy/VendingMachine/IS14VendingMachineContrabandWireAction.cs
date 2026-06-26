using Content.Server.Wires;
using Content.Shared._IS14.Economy.VendingMachine;
using Content.Shared.Wires;

namespace Content.Server._IS14.Economy.VendingMachine;

[DataDefinition]
public sealed partial class IS14VendingMachineContrabandWireAction : BaseToggleWireAction
{
    private VendingMachineSystem _vendingMachine = default!;

    public override Color Color { get; set; } = Color.Red;
    public override string Name { get; set; } = "wire-name-is14-vending-contraband";
    public override object? StatusKey { get; } = IS14ContrabandWireKey.StatusKey;

    public override void Initialize()
    {
        base.Initialize();
        _vendingMachine = EntityManager.System<VendingMachineSystem>();
    }

    public override StatusLightState? GetLightState(Wire wire)
    {
        if (EntityManager.TryGetComponent(wire.Owner, out IS14VendingMachineComponent? vending))
            return vending.ContrabandUnlocked ? StatusLightState.BlinkingSlow : StatusLightState.On;

        return StatusLightState.Off;
    }

    public override void ToggleValue(EntityUid owner, bool setting)
    {
        if (EntityManager.TryGetComponent(owner, out IS14VendingMachineComponent? vending))
            _vendingMachine.SetContraband(owner, !vending.ContrabandUnlocked, vending);
    }

    public override bool GetValue(EntityUid owner)
    {
        return EntityManager.TryGetComponent(owner, out IS14VendingMachineComponent? vending) && !vending.ContrabandUnlocked;
    }
}
