// Licensed under IS14's EULA, see EULA.txt for more information.

using Content.Shared.Wires;

namespace Content.Server.Wires;

/// <summary>
///     IS14 additions to the wire panel, kept out of the upstream file: making the class
///     <c>partial</c> costs one word there, and everything we add lives here instead.
/// </summary>
public sealed partial class WiresSystem
{
    /// <summary>
    ///     Sets a status light that belongs to no wire — a readout of the machine's own
    ///     state rather than of a cut line. Passing null takes the light off the panel.
    /// </summary>
    /// <remarks>
    ///     Lights normally come from wire actions, one per action, which is fine right up
    ///     until a machine has a state worth showing that no single wire owns. The
    ///     position sorts it among the others; -1 puts it first and matches no wire, so
    ///     nothing labels it with a wire letter.
    /// </remarks>
    public void SetStatus(Entity<WiresComponent?> ent, object key, StatusLightData? data, int position = -1)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (data == null)
        {
            if (!ent.Comp.Statuses.Remove(key))
                return;
        }
        else
        {
            ent.Comp.Statuses[key] = (position, data);
        }

        UpdateUserInterface(ent);
    }
}
