// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._IS14.Cyborgs.Prototypes;

/// <summary>
/// A visual variant of a cyborg model, the equivalent of an entry in tgstation's <c>borg_skins</c>.
/// </summary>
[Prototype]
public sealed partial class CyborgSkinPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    /// <summary>
    /// RSI path relative to the texture root.
    /// </summary>
    [DataField(required: true)]
    public ResPath Sprite;

    [DataField(required: true)]
    public string BodyState = string.Empty;

    /// <summary>
    /// Body state used while the cyborg walks.
    /// </summary>
    [DataField]
    public string? MovingBodyState;

    /// <summary>
    /// Eye overlay shown while a player controls the cyborg.
    /// </summary>
    [DataField]
    public string? EyesState;

    /// <summary>
    /// Eye overlay shown while the cyborg has a brain but no player.
    /// </summary>
    [DataField]
    public string? NoMindEyesState;

    /// <summary>
    /// Overlay shown while the headlamp is on.
    /// </summary>
    [DataField]
    public string? LightState;

    /// <summary>
    /// Pixel offset applied to a hat worn by the cyborg.
    /// </summary>
    [DataField]
    public Vector2i HatOffset = Vector2i.Zero;

    /// <summary>
    /// Skins with a built-in hat, or no head at all, can't wear hats.
    /// </summary>
    [DataField]
    public bool NoHat;

    public SpriteSpecifier Icon => new SpriteSpecifier.Rsi(Sprite, BodyState);
}
