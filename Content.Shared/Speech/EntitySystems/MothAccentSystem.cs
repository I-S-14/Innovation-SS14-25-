using System.Text.RegularExpressions;
using Content.Shared.Speech.Components;

using Robust.Shared.Random; // CorvaxGoob-Localization

namespace Content.Shared.Speech.EntitySystems;

public sealed partial class MothAccentSystem : RelayAccentSystem<MothAccentComponent>
{
    private static readonly Regex RegexLowerBuzz = new("z{1,3}");
    private static readonly Regex RegexUpperBuzz = new("Z{1,3}");

    [Dependency] private IRobustRandom _random = default!; // CorvaxGoob-Localization

    public override string Accentuate(string message, Entity<MothAccentComponent>? ent = null)
    {
        // buzzz
        message = RegexLowerBuzz.Replace(message, "zzz");
        // buZZZ
        message = RegexUpperBuzz.Replace(message, "ZZZ");

        // CorvaxGoob-Localization-Start
        // ж => жжж
        message = Regex.Replace(
            message,
            "ж+",
            _random.Pick(new List<string>() { "жж", "жжж" })
        );
        // Ж => ЖЖЖ
        message = Regex.Replace(
            message,
            "Ж+",
            _random.Pick(new List<string>() { "ЖЖ", "ЖЖЖ" })
        );
        // з => ссс
        message = Regex.Replace(
            message,
            "з+",
            _random.Pick(new List<string>() { "зз", "ззз" })
        );
        // З => CCC
        message = Regex.Replace(
            message,
            "З+",
            _random.Pick(new List<string>() { "ЗЗ", "ЗЗЗ" })
        );
        // CorvaxGoob-Localization-End

        return message;
    }
}
