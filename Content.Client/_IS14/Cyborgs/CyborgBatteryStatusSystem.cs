// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Client.Items;
using Content.Client.Items.UI;
using Content.Client.Stylesheets;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared.Power.Components;
using Content.Shared.Power.EntitySystems;
using Content.Shared.Weapons.Ranged.Components;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._IS14.Cyborgs;

public sealed class CyborgBatteryStatusSystem : EntitySystem
{
    [Dependency] private readonly SharedBatterySystem _battery = default!;

    public override void Initialize()
    {
        Subs.ItemStatus<CyborgBatteryStatusComponent>(ent =>
            new CyborgBatteryStatusControl(ent.Owner, EntityManager, _battery));
    }
}

public sealed class CyborgBatteryStatusControl(EntityUid tool, IEntityManager entities, SharedBatterySystem batteries)
    : PollingItemStatusControl<CyborgBatteryStatusControl.Data>
{
    private readonly Label _label = new() { StyleClasses = { StyleClass.ItemStatus } };

    public Data CurrentData => PollData();

    protected override void EnteredTree()
    {
        base.EnteredTree();
        if (_label.Parent == null)
            AddChild(_label);
    }

    protected override Data PollData()
    {
        if (!entities.TryGetComponent<BatteryComponent>(tool, out var battery) ||
            !entities.TryGetComponent<BatteryAmmoProviderComponent>(tool, out var ammo))
            return default;
        return new Data((int) MathF.Round(batteries.GetChargeLevel((tool, battery)) * 100), ammo.Shots, ammo.Capacity);
    }

    protected override void Update(in Data data)
    {
        _label.Text = Loc.GetString("cyborg-battery-ammo-status",
            ("percent", data.Percent), ("shots", data.Shots), ("capacity", data.Capacity));
    }

    public readonly record struct Data(int Percent, int Shots, int Capacity);
}
