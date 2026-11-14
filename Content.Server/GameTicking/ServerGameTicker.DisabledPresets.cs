using Content.Goobstation.Common.CCVar;
using Content.Shared.GameTicking.Prototypes;

namespace Content.Server.GameTicking;

public sealed partial class ServerGameTicker
{
    public bool IsPresetDisabled(GamePresetPrototype preset)
    {
        foreach (var entry in Cfg.GetCVar(GoobCVars.DisabledGamePresets).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (FindGamePreset(entry)?.ID == preset.ID)
                return true;
        }

        return false;
    }
}
