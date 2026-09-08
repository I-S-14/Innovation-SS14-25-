using Content.Shared._IS14.OS.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Interaction;
using Content.Shared.Popups;

namespace Content.Server._IS14.OS;

/// <summary>
///     Fitting an expansion board. Memory is the only thing on this platform you can actually
///     upgrade, so this is the whole progression loop in one file: buy a board, gain room for
///     one more serious application.
/// </summary>
public sealed class IS14OsMemoryBoardSystem : EntitySystem
{
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly IS14OsMemorySystem _memory = default!;
    [Dependency] private readonly IS14OsSystem _os = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<IS14OsDeviceComponent, InteractUsingEvent>(OnInteractUsing,
            before: new[] { typeof(ItemSlotsSystem) });
    }

    private void OnInteractUsing(Entity<IS14OsDeviceComponent> ent, ref InteractUsingEvent args)
    {
        if (args.Handled || !TryComp(args.Used, out IS14OsMemoryBoardComponent? board))
            return;

        args.Handled = true;

        if (!TryComp(ent, out IS14OsMemoryComponent? memory))
            return;

        var slots = _memory.GetProfile(ent.Comp)?.MemorySlots ?? 0;
        if (memory.UsedSlots >= slots)
        {
            _popup.PopupEntity(Loc.GetString("is14-os-board-no-slots"), ent, args.User);
            return;
        }

        memory.UsedSlots++;
        memory.ExtraMemory += board.Amount;
        QueueDel(args.Used);

        _popup.PopupEntity(Loc.GetString("is14-os-board-installed", ("amount", board.Amount)), ent, args.User);
        _os.UpdateUi(ent.Owner, ent.Comp);
    }
}
