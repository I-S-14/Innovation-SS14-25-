using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._IS14.OS.Prototypes;

/// <summary>
///     Wallpaper is deliberately its own thing rather than part of the theme (Docs §8.3): the
///     player picks a look and a picture separately, and the picture may be a photo they took
///     themselves. It is the cheapest personalisation on the platform and the most felt.
/// </summary>
[Prototype("osWallpaper")]
public sealed partial class IS14OsWallpaperPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public LocId Name;

    [DataField(required: true)]
    public ResPath Texture = default!;

    /// <summary>Has to be found on a disk or downloaded rather than being there from the start.</summary>
    [DataField]
    public bool Unlockable;
}
