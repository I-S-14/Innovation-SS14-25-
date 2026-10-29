// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Inventory;

namespace Content.Shared.Temperature;

/// <summary>
/// This event is raised before heat is exchanged so that the conductance of the exchange can be changed.
/// </summary>
[ByRefEvent]
public record struct BeforeHeatExchangeEvent() : IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = ~SlotFlags.POCKET;

    public float TemperatureDelta;
    public readonly EntityUid Target; // Goobstation

    public ModifyChangedTemperatureEvent(float temperature, EntityUid target) // Goobstation
    {
        TemperatureDelta = temperature;
        Target = target; // Goobstation
    }
}

/// <summary>
/// This event is raised after heat is exchanged to inform other systems that temperature has changed.
/// </summary>
/// <param name="CurrentTemperature">Current temperature of this entity.</param>
/// <param name="LastTemperature">Previous temperature of this entity.</param>
[ByRefEvent]
public record struct TemperatureChangedEvent(float CurrentTemperature, float LastTemperature)
{
    public readonly float CurrentTemperature = CurrentTemperature;
    public readonly float LastTemperature = LastTemperature;
    public readonly float TemperatureDelta = CurrentTemperature - LastTemperature;
}

// Goobstation
public sealed class TemperatureChangeAttemptEvent : CancellableEntityEventArgs
{
    public readonly float CurrentTemperature;
    public readonly float LastTemperature;
    public readonly float TemperatureDelta;

    public TemperatureChangeAttemptEvent(float current, float last, float delta)
    {
        CurrentTemperature = current;
        LastTemperature = last;
        TemperatureDelta = delta;
    }
}
