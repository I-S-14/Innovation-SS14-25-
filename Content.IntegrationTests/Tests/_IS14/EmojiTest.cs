// Licensed under IS14's EULA, see EULA.txt for more information.

#nullable enable

using System.Collections.Generic;
using System.Linq;
using Content.Client._IS14.Chat;
using Content.IntegrationTests.Pair;
using Content.Shared._IS14.Chat;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.ContentPack;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Content.IntegrationTests.Tests._IS14;

/// <summary>
///     Emoji: typed shortcodes have to become sprites, typed markup has to stay text, and the
///     sprites have to be big enough to recognise.
/// </summary>
[TestFixture]
public sealed class EmojiTest
{
    [Test]
    public async Task ShortcodesBecomeEmoji()
    {
        // The client only has its entity systems once it is in a game, which is the only
        // state the messenger ever runs in anyway.
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = true,
        });
        var client = pair.Client;

        var plain = string.Empty;
        var markup = string.Empty;
        var literal = string.Empty;
        var controls = 0;
        var unclaimed = new List<string>();

        await client.WaitPost(() =>
        {
            var emoji = client.System<IS14EmojiSystem>();
            var proto = client.ResolveDependency<IPrototypeManager>();

            var message = emoji.Format("Привет :) как дела <3");
            plain = message.ToString();
            markup = message.ToMarkup();

            // Rendering it is the real proof: a control per emoji means the tag resolved the
            // prototype and actually loaded the sprite.
            var label = new RichTextLabel();
            label.SetMessage(message, IS14EmojiText.Tags);
            controls = label.Controls.Count();

            // Every code must reach its own emoji and leave no text behind. This is what
            // catches a new code being swallowed by a longer one somebody added earlier.
            foreach (var entry in proto.EnumeratePrototypes<IS14EmojiPrototype>())
            {
                foreach (var code in entry.Codes)
                {
                    var one = emoji.Format(code);

                    if (one.ToString().Length != 0 || !one.ToMarkup().Contains(entry.ID))
                        unclaimed.Add($"{entry.ID}: '{code}' -> '{one.ToMarkup()}'");
                }
            }

            // Typed markup is not markup. This is the whole safety story.
            literal = emoji.Format("[color=red]x[/color]").ToString();
        });

        Assert.Multiple(() =>
        {
            Assert.That(plain, Is.EqualTo("Привет  как дела "), "emoji left text behind");
            Assert.That(markup, Does.Contain("EmojiSmile"));
            Assert.That(markup, Does.Contain("EmojiHeart"));
            Assert.That(controls, Is.EqualTo(2), "one drawn sprite per emoji");
            Assert.That(unclaimed, Is.Empty, "shortcodes that did not resolve to their own emoji");
            Assert.That(literal, Is.EqualTo("[color=red]x[/color]"), "typed markup was parsed");
        });

        await pair.CleanReturnAsync();
    }

    /// <summary>
    ///     An emoji whose art is a speck inside its frame renders as a dot however large the
    ///     control is — which is exactly how the first set of these shipped. Sprites made to be
    ///     worn or placed in the world are usually like that; icons are not.
    /// </summary>
    [Test]
    public async Task EmojiSpritesAreLegible()
    {
        await using var pair = await PoolManager.GetServerClient();
        var client = pair.Client;

        var proto = client.ResolveDependency<IPrototypeManager>();
        var resources = client.ResolveDependency<IResourceManager>();

        var tiny = new List<string>();

        foreach (var emoji in proto.EnumeratePrototypes<IS14EmojiPrototype>())
        {
            Assert.That(emoji.Size, Is.GreaterThanOrEqualTo(16), $"{emoji.ID} is drawn too small");

            // RSI frames live in an atlas laid out by direction, which is more than this test
            // needs to know; plain textures are the common case and the one that bit us.
            if (emoji.Sprite is not SpriteSpecifier.Texture texture)
                continue;

            await using var stream = resources.ContentFileRead(texture.TexturePath);
            using var image = Image.Load<Rgba32>(stream);

            var fill = Coverage(image);
            if (fill < 0.15f)
                tiny.Add($"{emoji.ID} ({texture.TexturePath}) fills {fill:P0} of its frame");
        }

        Assert.That(tiny, Is.Empty, "emoji art too small inside its frame — it will look like a dot");

        await pair.CleanReturnAsync();
    }

    /// <summary>Share of the frame the drawing's bounding box takes up.</summary>
    private static float Coverage(Image<Rgba32> image)
    {
        int left = image.Width, top = image.Height, right = -1, bottom = -1;

        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (image[x, y].A <= 32)
                    continue;

                left = Math.Min(left, x);
                top = Math.Min(top, y);
                right = Math.Max(right, x);
                bottom = Math.Max(bottom, y);
            }
        }

        if (right < 0)
            return 0f;

        return (right - left + 1) * (bottom - top + 1) / (float) (image.Width * image.Height);
    }
}
