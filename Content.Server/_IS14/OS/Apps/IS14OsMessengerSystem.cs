using System.Linq;
using Content.Server.Station.Systems;
using Content.Shared._IS14.OS.Components;
using Content.Shared._IS14.OS.Components.Apps;
using Content.Shared._IS14.OS.Files;
using Content.Shared._IS14.OS.UI;
using Content.Shared._IS14.OS.UI.Apps;
using Content.Shared.Access.Components;
using Content.Shared.DeviceNetwork.Components;
using Content.Shared.PDA;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._IS14.OS.Apps;

/// <summary>
///     Station messenger.
///
///     A device's chat number is its network address — the same code already shown in the OS
///     status bar — so there is no second identity to explain, and jamming the network is
///     jamming the chat. Delivery walks the devices that actually have the app installed:
///     uninstalling it really does take you off the network.
/// </summary>
public sealed class IS14OsMessengerSystem : EntitySystem
{
    public const string AppId = "AppMessenger";

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IS14OsSystem _os = default!;
    [Dependency] private readonly IS14OsFileSystem _files = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14OsMessengerComponent, OsAppGetStateEvent>(OnGetState);
        SubscribeLocalEvent<IS14OsMessengerComponent, OsAppEventRaised>(OnAppEvent);
    }

    #region State

    private void OnGetState(Entity<IS14OsMessengerComponent> ent, ref OsAppGetStateEvent args)
    {
        if (args.App != AppId)
            return;

        var state = new OsMessengerState
        {
            OwnAddress = GetAddress(ent) ?? string.Empty,
            OwnName = GetOwnerName(ent),
            OpenChat = ent.Comp.OpenChat,
            Muted = ent.Comp.Muted,
            Error = ent.Comp.Error,
        };

        foreach (var (address, chat) in ent.Comp.Chats)
        {
            var last = chat.Messages.Count > 0 ? chat.Messages[^1] : null;

            state.Chats.Add(new OsChatSummary
            {
                Address = address,
                Name = chat.Name,
                Job = chat.Job,
                Unread = chat.Unread,
                Preview = last == null
                    ? string.Empty
                    : last.Attachment != null && last.Text.Length == 0
                        ? Loc.GetString("is14-os-messenger-preview-attachment")
                        : last.Text,
            });
        }

        // Newest conversation first, but anything unread jumps the queue.
        state.Chats.Sort((a, b) => a.Unread == b.Unread
            ? string.Compare(a.Name, b.Name, StringComparison.CurrentCulture)
            : b.Unread.CompareTo(a.Unread));

        if (ent.Comp.OpenChat is { } open && ent.Comp.Chats.TryGetValue(open, out var log))
        {
            TryComp(ent, out IS14OsMemoryComponent? memory);

            foreach (var entry in log.Messages)
            {
                var file = entry.Attachment is { } id && memory != null ? _files.Get(memory, id) : null;

                state.Messages.Add(new OsChatMessage
                {
                    Outgoing = entry.Outgoing,
                    Text = entry.Text,
                    Time = entry.Time,
                    Attachment = file?.ToMeta(),
                });

                // The bytes only leave the server for the one photo the client asked about, and
                // only if it really is hanging in this conversation.
                if (file == null || file.Id != ent.Comp.ViewPhoto || file.Kind != OsFileKind.Photo)
                    continue;

                state.Photo = new OsFilePayload
                {
                    Id = file.Id,
                    Data = file.Data,
                };
            }
        }

        if (TryComp(ent, out IS14OsMemoryComponent? files))
        {
            foreach (var file in files.Files)
                state.Files.Add(file.ToMeta());
        }

        if (ent.Comp.ShowDirectory)
            state.Directory = BuildDirectory(ent);

        args.State = state;
    }

    /// <summary>
    ///     Everyone on the same station running the messenger. This is the phone book: without
    ///     it nobody could ever start a conversation, since addresses are not printed anywhere.
    /// </summary>
    private List<OsDirectoryEntry> BuildDirectory(EntityUid self)
    {
        var result = new List<OsDirectoryEntry>();
        var station = _station.GetOwningStation(self);
        var selfAddress = GetAddress(self);

        var query = EntityQueryEnumerator<IS14OsMessengerComponent, DeviceNetworkComponent>();
        while (query.MoveNext(out var uid, out _, out var network))
        {
            if (uid == self || network.Address is not { } address || address == selfAddress)
                continue;

            if (_station.GetOwningStation(uid) != station)
                continue;

            var name = GetOwnerName(uid);
            if (string.IsNullOrEmpty(name))
                continue;

            result.Add(new OsDirectoryEntry
            {
                Address = address,
                Name = name,
                Job = GetOwnerJob(uid),
            });
        }

        result.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
        return result;
    }

    #endregion

    #region Input

    private void OnAppEvent(Entity<IS14OsMessengerComponent> ent, ref OsAppEventRaised args)
    {
        if (args.App != AppId)
            return;

        switch (args.Event)
        {
            case OsMessengerOpenChatEvent open:
                ent.Comp.OpenChat = open.Address;
                ent.Comp.Error = null;
                ent.Comp.ViewPhoto = null;

                if (open.Address != null && ent.Comp.Chats.TryGetValue(open.Address, out var log))
                    log.Unread = false;

                break;

            case OsMessengerViewPhotoEvent photo:
                ent.Comp.ViewPhoto = photo.File;
                break;

            case OsMessengerDirectoryEvent directory:
                ent.Comp.ShowDirectory = directory.Show;
                break;

            case OsMessengerMuteEvent:
                ent.Comp.Muted = !ent.Comp.Muted;
                break;

            case OsMessengerDeleteChatEvent delete:
                ent.Comp.Chats.Remove(delete.Address);

                if (ent.Comp.OpenChat == delete.Address)
                    ent.Comp.OpenChat = null;

                break;

            case OsMessengerSendEvent send:
                TrySend(ent, send);
                break;
        }
    }

    private void TrySend(Entity<IS14OsMessengerComponent> ent, OsMessengerSendEvent send)
    {
        ent.Comp.Error = null;

        var text = send.Text.Trim();
        if (text.Length > ent.Comp.MaxMessageLength)
            text = text[..ent.Comp.MaxMessageLength];

        if (text.Length == 0 && send.Attachment == null)
            return;

        if (_timing.CurTime < ent.Comp.LastSend + ent.Comp.SendCooldown)
        {
            ent.Comp.Error = "is14-os-messenger-too-fast";
            return;
        }

        if (FindByAddress(send.Address) is not { } target)
        {
            ent.Comp.Error = "is14-os-messenger-unreachable";
            return;
        }

        OsFile? attachment = null;
        if (send.Attachment is { } fileId && TryComp(ent, out IS14OsMemoryComponent? ownMemory))
        {
            attachment = _files.Get(ownMemory, fileId);
            if (attachment == null)
            {
                ent.Comp.Error = "is14-os-messenger-no-file";
                return;
            }
        }

        var ownAddress = GetAddress(ent);
        if (ownAddress == null)
        {
            ent.Comp.Error = "is14-os-messenger-unreachable";
            return;
        }

        // Deliver first: if the recipient has no room for the attachment the sender should be
        // told, rather than believing a photo arrived that never did.
        int? deliveredAttachment = null;
        if (attachment != null)
        {
            if (!TryComp(target, out IS14OsDeviceComponent? targetDevice)
                || !TryComp(target, out IS14OsMemoryComponent? targetMemory))
            {
                ent.Comp.Error = "is14-os-messenger-unreachable";
                return;
            }

            var copy = _files.Copy((target, targetDevice, targetMemory), attachment);
            if (copy == null)
            {
                ent.Comp.Error = "is14-os-messenger-their-memory";
                return;
            }

            deliveredAttachment = copy.Id;
        }

        ent.Comp.LastSend = _timing.CurTime;

        Append(ent, send.Address, GetOwnerName(target), GetOwnerJob(target), new OsChatEntry
        {
            Outgoing = true,
            Text = text,
            Time = _timing.CurTime,
            Attachment = send.Attachment,
        });

        var targetMessenger = Comp<IS14OsMessengerComponent>(target);
        Append((target, targetMessenger), ownAddress, GetOwnerName(ent), GetOwnerJob(ent), new OsChatEntry
        {
            Outgoing = false,
            Text = text,
            Time = _timing.CurTime,
            Attachment = deliveredAttachment,
        });

        Notify((target, targetMessenger));
        _os.UpdateUi(target);
    }

    #endregion

    private void Append(Entity<IS14OsMessengerComponent> ent,
        string address,
        string name,
        string? job,
        OsChatEntry entry)
    {
        if (!ent.Comp.Chats.TryGetValue(address, out var log))
        {
            if (ent.Comp.Chats.Count >= ent.Comp.MaxChats)
            {
                // Drop the quietest conversation rather than refusing new ones outright.
                var oldest = ent.Comp.Chats.FirstOrDefault(c => !c.Value.Unread).Key;
                if (oldest != null)
                    ent.Comp.Chats.Remove(oldest);
                else
                    return;
            }

            log = new OsChatLog();
            ent.Comp.Chats[address] = log;
        }

        log.Name = string.IsNullOrEmpty(name) ? address : name;
        log.Job = job;
        log.Messages.Add(entry);

        if (!entry.Outgoing && ent.Comp.OpenChat != address)
            log.Unread = true;

        while (log.Messages.Count > ent.Comp.MaxMessagesPerChat)
            log.Messages.RemoveAt(0);
    }

    private void Notify(Entity<IS14OsMessengerComponent> ent)
    {
        if (ent.Comp.Muted)
            return;

        _audio.PlayPvs(ent.Comp.NotifySound, ent);
    }

    private EntityUid? FindByAddress(string address)
    {
        var query = EntityQueryEnumerator<IS14OsMessengerComponent, DeviceNetworkComponent>();
        while (query.MoveNext(out var uid, out _, out var network))
        {
            if (network.Address == address)
                return uid;
        }

        return null;
    }

    private string? GetAddress(EntityUid uid)
    {
        return CompOrNull<DeviceNetworkComponent>(uid)?.Address;
    }

    private string GetOwnerName(EntityUid uid)
    {
        if (!TryComp(uid, out PdaComponent? pda))
            return Name(uid);

        if (TryComp(pda.ContainedId, out IdCardComponent? id) && !string.IsNullOrEmpty(id.FullName))
            return id.FullName!;

        return pda.OwnerName ?? Name(uid);
    }

    private string? GetOwnerJob(EntityUid uid)
    {
        if (!TryComp(uid, out PdaComponent? pda))
            return null;

        return TryComp(pda.ContainedId, out IdCardComponent? id) ? id.LocalizedJobTitle : null;
    }
}
