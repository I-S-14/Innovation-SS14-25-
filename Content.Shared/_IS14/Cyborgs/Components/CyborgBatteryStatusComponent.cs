// Licensed under IS14's EULA, see EULA.txt for more information.

using Robust.Shared.GameStates;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>Shows explicit battery percentage and available shots in the selected cyborg tool's status.</summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyborgBatteryStatusComponent : Component;
