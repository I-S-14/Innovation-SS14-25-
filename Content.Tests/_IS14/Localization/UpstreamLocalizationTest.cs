using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Linguini.Syntax.Parser;
using NUnit.Framework;

namespace Content.Tests._IS14.Localization;

[TestFixture]
public sealed class UpstreamLocalizationTest
{
    private static readonly Regex Message = new(@"^(-?[A-Za-z][A-Za-z0-9_-]*)\s*=", RegexOptions.Multiline);
    private static readonly Regex Variable = new(@"\$([A-Za-z][A-Za-z0-9_-]*)");

    private static string LocaleRoot()
    {
        var directory = new DirectoryInfo(TestContext.CurrentContext.TestDirectory);
        while (directory != null)
        {
            var path = Path.Combine(directory.FullName, "Resources", "Locale");
            if (Directory.Exists(path))
                return path;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Cannot find the repository Resources/Locale directory.");
    }

    private static IEnumerable<(string Key, string Text, string File)> Messages(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*.ftl", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            var matches = Message.Matches(text);
            for (var i = 0; i < matches.Count; i++)
            {
                var start = matches[i].Index;
                var end = i + 1 < matches.Count ? matches[i + 1].Index : text.Length;
                yield return (matches[i].Groups[1].Value, text[start..end], file);
            }
        }
    }

    [Test]
    public void TranslationsParse()
    {
        var root = Path.Combine(LocaleRoot(), "ru-RU", "_IS14", "Upstream");
        var files = Directory.GetFiles(root, "*.ftl", SearchOption.AllDirectories);
        Assert.That(files, Is.Not.Empty);
        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            var resource = new LinguiniParser(text).Parse();
            Assert.That(resource.Errors, Is.Empty, $"Invalid Fluent in {file}");
        }
    }

    [Test]
    public void TranslationsHaveUniqueKeysAndMatchingVariables()
    {
        var root = LocaleRoot();
        var english = Messages(Path.Combine(root, "en-US"))
            .GroupBy(message => message.Key)
            .ToDictionary(group => group.Key, group => group.Last());
        var russian = Messages(Path.Combine(root, "ru-RU"))
            .GroupBy(message => message.Key)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var imported = Messages(Path.Combine(root, "ru-RU", "_IS14", "Upstream"));

        using (Assert.EnterMultipleScope())
        {
            foreach (var translation in imported)
            {
                Assert.That(russian[translation.Key], Has.Length.EqualTo(1),
                    $"Duplicate key {translation.Key}: {string.Join(", ", russian[translation.Key].Select(x => x.File))}");
                Assert.That(english.TryGetValue(translation.Key, out var source), Is.True,
                    $"No English source for {translation.Key}");
                if (source.Text == null)
                    continue;

                var expected = Variable.Matches(source.Text).Select(match => match.Groups[1].Value).ToHashSet();
                var actual = Variable.Matches(translation.Text).Select(match => match.Groups[1].Value).ToHashSet();
                Assert.That(actual, Is.EquivalentTo(expected), $"Variables differ for {translation.Key}");
            }
        }
    }
}
