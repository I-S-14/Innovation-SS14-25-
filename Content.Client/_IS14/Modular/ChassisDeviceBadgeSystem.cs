// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Client.UserInterface.Systems.Hands;
using Content.Shared._IS14.Modular.Behaviours;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Client.UserInterface;
using Robust.Shared.Utility;

namespace Content.Client._IS14.Modular;

/// <summary>
///     Marks the hand slot when it is holding chassis hardware.
///
///     The badge deliberately lives on the slot rather than on the item's own sprite:
///     an extra sprite layer follows the entity everywhere — into the in-hand overlay and
///     into the melee swing animation, which copies the weapon's layers wholesale and has
///     no idea what to do with a state from a foreign sheet.
/// </summary>
public sealed class ChassisDeviceBadgeSystem : EntitySystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IUserInterfaceManager _ui = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SpriteSystem _sprites = default!;

    private static readonly SpriteSpecifier Badge =
        new SpriteSpecifier.Rsi(new ResPath("/Textures/_IS14/Objects/Modsuit/badge.rsi"), "mod");

    private Texture? _badge;

    /// <summary>
    ///     Driven per frame rather than from hand events: hands are added, swapped and
    ///     rebuilt from several places, and a badge that is merely a decoration is not
    ///     worth chasing every one of them.
    /// </summary>
    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);

        if (_player.LocalEntity is not { } player
            || !TryComp<HandsComponent>(player, out var hands))
            return;

        if (_ui.GetUIController<HandsUIController>() is not { } controller)
            return;

        foreach (var name in hands.Hands.Keys)
        {
            if (!controller.TryGetHandButton(name, out var button))
                continue;

            _hands.TryGetHeldItem((player, hands), name, out var held);

            button.BadgeTexture = held != null && HasComp<ChassisDeviceComponent>(held.Value)
                ? _badge ??= _sprites.Frame0(Badge)
                : null;
        }
    }
}
