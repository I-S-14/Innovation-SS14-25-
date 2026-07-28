using System.Linq;
using Content.Server.Access.Systems;
using Content.Server.CartridgeLoader;
using Content.Server.Station.Systems;
using Content.Server.StationRecords.Systems;
using Content.Shared._IS14.CCVar;
using Content.Shared._IS14.Economy.Fines;
using Content.Shared.Access.Components;
using Content.Shared.CartridgeLoader;
using Content.Shared.StationRecords;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;

namespace Content.Server._IS14.Economy.Fines;

/// <summary>
/// Drives the fine-writing program on Security PDAs.
/// </summary>
public sealed class FineCartridgeSystem : EntitySystem
{
    [Dependency] private readonly CartridgeLoaderSystem _cartridgeLoader = default!;
    [Dependency] private readonly FineSystem _fines = default!;
    [Dependency] private readonly StationRecordsSystem _records = default!;
    [Dependency] private readonly StationSystem _station = default!;
    [Dependency] private readonly IdCardSystem _idCard = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<FineCartridgeComponent, CartridgeUiReadyEvent>(OnUiReady);
        SubscribeLocalEvent<FineCartridgeComponent, CartridgeMessageEvent>(OnUiMessage);
    }

    private void OnUiReady(Entity<FineCartridgeComponent> ent, ref CartridgeUiReadyEvent args)
    {
        UpdateUi(ent, args.Loader);
    }

    private void OnUiMessage(Entity<FineCartridgeComponent> ent, ref CartridgeMessageEvent args)
    {
        if (args is not FineCartridgeUiMessageEvent message)
            return;

        var loader = GetEntity(args.LoaderUid);
        if (_station.GetOwningStation(loader) is not { } station)
        {
            UpdateUi(ent, loader, Loc.GetString("is14-fine-status-no-station"));
            return;
        }

        var officer = GetOfficerName(args.Actor);
        var status = message.Action switch
        {
            FineCartridgeAction.Issue => Issue(station, message, officer),
            FineCartridgeAction.Void => Void(station, message, officer),
            _ => string.Empty,
        };

        UpdateUi(ent, loader, status);
    }

    private string Issue(EntityUid station, FineCartridgeUiMessageEvent message, string officer)
    {
        if (!_prototypes.TryIndex<FinePresetPrototype>(message.PresetId, out var preset))
            return Loc.GetString("is14-fine-status-bad-article");

        var article = Loc.GetString(preset.Name);
        var fine = _fines.TryIssueFine(station, message.RecordId, article, message.Amount, officer);

        if (fine == null)
            return Loc.GetString("is14-fine-status-rejected", ("max", _cfg.GetCVar(IS14CVars.FineMaxAmount)));

        return Loc.GetString("is14-fine-status-issued",
            ("name", fine.TargetName),
            ("amount", fine.Amount));
    }

    private string Void(EntityUid station, FineCartridgeUiMessageEvent message, string officer)
    {
        return _fines.TryVoidFine(station, message.FineId, officer)
            ? Loc.GetString("is14-fine-status-voided")
            : Loc.GetString("is14-fine-status-not-found");
    }

    /// <summary>
    /// Fines are signed with the name on the officer's ID card, so an officer who
    /// hands their card over signs with someone else's name — as it should be.
    /// </summary>
    private string GetOfficerName(EntityUid actor)
    {
        if (_idCard.TryFindIdCard(actor, out var card) && !string.IsNullOrWhiteSpace(card.Comp.FullName))
            return card.Comp.FullName!;

        return Name(actor);
    }

    private void UpdateUi(Entity<FineCartridgeComponent> ent, EntityUid loader, string status = "")
    {
        var targets = new List<FineTargetEntry>();
        var fines = new List<FineRecord>();

        if (_station.GetOwningStation(loader) is { } station)
        {
            foreach (var (id, record) in _records.GetRecordsOfType<GeneralStationRecord>(station))
            {
                targets.Add(new FineTargetEntry(
                    id,
                    record.Name,
                    record.JobTitle,
                    _fines.GetOutstandingAmount(station, id)));
            }

            targets = targets.OrderBy(t => t.Name).ToList();

            // Newest first: an officer almost always wants the fine they just wrote.
            fines = _fines.GetFines(station).AsEnumerable().Reverse().ToList();
        }

        var articles = _prototypes.EnumeratePrototypes<FinePresetPrototype>()
            .OrderBy(p => p.Order)
            .Select(p => new FineArticleEntry(p.ID, Loc.GetString(p.Name), p.Amount))
            .ToList();

        var state = new FineCartridgeUiState(
            targets,
            articles,
            fines,
            _cfg.GetCVar(IS14CVars.FineMaxAmount),
            status);

        _cartridgeLoader.UpdateCartridgeUiState(loader, state);
    }
}
