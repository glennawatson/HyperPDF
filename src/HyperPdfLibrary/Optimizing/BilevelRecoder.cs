// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>Re-encodes black-and-white images bit for bit, as Group 4 fax or Flate.</summary>
internal static class BilevelRecoder
{
    /// <summary>The bits of an 8-bit sample.</summary>
    private const int ByteBits = 8;

    /// <summary>The shift from a pixel count to a byte count of packed bits.</summary>
    private const int ByteShift = 3;

    /// <summary>The mask of a bit index within a byte.</summary>
    private const int BitMask = 7;

    /// <summary>Determines whether an image has one bit per pixel and one component, so its bits can be coded as they are.</summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <returns><see langword="true"/> for stencil masks and 1-bit grey, indexed and separation images.</returns>
    internal static bool IsBilevel(PdfDictionary dictionary)
    {
        var bits = dictionary.GetBoolean(KnownName.ImageMask) ? 1 : dictionary.GetInt32(KnownName.BitsPerComponent, ByteBits);
        return bits == 1 && ImageColor.HasOneComponent(dictionary);
    }

    /// <summary>Re-encodes a 1-bit image.</summary>
    /// <param name="image">The image.</param>
    /// <param name="codec">Its codec.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The outcome.</returns>
    internal static ImageOutcome Recode(PdfStream image, CodecKind codec, int width, int height, CancellationToken cancellationToken)
    {
        var parameters = ImageFilters.CodecParameters(image.Dictionary);
        if (codec == CodecKind.Jpeg || (codec == CodecKind.Fax && (parameters?.GetInt32(KnownName.K, 0) ?? 0) < 0))
        {
            // JPEG is not 1-bit, and Group 4 is already the best fax coding.
            return ImageOutcome.Unchanged;
        }

        var stride = (width + BitMask) >> ByteShift;
        var decoded = default(PooledBuffer);
        var rows = default(PooledBuffer);
        try
        {
            if (image.Decode(ref decoded, cancellationToken) == PdfImageCodec.Ccitt)
            {
                var target = rows.GetSpan(stride * height)[..(stride * height)];
                CcittFaxDecoder.Decode(decoded.WrittenSpan, CcittParameters.FromDictionary(parameters), width, height, target);
                rows.Advance(stride * height);
            }
            else if (decoded.Length >= stride * height)
            {
                rows.Write(decoded.WrittenSpan[..(stride * height)]);
            }
            else
            {
                return ImageOutcome.Unchanged;
            }

            return Code(image, rows.WrittenSpan, width, height, "1-bit image");
        }
        finally
        {
            decoded.Dispose();
            rows.Dispose();
        }
    }

    /// <summary>Packs a black-and-white greyscale image into 1-bit rows and codes it.</summary>
    /// <param name="image">The image.</param>
    /// <param name="samples">The 8-bit samples.</param>
    /// <param name="size">The size.</param>
    /// <returns>The outcome.</returns>
    internal static ImageOutcome PackAndCode(PdfStream image, ReadOnlySpan<byte> samples, PixelSize size)
    {
        var packed = default(PooledBuffer);
        try
        {
            BilevelEncoder.Pack(samples, size.Width, size.Height, ref packed);
            return Code(image, packed.WrittenSpan, size.Width, size.Height, "Black-and-white greyscale image");
        }
        finally
        {
            packed.Dispose();
        }
    }

    /// <summary>Codes packed rows and reports which coding won.</summary>
    /// <param name="image">The image.</param>
    /// <param name="rows">The packed rows.</param>
    /// <param name="width">The width.</param>
    /// <param name="height">The height.</param>
    /// <param name="what">What the image was.</param>
    /// <returns>The outcome.</returns>
    private static ImageOutcome Code(PdfStream image, ReadOnlySpan<byte> rows, int width, int height, string what)
    {
        if (BilevelEncoder.Encode(image.Dictionary, rows, width, height, image.RawLength) is not { } result)
        {
            return ImageOutcome.Unchanged;
        }

        var coding = result.Dictionary.GetName(KnownName.Filter).Is(KnownName.CCITTFaxDecode) ? "CCITT Group 4" : "Flate";
        return new(result, string.Create(CultureInfo.InvariantCulture, $"{what} {width}x{height} saved losslessly as {coding}."));
    }
}
