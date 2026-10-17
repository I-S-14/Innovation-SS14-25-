// Licensed under IS14's EULA, see EULA.txt for more information.

using System.Linq;
using Content.Server.Silicons.Laws;
using Content.Shared._IS14.Cyborgs.Components;
using Content.Shared._IS14.OS.Components.Apps;
using Content.Shared._IS14.OS.UI.Apps;
using Content.Shared.Damage;
using Content.Shared.Damage.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Systems;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.Silicons.Laws.Components;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.OS.Apps;

/// <summary>
///     tg's RoboTact for IS14 OS: the built-in tablet reports on the cyborg carrying it.
/// </summary>
public sealed class IS14OsCyborgDiagnosticsSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly MobThresholdSystem _thresholds = default!;
    [Dependency] private readonly PowerCellSystem _powerCell = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SiliconLawSystem _laws = default!;

    public const string AppId = "IS14AppCyborgDiagnostics";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14OsCyborgDiagnosticsComponent, OsAppGetStateEvent>(OnGetState);
    }

    private void OnGetState(Entity<IS14OsCyborgDiagnosticsComponent> ent, ref OsAppGetStateEvent args)
    {
        if (args.App != AppId)
            return;

        var state = new OsCyborgDiagnosticsState();
        args.State = state;

        // The tablet sits in the cyborg's own container, so its parent is the chassis.
        var cyborgUid = Transform(ent).ParentUid;
        if (!TryComp<CyborgComponent>(cyborgUid, out var cyborg))
            return;

        state.IsCyborg = true;
        state.Model = _proto.TryIndex(cyborg.Model, out var model) ? Loc.GetString(model.Name) : null;
        state.Slots = cyborg.ToolSlots;
        state.BrokenSlots = cyborg.BrokenSlots;
        state.Emagged = cyborg.EmagToolsUnlocked;
        state.Upgrades = cyborg.UpgradeContainer.ContainedEntities.Select(b => Name(b)).ToList();

        if (_powerCell.TryGetBatteryFromSlot(cyborgUid, out var battery))
            state.Charge = _battery.GetChargeLevel(battery.Value.AsNullable());

        if (TryComp<DamageableComponent>(cyborgUid, out var damageable) &&
            _thresholds.TryGetThresholdForState(cyborgUid, MobState.Critical, out var crit) && crit > 0)
        {
            state.Integrity = Math.Clamp(1f - damageable.TotalDamage.Float() / crit.Value.Float(), 0f, 1f);
        }

        if (TryComp<CyborgAiLinkComponent>(cyborgUid, out var link))
        {
            state.Ai = link.Ai is { } ai && Exists(ai) ? Name(ai) : null;
            state.LawSync = link.LawSync;
            state.Lockdown = link.Lockdown;
        }

        if (HasComp<SiliconLawBoundComponent>(cyborgUid))
        {
            foreach (var law in _laws.GetLaws(cyborgUid).Laws)
            {
                state.Laws.Add($"{law.LawIdentifierOverride ?? law.Order.ToString()}. {Loc.GetString(law.LawString)}");
            }
        }
    }
}
