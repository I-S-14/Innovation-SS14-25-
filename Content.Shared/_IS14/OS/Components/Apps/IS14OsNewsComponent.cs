namespace Content.Shared._IS14.OS.Components.Apps;

/// <summary>
///     News reader. The articles belong to the station, not the device, so all this holds is
///     which one the player is currently reading.
/// </summary>
[RegisterComponent]
public sealed partial class IS14OsNewsComponent : Component
{
    /// <summary>Index of the open article, or null while the headline list is showing.</summary>
    [ViewVariables]
    public int? Reading;

    /// <summary>
    ///     How many articles existed when the app last showed the list. A newer count is what
    ///     the unread badge is made of — the station press is a broadcast, so "since you last
    ///     looked" is the only per-device thing there is to know about it.
    /// </summary>
    [DataField]
    public int SeenCount;
}
