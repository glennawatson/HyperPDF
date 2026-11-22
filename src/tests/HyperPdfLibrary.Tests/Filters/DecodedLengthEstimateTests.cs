// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Compat;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>
/// Checks the size an image stream is expected to decode to, and that a stream decodes to the same bytes whether the
/// estimate is exact, too small or too large.
/// </summary>
public sealed class DecodedLengthEstimateTests
{
    /// <summary>The image width in pixels.</summary>
    private const int Width = 10;

    /// <summary>The image height in pixels.</summary>
    private const int Height = 6;

    /// <summary>The components of an RGB colour.</summary>
    private const int Rgb = 3;

    /// <summary>The bits of a byte sample.</summary>
    private const int ByteBits = 8;

    /// <summary>The bytes one row of a 1-bit image of the test width takes, with the row's predictor tag byte.</summary>
    private const int MaskRowBytes = 3;

    /// <summary>An encoded length large enough not to bound the test estimates.</summary>
    private const int AmpleEncodedLength = 1024;

    /// <summary>The length of the sample data decoded in the round trips: far more than the dictionary claims.</summary>
    private const int LongDataLength = 200_000;

    /// <summary>The multiplier that scatters sample values.</summary>
    private const int Scatter = 73;

    /// <summary>A gray, an RGB and a stencil image are sized from their rows, each with room for a predictor tag byte.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImagesAreSizedFromTheirRows()
    {
        var gray = Image(KnownName.DeviceGray);
        var rgb = Image(KnownName.DeviceRGB);
        var mask = new PdfDictionary(null);
        mask.Add(KnownName.Width, PdfValue.FromInteger(Width));
        mask.Add(KnownName.Height, PdfValue.FromInteger(Height));
        mask.Add(KnownName.ImageMask, PdfValue.FromBoolean(true));

        await Assert.That(DecodedLengthEstimate.ForImage(gray, AmpleEncodedLength)).IsEqualTo((Width + 1) * Height);
        await Assert.That(DecodedLengthEstimate.ForImage(rgb, AmpleEncodedLength)).IsEqualTo(((Width * Rgb) + 1) * Height);
        await Assert.That(DecodedLengthEstimate.ForImage(mask, AmpleEncodedLength)).IsEqualTo(MaskRowBytes * Height);
    }

    /// <summary>A stream that is not an image, or whose colour space needs parsing, gets no estimate.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OtherStreamsGetNoEstimate()
    {
        var separation = Image(KnownName.DeviceGray);
        separation.Set(KnownName.ColorSpace, PdfValue.FromName(KnownName.Pattern));

        await Assert.That(DecodedLengthEstimate.ForImage(new(null), AmpleEncodedLength)).IsEqualTo(0);
        await Assert.That(DecodedLengthEstimate.ForImage(separation, AmpleEncodedLength)).IsEqualTo(0);
    }

    /// <summary>A dictionary cannot claim more than the encoded data could expand to.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheEncodedLengthBoundsTheEstimate()
    {
        var huge = Image(KnownName.DeviceRGB);
        huge.Set(KnownName.Width, PdfValue.FromInteger(int.MaxValue));
        huge.Set(KnownName.Height, PdfValue.FromInteger(int.MaxValue));

        await Assert.That(DecodedLengthEstimate.ForImage(huge, 1)).IsLessThanOrEqualTo(AmpleEncodedLength + AmpleEncodedLength);
        await Assert.That(DecodedLengthEstimate.ForImage(huge, 0)).IsEqualTo(0);
    }

    /// <summary>A stream decodes to its own bytes when the dictionary claims far less than it holds, exactly what it holds, or far more.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecodingDoesNotDependOnTheEstimate()
    {
        var samples = new byte[LongDataLength];
        for (var i = 0; i < samples.Length; i++)
        {
            samples[i] = (byte)((i * Scatter) + (i / Rgb));
        }

        var compressed = default(PooledBuffer);
        ZLibCodec.Compress(samples, ref compressed);
        var encoded = compressed.ToArray();
        compressed.Dispose();

        foreach (var height in (int[])[1, LongDataLength / Width, LongDataLength])
        {
            var dictionary = Image(KnownName.DeviceGray);
            dictionary.Set(KnownName.Height, PdfValue.FromInteger(height));
            dictionary.Add(KnownName.Filter, PdfValue.FromName(KnownName.FlateDecode));
            var output = default(PooledBuffer);
            _ = PdfStreamDecoder.Decode(new(dictionary, encoded), ref output);
            var decoded = output.ToArray();
            output.Dispose();

            await Assert.That(decoded.AsSpan().SequenceEqual(samples)).IsTrue();
        }
    }

    /// <summary>Builds the dictionary of an 8-bit image in a device colour space.</summary>
    /// <param name="colorSpace">The colour space name.</param>
    /// <returns>The dictionary.</returns>
    private static PdfDictionary Image(KnownName colorSpace)
    {
        var dictionary = new PdfDictionary(null);
        dictionary.Add(KnownName.Width, PdfValue.FromInteger(Width));
        dictionary.Add(KnownName.Height, PdfValue.FromInteger(Height));
        dictionary.Add(KnownName.BitsPerComponent, PdfValue.FromInteger(ByteBits));
        dictionary.Add(KnownName.ColorSpace, PdfValue.FromName(colorSpace));
        return dictionary;
    }
}
