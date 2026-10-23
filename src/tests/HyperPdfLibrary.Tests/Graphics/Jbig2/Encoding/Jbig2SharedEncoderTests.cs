// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2.Encoding;

/// <summary>Tests of coding several images with one shared symbol dictionary in the globals stream.</summary>
public sealed class Jbig2SharedEncoderTests
{
    /// <summary>The merge similarity of the lossy test.</summary>
    private const double LossySimilarity = 0.8;

    /// <summary>The share of a page, as a divisor, that a lossy image may change.</summary>
    private const int LossyShare = 20;

    /// <summary>The page size of the shared images.</summary>
    private static readonly Jbig2Size Page = new(240, 160);

    /// <summary>The text styles of the images: the same glyphs, different layouts.</summary>
    private static readonly Jbig2TestTextStyle[] Styles = [new(2, 1, 0), new(2, 2, 0), new(2, 3, 0)];

    /// <summary>The seed of the lone-glyph image.</summary>
    private static readonly Jbig2TestTextStyle Different = new(3, 9, 0);

    /// <summary>The spacing of the pixels flipped to give near copies.</summary>
    private static readonly Jbig2Size FlipSpacing = new(17, 6);

    /// <summary>Each symbol mode gives every image back exactly, through the shared globals.</summary>
    /// <param name="mode">The symbol mode.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Jbig2EncodeMode.SymbolText)]
    [Arguments(Jbig2EncodeMode.SymbolTextRefined)]
    [Arguments(Jbig2EncodeMode.Smallest)]
    public async Task SharedImagesRoundTrip(Jbig2EncodeMode mode)
    {
        var rows = Images(true);
        var inputs = Inputs(rows);
        var coded = Jbig2Encoder.EncodeShared(inputs, new() { Mode = mode });

        await Assert.That(coded.Globals.Length).IsGreaterThan(0);
        await Assert.That(coded.Pages.Count).IsEqualTo(rows.Length);
        for (var i = 0; i < rows.Length; i++)
        {
            var decoded = Jbig2TestPages.Decode(coded.Pages[i], coded.Globals, Page);

            await Assert.That(decoded).IsNotNull();
            await Assert.That(decoded!.AsSpan().SequenceEqual(rows[i])).IsTrue().Because($"image {i} mode {mode}");
        }
    }

    /// <summary>Reusing output buffers replaces both the streams and their page lengths.</summary>
    /// <param name="mode">The symbol mode.</param>
    /// <param name="cancellationToken">Cancels the encode.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Jbig2EncodeMode.SymbolText)]
    [Arguments(Jbig2EncodeMode.SymbolTextRefined)]
    [Arguments(Jbig2EncodeMode.Smallest)]
    [Arguments(Jbig2EncodeMode.LossySymbolMerging)]
    public async Task ReusedBuffersMatchOwnedOutput(Jbig2EncodeMode mode, CancellationToken cancellationToken)
    {
        var inputs = Inputs(Images(true));
        var options = new Jbig2EncodeOptions { Mode = mode };
        var expected = Jbig2Encoder.EncodeShared(inputs, options, cancellationToken);
        var globals = default(PooledBuffer);
        var data = default(PooledBuffer);
        var lengths = new int[inputs.Length];
        var matches = true;
        try
        {
            _ = Jbig2Encoder.EncodeShared(inputs, options, ref globals, ref data, lengths, cancellationToken);
            var actualMode = Jbig2Encoder.EncodeShared(inputs, options, ref globals, ref data, lengths, cancellationToken);
            matches &= actualMode == expected.Mode;
            matches &= globals.WrittenSpan.SequenceEqual(expected.Globals);
            var offset = 0;
            for (var i = 0; i < inputs.Length; i++)
            {
                matches &= data.WrittenSpan.Slice(offset, lengths[i]).SequenceEqual(expected.Pages[i]);
                offset += lengths[i];
            }

            matches &= offset == data.Length;
        }
        finally
        {
            globals.Dispose();
            data.Dispose();
        }

        await Assert.That(matches).IsTrue();
    }

    /// <summary>One shared dictionary costs less than a dictionary in every image.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SharingSavesBytes()
    {
        var rows = Images(false);
        var inputs = Inputs(rows);
        var shared = Jbig2Encoder.EncodeShared(inputs, new() { Mode = Jbig2EncodeMode.SymbolText });
        var sharedTotal = shared.Globals.Length + shared.Pages.Sum(static page => page.Length);
        var separateTotal = rows.Sum(static r => Jbig2Encoder.Encode(r, Page.Width, Page.Height, new() { Mode = Jbig2EncodeMode.SymbolText }).Data.Length);

        await Assert.That(sharedTotal).IsLessThan(separateTotal);
    }

    /// <summary>Lossy sharing reports itself lossy and keeps every image close to its original.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LossySharingStaysClose()
    {
        var rows = Images(true);
        var inputs = Inputs(rows);
        var coded = Jbig2Encoder.EncodeShared(inputs, new() { Mode = Jbig2EncodeMode.LossySymbolMerging, MergeSimilarity = LossySimilarity });

        await Assert.That(coded.IsLossless).IsFalse();
        for (var i = 0; i < rows.Length; i++)
        {
            var decoded = Jbig2TestPages.Decode(coded.Pages[i], coded.Globals, Page);

            await Assert.That(Jbig2TestPages.CountDifferences(rows[i], decoded!, Page)).IsLessThan(Page.Width * Page.Height / LossyShare);
        }
    }

    /// <summary>The shared encoder rejects generic modes, an empty list and a missing list.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvalidArgumentsThrow()
    {
        var rows = Images(false);
        var inputs = Inputs(rows);

        await Assert.That(() => Jbig2Encoder.EncodeShared(inputs, new() { Mode = Jbig2EncodeMode.GenericRegion })).Throws<ArgumentException>();
        await Assert.That(static () => Jbig2Encoder.EncodeShared([], new())).Throws<ArgumentOutOfRangeException>();
        await Assert.That(static () => Jbig2Encoder.EncodeShared(null!, new())).Throws<ArgumentNullException>();
    }

    /// <summary>Wraps rows as encoder inputs.</summary>
    /// <param name="rows">The images' rows.</param>
    /// <returns>The inputs.</returns>
    private static Jbig2PageInput[] Inputs(byte[][] rows)
    {
        var inputs = new Jbig2PageInput[rows.Length];
        for (var i = 0; i < rows.Length; i++)
        {
            inputs[i] = new(rows[i], Page.Width, Page.Height);
        }

        return inputs;
    }

    /// <summary>Builds the images: three text pages with the same glyphs, a blank page and a page of one different mark.</summary>
    /// <param name="perturbed">Whether pixels are flipped so repeated glyphs are near copies.</param>
    /// <returns>The images' rows.</returns>
    private static byte[][] Images(bool perturbed)
    {
        var images = new List<byte[]>();
        foreach (var style in Styles)
        {
            var rows = Jbig2TestPages.Text(Page, style);
            if (perturbed)
            {
                for (var y = 0; y < Page.Height; y += FlipSpacing.Height)
                {
                    for (var x = 1; x < Page.Width; x += FlipSpacing.Width)
                    {
                        Jbig2TestPages.Flip(rows, Page.Width, x, y);
                    }
                }
            }

            images.Add(rows);
        }

        images.Add(Jbig2TestPages.White(Page.Width, Page.Height));
        images.Add(Jbig2TestPages.Text(Page, Different));
        return [.. images];
    }
}
