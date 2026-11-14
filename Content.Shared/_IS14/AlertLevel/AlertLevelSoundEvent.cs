using Robust.Shared.Audio;

namespace Content.Shared.AlertLevel;

/// <summary>Allows the server to select the current Corvax announcer's alert recording.</summary>
[ByRefEvent]
public record struct AlertLevelSoundEvent(AlertLevelPrototype Prototype, SoundSpecifier? Sound);
