// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.DoAfter;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// A built-in tank the cyborg empties onto the floor with an alternative interaction, like tg's cleaner box.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CyborgTankDumpComponent : Component
{
    [DataField(required: true)]
    public string Solution = string.Empty;

    [DataField]
    public TimeSpan Delay = TimeSpan.FromSeconds(3);
}

[Serializable, NetSerializable]
public sealed partial class CyborgTankDumpDoAfterEvent : SimpleDoAfterEvent;
