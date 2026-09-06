using Robust.Shared.Audio;

namespace Content.Shared._IS14.OS.Components.Apps;

/// <summary>
///     Messenger data. It lives on the device, not on the ID card: your conversations are the
///     device's, so a stolen PDA is a stolen inbox — which is the interesting outcome.
/// </summary>
[RegisterComponent]
public sealed partial class IS14OsMessengerComponent : Component
{
    /// <summary>Conversations keyed by the other device's network address.</summary>
    [DataField]
    public Dictionary<string, OsChatLog> Chats = new();

    [ViewVariables]
    public string? OpenChat;

    /// <summary>Whether the directory tab is showing, so it is only built when looked at.</summary>
    [ViewVariables]
    public bool ShowDirectory;

    /// <summary>
    ///     File the client is currently asking the bytes of, so a photo can be drawn inside its
    ///     bubble. One at a time, and never persisted — a request, not state.
    /// </summary>
    [ViewVariables]
    public int? ViewPhoto;

    [DataField]
    public bool Muted;

    [DataField]
    public int MaxChats = 40;

    [DataField]
    public int MaxMessagesPerChat = 60;

    [DataField]
    public int MaxMessageLength = 256;

    /// <summary>Rate limit, so a spammer cannot flood someone's inbox from a macro.</summary>
    [DataField]
    public TimeSpan SendCooldown = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public TimeSpan LastSend;

    [ViewVariables]
    public string? Error;

    [DataField]
    public SoundSpecifier NotifySound = new SoundPathSpecifier("/Audio/Effects/Cargo/ping.ogg");
}

[DataDefinition]
public sealed partial class OsChatLog
{
    [DataField]
    public string Name = string.Empty;

    [DataField]
    public string? Job;

    [DataField]
    public List<OsChatEntry> Messages = new();

    [DataField]
    public bool Unread;
}

[DataDefinition]
public sealed partial class OsChatEntry
{
    [DataField]
    public bool Outgoing;

    [DataField]
    public string Text = string.Empty;

    [DataField]
    public TimeSpan Time;

    /// <summary>Id of the attached file on *this* device, once it has been copied over.</summary>
    [DataField]
    public int? Attachment;
}
