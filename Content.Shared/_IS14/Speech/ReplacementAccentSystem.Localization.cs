// SPDX-License-Identifier: MIT

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Content.Shared.Speech.EntitySystems;

public sealed partial class ReplacementAccentSystem
{
    private (Regex regex, string replacement)[] GenerateLocalizedReplacements(Dictionary<string, string> replacements)
    {
        var localized = new List<(Regex regex, string replacement)>();
        foreach (var (first, replace) in replacements)
        {
            // Retained Goob dictionaries include language-specific rules. A pair
            // absent from the current culture cannot match natural speech.
            if (!Loc.TryGetString(first, out var firstLoc) || !Loc.TryGetString(replace, out var replaceLoc))
                continue;
            localized.Add((new Regex($@"(?<![\w']){firstLoc}(?![\w'])", RegexOptions.IgnoreCase), replaceLoc));
        }
        return localized.ToArray();
    }
}
