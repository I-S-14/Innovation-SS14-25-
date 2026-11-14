using System.Text.RegularExpressions;
using Content.Shared.Speech.Components;

using Robust.Shared.Random; // CorvaxGoob-Localization

namespace Content.Shared.Speech.EntitySystems;

public sealed partial class FrontalLispSystem : RelayAccentSystem<FrontalLispComponent>
{
    //IS14-change start: cache retained Russian accent patterns.
    private static readonly Regex RegexLowerCyrillicT = new(@"\B[т](?![АЕЁИОУЫЭЮЯаеёиоуыэюя])");
    private static readonly Regex RegexUpperCyrillicT = new(@"\B[Т](?![АЕЁИОУЫЭЮЯаеёиоуыэюя])");
    //IS14-change end

    // @formatter:off
    private static readonly Regex RegexUpperTh = new("[T]+[Ss]+|[S]+[Cc]+(?=[IiEeYy]+)|[C]+(?=[IiEeYy]+)|[P][Ss]+|([S]+[Tt]+|[T]+)(?=[Ii]+[Oo]+[Uu]*[Nn]*)|[C]+[Hh]+(?=[Ii]*[Ee]*)|[Z]+|[S]+|[X]+(?=[Ee]+)");
    private static readonly Regex RegexLowerTh = new("[t]+[s]+|[s]+[c]+(?=[iey]+)|[c]+(?=[iey]+)|[p][s]+|([s]+[t]+|[t]+)(?=[i]+[o]+[u]*[n]*)|[c]+[h]+(?=[i]*[e]*)|[z]+|[s]+|[x]+(?=[e]+)");
    private static readonly Regex RegexUpperEcks = new("[E]+[Xx]+[Cc]*|[X]+");
    private static readonly Regex RegexLowerEcks = new("[e]+[x]+[c]*|[x]+");
    // @formatter:on

    [Dependency] private IRobustRandom _random = default!; // CorvaxGoob-Localization

    public override string Accentuate(string message, Entity<FrontalLispComponent>? ent = null)
    {
        // handles ts, sc(i|e|y), c(i|e|y), ps, st(io(u|n)), ch(i|e), z, s
        message = RegexUpperTh.Replace(message, "TH");
        message = RegexLowerTh.Replace(message, "th");
        // handles ex(c), x
        message = RegexUpperEcks.Replace(message, "EKTH");
        message = RegexLowerEcks.Replace(message, "ekth");
        // Corvax-Localization Start
        // с - ш
        message = message.Replace(@"с", _random.Prob(0.90f) ? "ш" : "с");
        message = message.Replace(@"С", _random.Prob(0.90f) ? "Ш" : "С");
        // ч - ш
        message = message.Replace(@"ч", _random.Prob(0.90f) ? "ш" : "ч");
        message = message.Replace(@"Ч", _random.Prob(0.90f) ? "Ш" : "Ч");
        // ц - ч
        message = message.Replace(@"ц", _random.Prob(0.90f) ? "ч" : "ц");
        message = message.Replace(@"Ц", _random.Prob(0.90f) ? "Ч" : "Ц");
        // т - ч
        message = RegexLowerCyrillicT.Replace(message, _random.Prob(0.90f) ? "ч" : "т");
        message = RegexUpperCyrillicT.Replace(message, _random.Prob(0.90f) ? "Ч" : "Т");
        // з - ж
        message = message.Replace(@"з", _random.Prob(0.90f) ? "ж" : "з");
        message = message.Replace(@"З", _random.Prob(0.90f) ? "Ж" : "З");
        // Corvax-Localization End

        return message;
    }
}
