using System.Text.RegularExpressions;
using Robust.Shared.Random; // CorvaxGoob-Localization
using Content.Shared.Speech.Components;

namespace Content.Shared.Speech.EntitySystems;

public sealed partial class LizardAccentSystem : RelayAccentSystem<LizardAccentComponent>
{
    //IS14-change start: cache retained Russian accent patterns.
    private static readonly Regex RegexLowerCyrillicS = new("с+");
    private static readonly Regex RegexUpperCyrillicS = new("С+");
    private static readonly Regex RegexLowerCyrillicZ = new("з+");
    private static readonly Regex RegexUpperCyrillicZ = new("З+");
    private static readonly Regex RegexLowerCyrillicSh = new("ш+");
    private static readonly Regex RegexUpperCyrillicSh = new("Ш+");
    private static readonly Regex RegexLowerCyrillicCh = new("ч+");
    private static readonly Regex RegexUpperCyrillicCh = new("Ч+");
    //IS14-change end

    private static readonly Regex RegexLowerS = new("s+");
    private static readonly Regex RegexUpperS = new("S+");
    private static readonly Regex RegexInternalX = new(@"(\w)x");
    private static readonly Regex RegexLowerEndX = new(@"\bx([\-|r|R]|\b)");
    private static readonly Regex RegexUpperEndX = new(@"\bX([\-|r|R]|\b)");

    [Dependency] private IRobustRandom _random = default!; // CorvaxGoob-Localization

    public override string Accentuate(string message, Entity<LizardAccentComponent>? ent = null)
    {
        // hissss
        message = RegexLowerS.Replace(message, "sss");
        // hiSSS
        message = RegexUpperS.Replace(message, "SSS");
        // ekssit
        message = RegexInternalX.Replace(message, "$1kss");
        // ecks
        message = RegexLowerEndX.Replace(message, "ecks$1");
        // eckS
        message = RegexUpperEndX.Replace(message, "ECKS$1");

        // CorvaxGoob-Localization-Start
        // c => ссс
        message = RegexLowerCyrillicS.Replace(
            message,
            _random.Pick(new List<string>() { "сс", "ссс" })
        );
        // С => CCC
        message = RegexUpperCyrillicS.Replace(
            message,
            _random.Pick(new List<string>() { "СС", "ССС" })
        );
        // з => ссс
        message = RegexLowerCyrillicZ.Replace(
            message,
            _random.Pick(new List<string>() { "сс", "ссс" })
        );
        // З => CCC
        message = RegexUpperCyrillicZ.Replace(
            message,
            _random.Pick(new List<string>() { "СС", "ССС" })
        );
        // ш => шшш
        message = RegexLowerCyrillicSh.Replace(
            message,
            _random.Pick(new List<string>() { "шш", "шшш" })
        );
        // Ш => ШШШ
        message = RegexUpperCyrillicSh.Replace(
            message,
            _random.Pick(new List<string>() { "ШШ", "ШШШ" })
        );
        // ч => щщщ
        message = RegexLowerCyrillicCh.Replace(
            message,
            _random.Pick(new List<string>() { "щщ", "щщщ" })
        );
        // Ч => ЩЩЩ
        message = RegexUpperCyrillicCh.Replace(
            message,
            _random.Pick(new List<string>() { "ЩЩ", "ЩЩЩ" })
        );
        // CorvaxGoob-Localization-End

        return message;
    }
}
