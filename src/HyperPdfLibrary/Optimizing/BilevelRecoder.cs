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
    /// <param name="size">The width and height.</param>
    /// <param name="codings">The codings besides Flate that may be tried.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The outcome.</returns>
    internal static ImageOutcome Recode(PdfStream image, CodecKind codec, PixelSize size, in BilevelCodings codings, CancellationToken cancellationToken)
    {
        var (width, height, _) = size;
        var parameters = ImageFilters.CodecParameters(image.Dictionary);
        if (codec == CodecKind.Jpeg || (codec == CodecKind.Fax && codings.Jbig2 == PdfJbig2Mode.Off && (parameters?.GetInt32(KnownName.K, 0) ?? 0) < 0))
        {
            // JPEG is not 1-bit, and Group 4 is already the best fax coding unless JBIG2 may beat it.
            return ImageOutcome.Unchanged;
        }

        var decoded = default(PooledBuffer);
        var rows = default(PooledBuffer);
        try
        {
            return TryReadRows(image, parameters, size, ref decoded, ref rows, cancellationToken)
                ? Code(image, rows.WrittenSpan, size, "1-bit image", codings, cancellationToken)
                : ImageOutcome.Unchanged;
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
    /// <param name="codings">The codings besides Flate that may be tried.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The outcome.</returns>
    internal static ImageOutcome PackAndCode(PdfStream image, ReadOnlySpan<byte> samples, PixelSize size, in BilevelCodings codings, CancellationToken cancellationToken)
    {
        var packed = default(PooledBuffer);
        try
        {
            BilevelEncoder.Pack(samples, size.Width, size.Height, ref packed);
            return Code(image, packed.WrittenSpan, size, "Black-and-white greyscale image", codings, cancellationToken);
        }
        finally
        {
            packed.Dispose();
        }
    }

    /// <summary>Reads the packed rows of a 1-bit image, decoding fax data when that is how it is stored.</summary>
    /// <param name="image">The image.</param>
    /// <param name="parameters">Its codec parameters.</param>
    /// <param name="size">The width and height.</param>
    /// <param name="decoded">Receives the image after its filters.</param>
    /// <param name="rows">Receives the packed rows.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns><see langword="false"/> when the image has too little data.</returns>
    internal static bool TryReadRows(PdfStream image, PdfDictionary? parameters, PixelSize size, ref PooledBuffer decoded, ref PooledBuffer rows, CancellationToken cancellationToken)
    {
        var length = ((size.Width + BitMask) >> ByteShift) * size.Height;
        if (image.Decode(ref decoded, cancellationToken) == PdfImageCodec.Ccitt)
        {
            var target = rows.GetSpan(length)[..length];
            CcittFaxDecoder.Decode(decoded.WrittenSpan, CcittParameters.FromDictionary(parameters), size.Width, size.Height, target, image.Dictionary.Owner?.ScratchPools ?? new ScratchPools());
            rows.Advance(length);
            return true;
        }

        if (decoded.Length < length)
        {
            return false;
        }

        rows.Write(decoded.WrittenSpan[..length]);
        return true;
    }

    /// <summary>Codes packed rows and reports which coding won.</summary>
    /// <param name="image">The image.</param>
    /// <param name="rows">The packed rows.</param>
    /// <param name="size">The width and height.</param>
    /// <param name="what">What the image was.</param>
    /// <param name="codings">The codings besides Flate that may be tried.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The outcome.</returns>
    private static ImageOutcome Code(PdfStream image, ReadOnlySpan<byte> rows, PixelSize size, string what, in BilevelCodings codings, CancellationToken cancellationToken)
    {
        if (BilevelEncoder.Encode(image.Dictionary, rows, size.Width, size.Height, image.RawLength, codings, cancellationToken) is not { } result)
        {
            return ImageOutcome.Unchanged;
        }

        var note = result.IsLossless
            ? string.Create(CultureInfo.InvariantCulture, $"{what} {size.Width}x{size.Height} saved losslessly as {result.Coding}.")
            : string.Create(CultureInfo.InvariantCulture, $"{what} {size.Width}x{size.Height} saved as {result.Coding}; similar marks share one shape, so the pixels change slightly.");
        return new(result.Stream, note);
    }
}
