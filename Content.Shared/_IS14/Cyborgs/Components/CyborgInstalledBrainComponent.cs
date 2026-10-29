// Licensed under IS14's EULA, see EULA.txt for more information.

namespace Content.Shared._IS14.Cyborgs.Components;

/// <summary>
/// Added to a brain while it sits inside a cyborg, so a mind that joins the brain later
/// (a positronic brain getting a ghost) is moved into the chassis.
/// </summary>
/// <remarks>
/// The upstream borg system already subscribes to <c>MindAddedMessage</c> on the borg brain component,
/// and the engine allows only one subscription per component and event pair.
/// </remarks>
[RegisterComponent]
public sealed partial class CyborgInstalledBrainComponent : Component
{
    [ViewVariables]
    public EntityUid Cyborg;
}
