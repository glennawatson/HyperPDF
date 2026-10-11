// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Graphics.Images.Jbig2;
using HyperPdfLibrary.Objects;
using PdfViewerLite.TestAssets;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>Tests JBIG2 images through the image pipeline: colour space, /Decode, image masks and /JBIG2Globals.</summary>
public sealed class Jbig2ImageDecoderTests
{
    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The largest byte.</summary>
    private const byte Full = 0xFF;

    /// <summary>The side of one reduced source block.</summary>
    private const int ReductionSide = 2;

    /// <summary>A gray JBIG2 image is white where the page is white and black where it is black.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GrayImageFollowsThePage()
    {
        var sample = Jbig2Samples.TextArith;
        var image = DecodeImage(Image(sample, false, false));

        await Assert.That(image is not null && GrayMatches(image, Rows(sample), false)).IsTrue();
    }

    /// <summary>A /Decode array of [1 0] inverts the image.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecodeArrayInverts()
    {
        var sample = Jbig2Samples.HalftoneArith;
        var image = DecodeImage(Image(sample, false, true));

        await Assert.That(image is not null && GrayMatches(image, Rows(sample), true)).IsTrue();
    }

    /// <summary>Reduced output averages the binary source while preserving an inverted /Decode array.</summary>
    /// <param name="inverted">Whether /Decode swaps black and white.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ReducedGrayAveragesBinarySource(bool inverted)
    {
        var sample = Jbig2Samples.TextArith;
        var image = DecodeImage(Image(sample, false, inverted), 1);

        await Assert.That(image is not null && ReducedMatches(image, Rows(sample), sample.Width, sample.Height, inverted)).IsTrue();
    }

    /// <summary>Direct compact reduction keeps one gray byte per pixel for either simple decode direction.</summary>
    /// <param name="inverted">Whether /Decode swaps black and white.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CompactReductionPreservesGraySamples(bool inverted)
    {
        var sample = Jbig2Samples.TextArith;
        var image = PdfImageDecoder.DecodeCompact(Image(sample, false, inverted), 1);

        await Assert.That(image is { IsGray: true } && CompactMatches(image, Rows(sample), sample.Width, sample.Height, inverted)).IsTrue();
    }

    /// <summary>Reduced stencil coverage paints the averaged black or white pixels selected by /Decode.</summary>
    /// <param name="paintOnes">Whether the inverted decode paints white source bits.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task ReducedStencilAveragesCoverage(bool paintOnes)
    {
        var sample = Jbig2Samples.TextArith;
        var image = DecodeImage(Image(sample, true, paintOnes), 1);

        await Assert.That(image is { IsStencilMask: true } && ReducedCoverageMatches(image, Rows(sample), sample.Width, sample.Height, paintOnes)).IsTrue();
    }

    /// <summary>A JBIG2 image mask paints where the page is black.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImageMaskPaintsBlackPixels()
    {
        var sample = Jbig2Samples.GenericMmr;
        var image = DecodeImage(Image(sample, true, false));

        await Assert.That(image is { IsStencilMask: true } && CoverageMatches(image, Rows(sample))).IsTrue();
    }

    /// <summary>Global segments come from the /JBIG2Globals stream in the decode parameters.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GlobalsComeFromDecodeParameters()
    {
        var sample = Jbig2Samples.SymbolsGlobalsRefinementAggregate;
        var image = DecodeImage(Image(sample, false, false));

        await Assert.That(image is not null && GrayMatches(image, Rows(sample), false)).IsTrue();
    }

    /// <summary>Data with no page gives no image.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedDataGivesNoImage()
    {
        var sample = Jbig2Samples.TextArith with { Data = [Full, Full, Full, Full, Full, Full, Full, Full, Full, Full, Full, Full], Globals = [] };
        var image = DecodeImage(Image(sample, false, false));

        await Assert.That(image).IsNull();
    }

    /// <summary>Builds a JBIG2 image XObject for a sample.</summary>
    /// <param name="sample">The sample.</param>
    /// <param name="mask">Whether the image is an image mask.</param>
    /// <param name="invert">Whether the /Decode array inverts the samples.</param>
    /// <returns>The image stream.</returns>
    private static PdfStream Image(Jbig2Sample sample, bool mask, bool invert)
    {
        var dictionary = new PdfDictionary(null);
        dictionary.Add(KnownName.Width, PdfValue.FromInteger(sample.Width));
        dictionary.Add(KnownName.Height, PdfValue.FromInteger(sample.Height));
        dictionary.Add(KnownName.Filter, PdfValue.FromName(KnownName.JBIG2Decode));
        if (mask)
        {
            dictionary.Add(KnownName.ImageMask, PdfValue.FromBoolean(true));
        }
        else
        {
            dictionary.Add(KnownName.BitsPerComponent, PdfValue.FromInteger(1));
            dictionary.Add(KnownName.ColorSpace, PdfValue.FromName(KnownName.DeviceGray));
        }

        if (invert)
        {
            dictionary.Add(KnownName.Decode, PdfValue.FromArray(PdfArray.FromNumbers(null, [1, 0])));
        }

        if (sample.Globals.Length > 0)
        {
            var parameters = new PdfDictionary(null);
            parameters.Add(KnownName.JBIG2Globals, PdfValue.FromStream(new(new PdfDictionary(null), sample.Globals)));
            dictionary.Add(KnownName.DecodeParms, PdfValue.FromDictionary(parameters));
        }

        return new(dictionary, sample.Data);
    }

    /// <summary>Decodes an image stream the way the image pipeline does: the byte filters, then the JBIG2 decoder.</summary>
    /// <param name="stream">The image stream.</param>
    /// <param name="reductionLevels">The number of resolution halvings.</param>
    /// <returns>The image, or <see langword="null"/>.</returns>
    private static PdfImageData? DecodeImage(PdfStream stream, int reductionLevels = 0)
    {
        if (ImageHeader.FromXObject(stream.Dictionary) is not { } header)
        {
            return null;
        }

        var buffer = default(PooledBuffer);
        try
        {
            var codec = stream.Decode(ref buffer);
            return codec == PdfImageCodec.Jbig2 ? Jbig2ImageDecoder.Decode(header, buffer.WrittenSpan, stream.Dictionary.GetDictionary(KnownName.DecodeParms), reductionLevels) : null;
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Checks each reduced gray pixel against the average of its packed binary source block.</summary>
    /// <param name="image">The reduced BGRA image.</param>
    /// <param name="rows">The packed source rows.</param>
    /// <param name="sourceWidth">The source width.</param>
    /// <param name="sourceHeight">The source height.</param>
    /// <param name="inverted">Whether the output is inverted.</param>
    /// <returns>Whether every displayed pixel matches.</returns>
    private static bool ReducedMatches(PdfImageData image, byte[] rows, int sourceWidth, int sourceHeight, bool inverted)
    {
        if (image.Width != (sourceWidth + 1) / ReductionSide || image.Height != (sourceHeight + 1) / ReductionSide)
        {
            return false;
        }

        var stride = Jbig2Decoder.GetRowBytes(sourceWidth);
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (image.Pixels[((y * image.Width) + x) * BytesPerPixel] != ExpectedGray(rows, stride, sourceWidth, sourceHeight, x, y, inverted))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Checks the reduced one-byte coverage against the source bits.</summary>
    /// <param name="image">The coverage image.</param>
    /// <param name="rows">The packed source rows.</param>
    /// <param name="sourceWidth">The source width.</param>
    /// <param name="sourceHeight">The source height.</param>
    /// <param name="paintOnes">Whether white bits are painted.</param>
    /// <returns>Whether every coverage sample matches.</returns>
    private static bool ReducedCoverageMatches(PdfImageData image, byte[] rows, int sourceWidth, int sourceHeight, bool paintOnes)
    {
        if (image.Width != (sourceWidth + 1) / ReductionSide || image.Height != (sourceHeight + 1) / ReductionSide)
        {
            return false;
        }

        var stride = Jbig2Decoder.GetRowBytes(sourceWidth);
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var white = ExpectedGray(rows, stride, sourceWidth, sourceHeight, x, y, false);
                if (image.Pixels[(y * image.Width) + x] != (paintOnes ? white : (byte)(Full - white)))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Checks each direct gray byte against the packed source bits.</summary>
    /// <param name="image">The compact gray image.</param>
    /// <param name="rows">The packed source rows.</param>
    /// <param name="sourceWidth">The source width.</param>
    /// <param name="sourceHeight">The source height.</param>
    /// <param name="inverted">Whether gray is reversed.</param>
    /// <returns>Whether every sample matches.</returns>
    private static bool CompactMatches(PdfImageData image, byte[] rows, int sourceWidth, int sourceHeight, bool inverted)
    {
        if (image.Width != (sourceWidth + 1) / ReductionSide || image.Height != (sourceHeight + 1) / ReductionSide)
        {
            return false;
        }

        var stride = Jbig2Decoder.GetRowBytes(sourceWidth);
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                if (image.Pixels[(y * image.Width) + x] != ExpectedGray(rows, stride, sourceWidth, sourceHeight, x, y, inverted))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Averages one source block independently of the image decoder.</summary>
    /// <param name="rows">The packed source rows.</param>
    /// <param name="stride">The packed row length.</param>
    /// <param name="width">The source width.</param>
    /// <param name="height">The source height.</param>
    /// <param name="x">The reduced column.</param>
    /// <param name="y">The reduced row.</param>
    /// <param name="inverted">Whether the output is inverted.</param>
    /// <returns>The expected gray sample.</returns>
    private static byte ExpectedGray(byte[] rows, int stride, int width, int height, int x, int y, bool inverted)
    {
        var white = 0;
        var count = 0;
        for (var sourceY = y * ReductionSide; sourceY < Math.Min((y + 1) * ReductionSide, height); sourceY++)
        {
            for (var sourceX = x * ReductionSide; sourceX < Math.Min((x + 1) * ReductionSide, width); sourceX++)
            {
                white += Jbig2Bits.Get(rows.AsSpan(sourceY * stride, stride), sourceX, width);
                count++;
            }
        }

        var expected = (byte)((white * Full) / count);
        return inverted ? (byte)(Full - expected) : expected;
    }

    /// <summary>Decodes a sample's rows directly.</summary>
    /// <param name="sample">The sample.</param>
    /// <returns>The rows, 1 for white.</returns>
    private static byte[] Rows(Jbig2Sample sample)
    {
        var rows = new byte[Jbig2Decoder.GetRowBytes(sample.Width) * sample.Height];
        _ = Jbig2Decoder.TryDecode(sample.Data, sample.Globals, sample.Width, sample.Height, rows);
        return rows;
    }

    /// <summary>Checks that every BGRA pixel is white or black as the rows say.</summary>
    /// <param name="image">The image.</param>
    /// <param name="rows">The rows, 1 for white.</param>
    /// <param name="inverted">Whether the image is inverted.</param>
    /// <returns><see langword="true"/> when every pixel matches.</returns>
    private static bool GrayMatches(PdfImageData image, byte[] rows, bool inverted)
    {
        var stride = Jbig2Decoder.GetRowBytes(image.Width);
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var white = Jbig2Bits.Get(rows.AsSpan(y * stride, stride), x, image.Width) != 0;
                if (image.Pixels[((y * image.Width) + x) * BytesPerPixel] != (white != inverted ? Full : (byte)0))
                {
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Checks that a mask covers exactly the black pixels.</summary>
    /// <param name="image">The mask.</param>
    /// <param name="rows">The rows, 1 for white.</param>
    /// <returns><see langword="true"/> when every pixel matches.</returns>
    private static bool CoverageMatches(PdfImageData image, byte[] rows)
    {
        var stride = Jbig2Decoder.GetRowBytes(image.Width);
        for (var y = 0; y < image.Height; y++)
        {
            for (var x = 0; x < image.Width; x++)
            {
                var black = Jbig2Bits.Get(rows.AsSpan(y * stride, stride), x, image.Width) == 0;
                if (image.Pixels[(y * image.Width) + x] != (black ? Full : (byte)0))
                {
                    return false;
                }
            }
        }

        return true;
    }
}
