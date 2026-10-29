// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Administration.Logs;
using Content.Server.Atmos.EntitySystems;
using Content.Server.Atmos.Piping.Unary.EntitySystems;
using Content.Server.Medical.Components;
using Content.Server.NodeContainer.EntitySystems;
using Content.Server.NodeContainer.NodeGroups;
using Content.Server.NodeContainer.Nodes;
using Content.Shared.Atmos;
using Content.Shared.Actions; // Shitmed Change
using Content.Shared.UserInterface;
using Content.Shared.Body.Components;
using Content.Shared.Chemistry.EntitySystems;
using Content.Shared.Atmos.Components;
using Content.Shared.Damage.Systems;
using Content.Shared.Medical.Cryogenics;
using Content.Shared.MedicalScanner;
using Content.Shared.Temperature.Components;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Content.Shared.Bed.Sleep; // Shitmed Change
using Robust.Server.GameObjects;
using Robust.Shared.Containers;

using Content.Shared.Medical.HealthAnalyzer;

namespace Content.Server.Medical;

public sealed partial class CryoPodSystem : SharedCryoPodSystem
{
    [Dependency] private readonly AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private readonly GasCanisterSystem _gasCanisterSystem = default!;
    [Dependency] private readonly NodeContainerSystem _nodeContainer = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly SharedSolutionContainerSystem _solutionContainerSystem = default!;
    [Dependency] private readonly SleepingSystem _sleepingSystem = default!; // Shitmed change
    [Dependency] private AtmosphereSystem _atmosphereSystem = default!;
    [Dependency] private GasCanisterSystem _gasCanisterSystem = default!;
    [Dependency] private GasAnalyzerSystem _gasAnalyzerSystem = default!;
    [Dependency] private HealthAnalyzerSystem _healthAnalyzerSystem = default!;
    [Dependency] private NodeContainerSystem _nodeContainer = default!;
    [Dependency] private DamageableSystem _damageable = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CryoPodComponent, AfterActivatableUIOpenEvent>(OnActivateUI);
        SubscribeLocalEvent<CryoPodComponent, AtmosDeviceUpdateEvent>(OnCryoPodUpdateAtmosphere);
        SubscribeLocalEvent<CryoPodComponent, GasAnalyzerScanEvent>(OnGasAnalyzed);
        SubscribeLocalEvent<CryoPodComponent, EntRemovedFromContainerMessage>(OnEjected);
    }

    private void OnActivateUI(Entity<CryoPodComponent> entity, ref AfterActivatableUIOpenEvent args)
    {
        if (!entity.Comp.BodyContainer.ContainedEntity.HasValue)
            return;

        var patient = entity.Comp.BodyContainer.ContainedEntity;
        var gasMix = _gasAnalyzerSystem.GenerateGasMixEntry("Cryo pod", air.Air);
        var (beakerCapacity, beaker) = GetBeakerInfo(entity);
        var injecting = GetInjectingReagents(entity);
        var health = _healthAnalyzerSystem.GetHealthAnalyzerUiState(patient, true);
        health.ScanMode = true;
        var hasDamage = patient is null ? false : _damageable.GetTotalDamage(patient.Value) > 0;

        if (TryComp<HealthAnalyzerComponent>(entity, out var healthAnalyzer))
        {
            healthAnalyzer.ScannedEntity = entity.Comp.BodyContainer.ContainedEntity;
        }

        // TODO: This should be a state my dude
        _uiSystem.ServerSendUiMessage(
            entity.Owner,
            HealthAnalyzerUiKey.Key,
            new HealthAnalyzerScannedUserMessage(GetNetEntity(entity.Comp.BodyContainer.ContainedEntity),
            temp?.CurrentTemperature ?? 0,
            (bloodstream != null && _solutionContainerSystem.ResolveSolution(entity.Comp.BodyContainer.ContainedEntity.Value,
                bloodstream.BloodSolutionName, ref bloodstream.BloodSolution, out var bloodSolution))
                ? bloodSolution.FillFraction
                : 0,
            null,
            null,
            null,
            null // Shitmed Change
        ));
    }

    private void OnCryoPodUpdateAtmosphere(Entity<CryoPodComponent> entity, ref AtmosDeviceUpdateEvent args)
    {
        if (!_nodeContainer.TryGetNode(entity.Owner, entity.Comp.PortName, out PortablePipeNode? portNode))
            return;

        if (!TryComp(entity, out CryoPodAirComponent? cryoPodAir))
            return;

        _atmosphereSystem.React(cryoPodAir.Air, portNode);

        if (portNode.NodeGroup is PipeNet { NodeCount: > 1 } net)
        {
            _gasCanisterSystem.MixContainerWithPipeNet(cryoPodAir.Air, net.Air);
        }
    }

    private void OnGasAnalyzed(Entity<CryoPodComponent> entity, ref GasAnalyzerScanEvent args)
    {
        if (!TryComp(entity, out CryoPodAirComponent? cryoPodAir))
            return;

        args.GasMixtures ??= new List<(string, GasMixture?)>();
        args.GasMixtures.Add((Name(entity.Owner), cryoPodAir.Air));
        // If it's connected to a port, include the port side
        // multiply by volume fraction to make sure to send only the gas inside the analyzed pipe element, not the whole pipe system
        if (_nodeContainer.TryGetNode(entity.Owner, entity.Comp.PortName, out PipeNode? port) && port.Air.Volume != 0f)
        {
            var portAirLocal = port.Air.Clone();
            portAirLocal.Multiply(port.Volume / port.Air.Volume);
            portAirLocal.Volume = port.Volume;
            args.GasMixtures.Add((entity.Comp.PortName, portAirLocal));
        }
    }

    private void OnEjected(Entity<CryoPodComponent> cryoPod, ref EntRemovedFromContainerMessage args)
    {
        if (TryComp<HealthAnalyzerComponent>(cryoPod.Owner, out var healthAnalyzer))
        {
            healthAnalyzer.ScannedEntity = null;
        }

        // if body is ejected - no need to display health-analyzer
        _uiSystem.CloseUi(cryoPod.Owner, HealthAnalyzerUiKey.Key);
    }
}
