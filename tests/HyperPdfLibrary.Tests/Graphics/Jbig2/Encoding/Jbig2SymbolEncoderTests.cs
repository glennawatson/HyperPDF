// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2.Encoding;

/// <summary>Round-trip tests of the symbol dictionary and text region encoder.</summary>
public sealed class Jbig2SymbolEncoderTests
{
    /// <summary>The share of pixels that may differ in a lossy page.</summary>
    private const double LossyDifferenceLimit = 0.02;

    /// <summary>The merge similarity the lossy tests use.</summary>
    private const double LossySimilarity = 0.8;

    /// <summary>A text page the symbol tests code.</summary>
    private static readonly Jbig2Size TextPage = new(320, 220);

    /// <summary>A small page.</summary>
    private static readonly Jbig2Size SmallPage = new(61, 45);

    /// <summary>Text styles from small to large glyphs, with and without stray pixels.</summary>
    private static readonly Jbig2TestTextStyle[] Styles =
    [
        new(1, 1, 25),
        new(2, 7, 25),
        new(3, 42, 25),
        new(4, 1, 0),
        new(3, 9, 0),
    ];

    /// <summary>Glyphs 21 by 15 pixels with some stray pixels.</summary>
    private static readonly Jbig2TestTextStyle NoisyStyle = new(3, 9, 25);

    /// <summary>Glyphs 21 by 15 pixels with no stray pixels.</summary>
    private static readonly Jbig2TestTextStyle CleanStyle = new(3, 9, 0);

    /// <summary>Glyphs 14 by 10 pixels with no stray pixels.</summary>
    private static readonly Jbig2TestTextStyle SmallStyle = new(2, 9, 0);

    /// <summary>A large rectangle, beyond the default symbol size.</summary>
    private static readonly Jbig2RegionBox LargeBlock = new(20, 150, 200, 40);

    /// <summary>The symbol size limit that sends glyphs to the residual region.</summary>
    private static readonly int[] SmallSymbolLimits = [12];

    /// <summary>Three equal rectangles on one row.</summary>
    private static readonly Jbig2RegionBox[] Repeats = [new(3, 5, 4, 6), new(20, 5, 4, 6), new(40, 5, 4, 6)];

    /// <summary>A ring, its hole and the dot in it, as one nested set of components.</summary>
    private static readonly Jbig2RegionBox[] Ring = [new(5, 5, 20, 20), new(8, 8, 14, 14), new(13, 13, 4, 4)];

    /// <summary>The left edge of the first block letter.</summary>
    private static readonly Jbig2Point FirstLetter = new(10, 10);

    /// <summary>The left edge of the second block letter.</summary>
    private static readonly Jbig2Point SecondLetter = new(100, 10);

    /// <summary>The strokes of a block letter C, relative to its top left corner.</summary>
    private static readonly Jbig2RegionBox[] LetterStrokes = [new(0, 0, 4, 20), new(0, 0, 14, 4), new(0, 16, 14, 4)];

    /// <summary>The middle bar that turns a C into an E.</summary>
    private static readonly Jbig2RegionBox LetterBar = new(0, 8, 12, 4);

    /// <summary>The spacing of the pixels flipped to make glyphs differ slightly.</summary>
    private static readonly Jbig2Size PerturbSpacing = new(23, 7);

    /// <summary>Text pages at several styles round-trip exactly in the lossless symbol mode.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SymbolModeRoundTripsText()
    {
        var options = new Jbig2EncodeOptions { Mode = Jbig2EncodeMode.SymbolText };
        foreach (var style in Styles)
        {
            var rows = Jbig2TestPages.Text(TextPage, style);
            var coded = Jbig2Encoder.Encode(rows, TextPage.Width, TextPage.Height, options);
            var decoded = Jbig2TestPages.Decode(coded.Data, coded.Globals, TextPage);

            await Assert.That(coded.Mode).IsEqualTo(Jbig2EncodeMode.SymbolText);
            await Assert.That(coded.IsLossless).IsTrue();
            await Assert.That(decoded).IsNotNull();
            await Assert.That(decoded!.AsSpan().SequenceEqual(rows)).IsTrue().Because($"{style}");
        }
    }

    /// <summary>Every template codes the dictionary, so every template round-trips.</summary>
    /// <param name="template">The generic template for the symbol bitmaps.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    public async Task EachTemplateCodesTheDictionary(int template)
    {
        var rows = Jbig2TestPages.Text(TextPage, NoisyStyle);
        var options = new Jbig2EncodeOptions { Mode = Jbig2EncodeMode.SymbolText, GenericTemplate = template };
        var coded = Jbig2Encoder.Encode(rows, TextPage.Width, TextPage.Height, options);
        var decoded = Jbig2TestPages.Decode(coded.Data, coded.Globals, TextPage);

        await Assert.That(coded.Mode).IsEqualTo(Jbig2EncodeMode.SymbolText);
        await Assert.That(decoded!.AsSpan().SequenceEqual(rows)).IsTrue();
    }

    /// <summary>Repeated glyphs make the symbol coding smaller than the generic region.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SymbolModeBeatsGenericOnRepeatedText()
    {
        var rows = Jbig2TestPages.Text(TextPage, CleanStyle);
        var generic = Jbig2Encoder.Encode(rows, TextPage.Width, TextPage.Height, new() { Mode = Jbig2EncodeMode.GenericRegion });
        var symbols = Jbig2Encoder.Encode(rows, TextPage.Width, TextPage.Height, new() { Mode = Jbig2EncodeMode.SymbolText });

        await Assert.That(symbols.Mode).IsEqualTo(Jbig2EncodeMode.SymbolText);
        await Assert.That(symbols.Data.Length).IsLessThan(generic.Data.Length);
    }

    /// <summary>The smallest mode picks the symbol coding for repeated text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SmallestPicksSymbolsForText()
    {
        var rows = Jbig2TestPages.Text(TextPage, CleanStyle);
        var coded = Jbig2Encoder.Encode(rows, TextPage.Width, TextPage.Height);
        var decoded = Jbig2TestPages.Decode(coded.Data, coded.Globals, TextPage);

        await Assert.That(coded.Mode).IsEqualTo(Jbig2EncodeMode.SymbolText);
        await Assert.That(decoded!.AsSpan().SequenceEqual(rows)).IsTrue();
    }

    /// <summary>A component too large to be a symbol is coded in a generic region and the page still round-trips.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LargeComponentsGoToTheResidualRegion()
    {
        var rows = Jbig2TestPages.Text(TextPage, SmallStyle);
        Jbig2TestPages.Fill(rows, TextPage.Width, LargeBlock);
        var coded = Jbig2Encoder.Encode(rows, TextPage.Width, TextPage.Height, new() { Mode = Jbig2EncodeMode.SymbolText });
        var decoded = Jbig2TestPages.Decode(coded.Data, coded.Globals, TextPage);

        await Assert.That(coded.Mode).IsEqualTo(Jbig2EncodeMode.SymbolText);
        await Assert.That(decoded!.AsSpan().SequenceEqual(rows)).IsTrue();
    }

    /// <summary>A lowered symbol size limit sends glyph-sized components to the residual region too.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SymbolSizeLimitIsHonoured()
    {
        var rows = Jbig2TestPages.Text(TextPage, NoisyStyle);
        foreach (var limit in SmallSymbolLimits)
        {
            var options = new Jbig2EncodeOptions { Mode = Jbig2EncodeMode.SymbolText, MaximumSymbolSize = limit };
            var coded = Jbig2Encoder.Encode(rows, TextPage.Width, TextPage.Height, options);
            var decoded = Jbig2TestPages.Decode(coded.Data, coded.Globals, TextPage);

            await Assert.That(decoded!.AsSpan().SequenceEqual(rows)).IsTrue();
        }
    }

    /// <summary>The dictionary can go in a separate globals stream, and the page decodes with it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeparateGlobalsRoundTrip()
    {
        var rows = Jbig2TestPages.Text(TextPage, NoisyStyle);
        var coded = Jbig2Encoder.Encode(rows, TextPage.Width, TextPage.Height, new() { Mode = Jbig2EncodeMode.SymbolText, SeparateGlobals = true });
        var decoded = Jbig2TestPages.Decode(coded.Data, coded.Globals, TextPage);
        var withoutGlobals = Jbig2TestPages.Decode(coded.Data, null, TextPage);

        await Assert.That(coded.Globals).IsNotNull();
        await Assert.That(decoded!.AsSpan().SequenceEqual(rows)).IsTrue();
        await Assert.That(withoutGlobals is null || !withoutGlobals.AsSpan().SequenceEqual(rows)).IsTrue();
    }

    /// <summary>A page whose only symbol repeats still round-trips (the dictionary gets a spare symbol).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SingleSymbolRoundTrips()
    {
        var rows = Jbig2TestPages.White(SmallPage.Width, SmallPage.Height);
        foreach (var repeat in Repeats)
        {
            Jbig2TestPages.Fill(rows, SmallPage.Width, repeat);
        }

        var coded = Jbig2Encoder.Encode(rows, SmallPage.Width, SmallPage.Height, new() { Mode = Jbig2EncodeMode.SymbolText });
        var decoded = Jbig2TestPages.Decode(coded.Data, coded.Globals, SmallPage);

        await Assert.That(coded.Mode).IsEqualTo(Jbig2EncodeMode.SymbolText);
        await Assert.That(decoded!.AsSpan().SequenceEqual(rows)).IsTrue();
    }

    /// <summary>Components whose bounding boxes overlap, such as a ring around a dot, round-trip.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NestedComponentsRoundTrip()
    {
        var rows = Jbig2TestPages.White(SmallPage.Width, SmallPage.Height);
        Jbig2TestPages.Fill(rows, SmallPage.Width, Ring[0]);
        Jbig2TestPages.Clear(rows, SmallPage.Width, Ring[1]);
        Jbig2TestPages.Fill(rows, SmallPage.Width, Ring[2]);
        var coded = Jbig2Encoder.Encode(rows, SmallPage.Width, SmallPage.Height, new() { Mode = Jbig2EncodeMode.SymbolText });
        var decoded = Jbig2TestPages.Decode(coded.Data, coded.Globals, SmallPage);

        await Assert.That(decoded!.AsSpan().SequenceEqual(rows)).IsTrue();
    }

    /// <summary>A blank page and a mostly black page are not coded as text; they fall back to a generic region.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsuitablePagesFallBackToGeneric()
    {
        var blank = Jbig2TestPages.White(SmallPage.Width, SmallPage.Height);
        var black = Jbig2TestPages.White(SmallPage.Width, SmallPage.Height);
        Jbig2TestPages.Fill(black, SmallPage.Width, new(0, 0, SmallPage.Width, SmallPage.Height));
        var options = new Jbig2EncodeOptions { Mode = Jbig2EncodeMode.SymbolText };

        var codedBlank = Jbig2Encoder.Encode(blank, SmallPage.Width, SmallPage.Height, options);
        var codedBlack = Jbig2Encoder.Encode(black, SmallPage.Width, SmallPage.Height, options);

        await Assert.That(codedBlank.Mode).IsEqualTo(Jbig2EncodeMode.GenericRegion);
        await Assert.That(codedBlack.Mode).IsEqualTo(Jbig2EncodeMode.GenericRegion);
        await Assert.That(Jbig2TestPages.Decode(codedBlank.Data, null, SmallPage)!.AsSpan().SequenceEqual(blank)).IsTrue();
        await Assert.That(Jbig2TestPages.Decode(codedBlack.Data, null, SmallPage)!.AsSpan().SequenceEqual(black)).IsTrue();
    }

    /// <summary>Lossy merging reports that it is lossy and changes only a small share of the pixels of noisy text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LossyMergingStaysCloseToTheInput()
    {
        var rows = Jbig2TestPages.Text(TextPage, CleanStyle);
        for (var y = 0; y < TextPage.Height; y += PerturbSpacing.Height)
        {
            for (var x = 1; x < TextPage.Width; x += PerturbSpacing.Width)
            {
                Jbig2TestPages.Flip(rows, TextPage.Width, x, y);
            }
        }

        var options = new Jbig2EncodeOptions { Mode = Jbig2EncodeMode.LossySymbolMerging, MergeSimilarity = LossySimilarity };
        var lossless = Jbig2Encoder.Encode(rows, TextPage.Width, TextPage.Height, new() { Mode = Jbig2EncodeMode.SymbolText });
        var coded = Jbig2Encoder.Encode(rows, TextPage.Width, TextPage.Height, options);
        var decoded = Jbig2TestPages.Decode(coded.Data, coded.Globals, TextPage);
        var differences = Jbig2TestPages.CountDifferences(rows, decoded!, TextPage);

        await Assert.That(coded.Mode).IsEqualTo(Jbig2EncodeMode.LossySymbolMerging);
        await Assert.That(coded.IsLossless).IsFalse();
        await Assert.That(coded.Data.Length).IsLessThan(lossless.Data.Length);
        await Assert.That(differences).IsGreaterThan(0);
        await Assert.That(differences).IsLessThan((int)(TextPage.Width * TextPage.Height * LossyDifferenceLimit));
    }

    /// <summary>Refined coding reproduces noisy text exactly and is smaller than coding every distinct mark as a new symbol.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefinedModeIsLosslessAndSmaller()
    {
        var rows = Jbig2TestPages.Text(TextPage, CleanStyle);
        FlipSparsePixels(rows);
        var plain = Jbig2Encoder.Encode(rows, TextPage.Width, TextPage.Height, new() { Mode = Jbig2EncodeMode.SymbolText });
        var refined = Jbig2Encoder.Encode(rows, TextPage.Width, TextPage.Height, new() { Mode = Jbig2EncodeMode.SymbolTextRefined });
        var decoded = Jbig2TestPages.Decode(refined.Data, refined.Globals, TextPage);

        await Assert.That(refined.Mode).IsEqualTo(Jbig2EncodeMode.SymbolTextRefined);
        await Assert.That(refined.IsLossless).IsTrue();
        await Assert.That(decoded!.AsSpan().SequenceEqual(rows)).IsTrue();
        await Assert.That(refined.Data.Length).IsLessThan(plain.Data.Length);
    }

    /// <summary>Refined coding round-trips for every dictionary template and with the dictionaries in the globals stream.</summary>
    /// <param name="template">The generic template.</param>
    /// <param name="separateGlobals">Whether the dictionaries go in the globals stream.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0, false)]
    [Arguments(1, false)]
    [Arguments(2, true)]
    [Arguments(3, true)]
    public async Task RefinedModeRoundTrips(int template, bool separateGlobals)
    {
        var rows = Jbig2TestPages.Text(TextPage, NoisyStyle);
        FlipSparsePixels(rows);
        var options = new Jbig2EncodeOptions { Mode = Jbig2EncodeMode.SymbolTextRefined, GenericTemplate = template, SeparateGlobals = separateGlobals };
        var coded = Jbig2Encoder.Encode(rows, TextPage.Width, TextPage.Height, options);
        var decoded = Jbig2TestPages.Decode(coded.Data, coded.Globals, TextPage);

        await Assert.That(coded.Mode).IsEqualTo(Jbig2EncodeMode.SymbolTextRefined);
        await Assert.That(decoded!.AsSpan().SequenceEqual(rows)).IsTrue();
    }

    /// <summary>With similarity 1 the lossy mode only shares identical shapes, so it reproduces the page exactly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LossyModeWithFullSimilarityIsExact()
    {
        var rows = Jbig2TestPages.Text(TextPage, NoisyStyle);
        var coded = Jbig2Encoder.Encode(rows, TextPage.Width, TextPage.Height, new() { Mode = Jbig2EncodeMode.LossySymbolMerging, MergeSimilarity = 1.0 });
        var decoded = Jbig2TestPages.Decode(coded.Data, coded.Globals, TextPage);

        await Assert.That(decoded!.AsSpan().SequenceEqual(rows)).IsTrue();
    }

    /// <summary>Glyphs that differ by a whole stroke are never merged, even at a loose similarity.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LossyModeKeepsDifferentGlyphsApart()
    {
        // The E's middle bar is far from any pixel of the C, so the two must stay separate symbols.
        var rows = Jbig2TestPages.White(TextPage.Width, TextPage.Height);
        DrawLetter(rows, FirstLetter, true);
        DrawLetter(rows, SecondLetter, false);
        var options = new Jbig2EncodeOptions { Mode = Jbig2EncodeMode.LossySymbolMerging, MergeSimilarity = LossySimilarity };
        var coded = Jbig2Encoder.Encode(rows, TextPage.Width, TextPage.Height, options);
        var decoded = Jbig2TestPages.Decode(coded.Data, coded.Globals, TextPage);

        await Assert.That(decoded!.AsSpan().SequenceEqual(rows)).IsTrue();
    }

    /// <summary>Flips pixels on a grid so repeated glyphs are no longer identical.</summary>
    /// <param name="rows">The rows.</param>
    private static void FlipSparsePixels(byte[] rows)
    {
        for (var y = 0; y < TextPage.Height; y += PerturbSpacing.Height)
        {
            for (var x = 1; x < TextPage.Width; x += PerturbSpacing.Width)
            {
                Jbig2TestPages.Flip(rows, TextPage.Width, x, y);
            }
        }
    }

    /// <summary>Draws a block letter, either an E or a C.</summary>
    /// <param name="rows">The rows.</param>
    /// <param name="corner">The letter's top left corner.</param>
    /// <param name="withBar">Whether to draw the E's middle bar.</param>
    private static void DrawLetter(byte[] rows, Jbig2Point corner, bool withBar)
    {
        foreach (var stroke in LetterStrokes)
        {
            Jbig2TestPages.Fill(rows, TextPage.Width, Offset(stroke, corner));
        }

        if (withBar)
        {
            Jbig2TestPages.Fill(rows, TextPage.Width, Offset(LetterBar, corner));
        }
    }

    /// <summary>Moves a rectangle by a corner.</summary>
    /// <param name="rectangle">The rectangle.</param>
    /// <param name="corner">The offset.</param>
    /// <returns>The moved rectangle.</returns>
    private static Jbig2RegionBox Offset(Jbig2RegionBox rectangle, Jbig2Point corner) => rectangle with { X = rectangle.X + corner.X, Y = rectangle.Y + corner.Y };
}
