using CorvaxPlantAnalyzerComponent = Content.Shared._CorvaxGoob.Botany.Components.CorvaxPlantAnalyzerComponent;
using Content.Shared.Botany.Components; // IS14-change: upstream namespace move
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Shared.Botany.Systems;
using Content.Shared.Botany.Items.Components;
using Content.Shared.Botany.Traits.Components;
using Robust.Shared.Prototypes;
using Content.Server.Popups;
using Content.Shared._CorvaxGoob.AbstractAnalyzer;
using Content.Shared._CorvaxGoob.Botany.Components;
using Content.Shared._CorvaxGoob.Botany.PlantAnalyzer;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.Labels.EntitySystems;
using Content.Shared.Paper;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Server._CorvaxGoob.Botany.Systems;

public sealed class CorvaxPlantAnalyzerSystem : AbstractAnalyzerSystem<CorvaxPlantAnalyzerComponent, CorvaxPlantAnalyzerDoAfterEvent>
{
    [Dependency] private readonly BotanySystem _botany = default!;
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IGameTiming _gameTiming = default!;
    [Dependency] private readonly PopupSystem _popupSystem = default!;
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly PaperSystem _paperSystem = default!;
    [Dependency] private readonly LabelSystem _labelSystem = default!;
    [Dependency] private readonly CorvaxPlantAnalyzerLocalizationHelper _localizationHelper = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CorvaxPlantAnalyzerComponent, CorvaxPlantAnalyzerPrintMessage>(OnPrint);
    }

    /// <inheritdoc/>
    public override void UpdateScannedUser(EntityUid analyzer, EntityUid target, bool scanMode)
    {
        if (!_uiSystem.HasUi(analyzer, CorvaxPlantAnalyzerUiKey.Key))
            return;

        if (!ValidScanTarget(target))
            return;

        if (!_entityManager.TryGetComponent<CorvaxPlantAnalyzerComponent>(analyzer, out var analyzerComponent))
            return;

        _uiSystem.ServerSendUiMessage(analyzer, CorvaxPlantAnalyzerUiKey.Key, GatherData(analyzerComponent, scanMode, target: target));
    }

    private CorvaxPlantAnalyzerScannedUserMessage GatherData(CorvaxPlantAnalyzerComponent analyzer, bool? scanMode = null, EntityUid? target = null)
    {
        target ??= analyzer.ScannedEntity;
        EntityUid? plantEntity = target;
        EntProtoId? plantPrototype = null;
        TryComp<PlantTrayComponent>(target, out var tray);
        if (tray != null)
            plantEntity = tray.PlantEntity;
        else if (TryComp<SeedComponent>(target, out var seed))
        {
            plantEntity = seed.PlantData;
            plantPrototype = seed.PlantProtoId;
        }
        else if (TryComp<ProduceComponent>(target, out var produce))
        {
            plantEntity = produce.PlantData;
            plantPrototype = produce.PlantProtoId;
        }

        T? ReadPlant<T>() where T : class, IComponent, new()
            => _botany.TryGetPlantComponent<T>(plantEntity, plantPrototype, out var component) ? component : null;

        CorvaxPlantAnalyzerPlantData? plantData = null;
        CorvaxPlantAnalyzerTolerancesData? tolerancesData = null;
        CorvaxPlantAnalyzerProduceData? produceData = null;
        CorvaxPlantAnalyzerTrayData? trayData = null;
        var plant = ReadPlant<PlantComponent>();
        var lifecycle = ReadPlant<PlantHolderComponent>();
        var species = ReadPlant<PlantDataComponent>();
        if (plant != null && species != null)
        {
            plantData = new CorvaxPlantAnalyzerPlantData(species.Name,
                lifecycle?.Health ?? plant.Endurance, plant.Endurance, lifecycle?.Age ?? 0, plant.Lifespan,
                lifecycle?.Dead ?? false, ReadPlant<PlantTraitUnviableComponent>() == null,
                lifecycle?.MutationLevels.Values.Any(value => value > 0) ?? false,
                ReadPlant<PlantTraitKudzuComponent>() != null);
            var growth = ReadPlant<PlantGrowthComponent>();
            var atmosphere = ReadPlant<PlantAtmosphericComponent>();
            var toxins = ReadPlant<PlantToxinsComponent>();
            var pests = ReadPlant<PlantWeedPestComponent>();
            var gases = ReadPlant<PlantConsumeExudeGasComponent>();
            tolerancesData = new CorvaxPlantAnalyzerTolerancesData(
                growth?.NutrientConsumption ?? 0, growth?.WaterConsumption ?? 0,
                atmosphere == null ? 0 : (atmosphere.LowHeatTolerance + atmosphere.HighHeatTolerance) / 2,
                atmosphere == null ? 0 : (atmosphere.HighHeatTolerance - atmosphere.LowHeatTolerance) / 2,
                null, null, toxins?.ToxinsTolerance ?? 0,
                atmosphere?.LowPressureTolerance ?? 0, atmosphere?.HighPressureTolerance ?? 0,
                pests?.PestTolerance ?? 0, pests?.WeedTolerance ?? 0,
                gases?.ConsumeGasses.Keys.ToList() ?? []);
            var chemicals = ReadPlant<PlantChemicalsComponent>();
            produceData = new CorvaxPlantAnalyzerProduceData(
                species.ProductPrototypes.Count == 0 ? 0 : plant.Yield * (lifecycle?.YieldMod ?? 1),
                plant.Potency, chemicals?.Chemicals.Keys.Select(id => id.Id).ToList() ?? [],
                species.ProductPrototypes.ToList(), gases?.ExudeGasses.Keys.ToList() ?? [],
                ReadPlant<PlantTraitSeedlessComponent>() != null);
        }
        if (tray != null)
        {
            trayData = new CorvaxPlantAnalyzerTrayData(tray.WaterLevel, tray.NutritionLevel,
                tray.ToxinLevel, tray.PestLevel, tray.WeedLevel,
                tray.SoilSolution?.Comp.Solution.Contents.Select(reagent => reagent.Reagent.Prototype.Id).ToList());
        }
        return new CorvaxPlantAnalyzerScannedUserMessage(GetNetEntity(target), scanMode,
            plantData, trayData, tolerancesData, produceData, analyzer.PrintReadyAt);
    }

    private void OnPrint(EntityUid uid, CorvaxPlantAnalyzerComponent component, CorvaxPlantAnalyzerPrintMessage args)
    {
        var user = args.Actor;

        if (_gameTiming.CurTime < component.PrintReadyAt)
        {
            // This shouldn't occur due to the UI guarding against it, but
            // if it does, tell the user why nothing happened.
            _popupSystem.PopupEntity(Loc.GetString("forensic-scanner-printer-not-ready"), uid, user);
            return;
        }

        // Spawn a piece of paper.
        //IS14-change start
        var printed = Spawn(component.MachineOutput, Transform(uid).Coordinates);
        //IS14-change end
        _handsSystem.PickupOrDrop(args.Actor, printed, checkActionBlocker: false);

        if (!TryComp<PaperComponent>(printed, out var paperComp))
        {
            Log.Error("Printed paper did not have PaperComponent.");
            return;
        }

        var data = GatherData(component);
        var missingData = Loc.GetString("plant-analyzer-printout-missing");

        var seedName = data.PlantData is not null ? Loc.GetString(data.PlantData.SeedDisplayName) : null;
        (string, object)[] parameters = [
            ("seedName", seedName ?? missingData),
            ("produce", data.ProduceData is not null ? _localizationHelper.ProduceToLocalizedStrings(data.ProduceData.Produce).Plural : missingData),
            ("water", data.TolerancesData?.WaterConsumption.ToString("0.00") ?? missingData),
            ("nutrients", data.TolerancesData?.NutrientConsumption.ToString("0.00") ?? missingData),
            ("toxins", data.TolerancesData?.ToxinsTolerance.ToString("0.00") ?? missingData),
            ("pests", data.TolerancesData?.PestTolerance.ToString("0.00") ?? missingData),
            ("weeds", data.TolerancesData?.WeedTolerance.ToString("0.00") ?? missingData),
            ("gasesIn", data.TolerancesData is not null ? _localizationHelper.GasesToLocalizedStrings(data.TolerancesData.ConsumeGasses) : missingData),
            ("kpa", data.TolerancesData?.IdealPressure.ToString("0.00") ?? missingData),
            ("kpaTolerance", data.TolerancesData?.PressureTolerance.ToString("0.00") ?? missingData),
            ("temp", data.TolerancesData?.IdealHeat.ToString("0.00") ?? missingData),
            ("tempTolerance", data.TolerancesData?.HeatTolerance.ToString("0.00") ?? missingData),
            ("lightLevel", data.TolerancesData?.IdealLight?.ToString("0.00") ?? missingData),
            ("lightTolerance", data.TolerancesData?.LightTolerance?.ToString("0.00") ?? missingData),
            ("yield", data.ProduceData?.Yield ?? -1),
            ("potency", data.ProduceData is not null ? Loc.GetString(data.ProduceData.Potency) : missingData),
            ("chemicals", data.ProduceData is not null ? _localizationHelper.ChemicalsToLocalizedStrings(data.ProduceData.Chemicals) : missingData),
            ("gasesOut", data.ProduceData is not null ? _localizationHelper.GasesToLocalizedStrings(data.ProduceData.ExudeGasses) : missingData),
            ("endurance", data.PlantData?.Endurance.ToString("0.00") ?? missingData),
            ("lifespan", data.PlantData?.Lifespan.ToString("0.00") ?? missingData),
            ("seeds", data.ProduceData is not null ? (data.ProduceData.Seedless ? "no" : "yes") : "other"),
            ("viable", data.PlantData is not null ? (data.PlantData.Viable ? "yes" : "no") : "other"),
            ("kudzu", data.PlantData is not null ? (data.PlantData.Kudzu ? "yes" : "no") : "other")
        ];

        _paperSystem.SetContent((printed, paperComp), Loc.GetString($"plant-analyzer-printout", [.. parameters]));
        _labelSystem.Label(printed, seedName);
        _audioSystem.PlayPvs(component.SoundPrint, uid,
            AudioParams.Default
            .WithVariation(0.25f)
            .WithVolume(3f)
            .WithRolloffFactor(2.8f)
            .WithMaxDistance(4.5f));

        component.PrintReadyAt = _gameTiming.CurTime + component.PrintCooldown;
    }

    /// <inheritdoc/>
    protected override Enum GetUiKey()
    {
        return CorvaxPlantAnalyzerUiKey.Key;
    }

    /// <inheritdoc/>
    protected override bool ScanTargetPopupMessage(Entity<CorvaxPlantAnalyzerComponent> uid, AfterInteractEvent args, [NotNullWhen(true)] out string? message)
    {
        message = null;
        return false;
    }

    /// <inheritdoc/>
    protected override bool ValidScanTarget(EntityUid? target)
    {
        return HasComp<PlantTrayComponent>(target) || HasComp<PlantComponent>(target)
            || HasComp<SeedComponent>(target) || HasComp<ProduceComponent>(target);
    }
}
