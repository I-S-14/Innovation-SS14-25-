using System.Text.RegularExpressions;
using Content.Shared.Speech.Components;

using Robust.Shared.Random; // CorvaxGoob-Localization

namespace Content.Shared.Speech.EntitySystems;

public sealed partial class MothAccentSystem : RelayAccentSystem<MothAccentComponent>
{
    //IS14-change start: cache retained Russian accent patterns.
    private static readonly Regex RegexLowerCyrillicZh = new("ж+");
    private static readonly Regex RegexUpperCyrillicZh = new("Ж+");
    private static readonly Regex RegexLowerCyrillicZ = new("з+");
    private static readonly Regex RegexUpperCyrillicZ = new("З+");
    //IS14-change end

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
        message = RegexLowerCyrillicZh.Replace(
            message,
            _random.Pick(new List<string>() { "жж", "жжж" })
        );
        // Ж => ЖЖЖ
        message = RegexUpperCyrillicZh.Replace(
            message,
            _random.Pick(new List<string>() { "ЖЖ", "ЖЖЖ" })
        );
        // з => ссс
        message = RegexLowerCyrillicZ.Replace(
            message,
            _random.Pick(new List<string>() { "зз", "ззз" })
        );
        // З => CCC
        message = RegexUpperCyrillicZ.Replace(
            message,
            _random.Pick(new List<string>() { "ЗЗ", "ЗЗЗ" })
        );
        // CorvaxGoob-Localization-End

        return message;
    }
}
