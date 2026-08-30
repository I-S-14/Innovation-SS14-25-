// Licensed under IS14's EULA, see EULA.txt for more information.

namespace Content.Shared.UserInterface;

/// <summary>
///     IS14 additions to the upstream component, kept in our own file: the type is already
///     <c>partial</c>, so a new field costs the upstream file nothing at all.
/// </summary>
public sealed partial class ActivatableUIComponent
{
    /// <summary>
    ///     Sort priority for the verbs this component adds. Higher wins alt-click.
    /// </summary>
    /// <remarks>
    ///     Anything else that hangs an alt-verb off the same entity — a MOD suit that has
    ///     been handed a gas tank, for one — would otherwise win alt-click merely by
    ///     sorting first, and the panel is what alt-click is for.
    /// </remarks>
    [DataField, ViewVariables(VVAccess.ReadWrite)]
    public int VerbPriority;
}
