using Robust.Client.Graphics;
using Robust.Shared.Graphics;

namespace Content.Client._IS14.Rendering;

/// <summary>
///     Eyes whose viewport must render the world and nothing else — no job icons, no health
///     bars from a medical HUD, no do-after wheels, no floating popups.
///
///     A photograph is a picture of the station, not of the photographer's HUD, so the camera
///     registers its eye here and the overlays that make up that HUD ask before drawing. The
///     alternative — every overlay learning about the OS — would be far worse.
/// </summary>
public static class IS14CleanView
{
    private static readonly HashSet<IEye> Eyes = new();

    public static void Register(IEye eye)
    {
        Eyes.Add(eye);
    }

    public static void Unregister(IEye eye)
    {
        Eyes.Remove(eye);
    }

    /// <summary>True when the viewport currently being drawn into wants no HUD on top of it.</summary>
    public static bool Hidden(in OverlayDrawArgs args)
    {
        return Eyes.Count > 0 && args.Viewport.Eye is { } eye && Eyes.Contains(eye);
    }
}
