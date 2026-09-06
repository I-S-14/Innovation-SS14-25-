using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.Prototypes;
using Content.Shared.Light.Components;
using Content.Shared.Popups;
using Content.Shared.Power.EntitySystems;
using Content.Shared.PowerCell;
using Content.Shared.PowerCell.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server._IS14.OS;

/// <summary>
///     Battery drain (Docs §5.6).
///
///     The balance target is that a player who flips the lid open for a few seconds at a time
///     never thinks about the battery, while one who leaves it open — or leaves the torch on —
///     loses the device in about an hour. Closing the lid is what the player controls, so that
///     is what the drain is tied to.
/// </summary>
public sealed class IS14OsPowerSystem : EntitySystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly PowerCellSystem _cell = default!;
    [Dependency] private readonly SharedBatterySystem _battery = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IS14OsSystem _os = default!;

    /// <summary>Watts drawn by the screen and shell while the lid is open.</summary>
    private const float ScreenDraw = 0.08f;

    /// <summary>Watts drawn with the lid shut. Low enough that a stored PDA never dies.</summary>
    private const float StandbyDraw = 0.002f;

    /// <summary>Watts for the built-in light — a full small cell in roughly an hour.</summary>
    private const float LightDraw = 0.1f;

    private TimeSpan _nextTick;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        if (now < _nextTick)
            return;

        const float step = 1f;
        _nextTick = now + TimeSpan.FromSeconds(step);

        var query = EntityQueryEnumerator<IS14OsDeviceComponent, PowerCellSlotComponent>();
        while (query.MoveNext(out var uid, out var device, out _))
        {
            var draw = GetDraw(uid, device);
            if (draw <= 0f)
                continue;

            if (_cell.TryUseCharge((uid, null), draw * step))
                continue;

            // Flat battery. Everything shuts, but the ID slot is mechanical and stays usable —
            // a dead PDA must never lock a player out of doors.
            if (device.LidOpen)
            {
                _popup.PopupEntity(Loc.GetString("is14-os-battery-dead"), uid);
                _os.CloseLid((uid, device));
            }
        }
    }

    private float GetDraw(EntityUid uid, IS14OsDeviceComponent device)
    {
        var draw = 0f;

        if (TryComp(uid, out UnpoweredFlashlightComponent? light) && light.LightOn)
            draw += LightDraw;

        if (!device.LidOpen || !device.Powered)
            return draw + StandbyDraw;

        draw += ScreenDraw;

        // Open apps cost; minimised ones cost half. That is the in-fiction reason the shell
        // stops sending their state, and the reason closing what you are done with matters.
        foreach (var app in device.Open)
        {
            if (!_proto.TryIndex(app, out var proto))
                continue;

            draw += device.Minimized.Contains(app) ? proto.PowerDraw * 0.5f : proto.PowerDraw;
        }

        return draw;
    }

    /// <summary>Charge 0..1, or null when this device has no cell slot at all.</summary>
    public float? GetCharge(EntityUid uid)
    {
        if (!HasComp<PowerCellSlotComponent>(uid))
            return null;

        if (!_cell.TryGetBatteryFromSlot(uid, out var battery))
            return 0f;

        return Math.Clamp(_battery.GetChargeLevel(battery.Value.Owner), 0f, 1f);
    }
}
