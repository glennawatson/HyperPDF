// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Images.Jpeg;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>
/// Interprets JPEG component samples through the PDF color space and decode array. Ordinary gray and YCbCr images
/// use the configured image codec when available. CMYK and untransformed RGB use the managed decoder, which also
/// handles JPEGs the image codec cannot read.
/// </summary>
internal static class JpegImageDecoder
{
    /// <summary>What the repair log says about a JPEG that ends before its end-of-image marker.</summary>
    private const string TruncatedMessage = "A JPEG image ends without its end marker; the part that decoded is used.";

    /// <summary>The components of a two-channel JPEG, which only a two-component colour space can interpret.</summary>
    private const int TwoComponents = 2;

    /// <summary>The components of an RGB JPEG.</summary>
    private const int RgbComponents = 3;

    /// <summary>The components of a CMYK JPEG.</summary>
    private const int CmykComponents = 4;

    /// <summary>The bits of a JPEG sample.</summary>
    private const int SampleBits = 8;

    /// <summary>The offset of the red byte in a BGRA pixel.</summary>
    private const int RedIndex = 2;

    /// <summary>Decodes a JPEG image with no <c>/ColorTransform</c> entry.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The JPEG data.</param>
    /// <returns>The image, <see langword="null"/> when it is too large, or an image marked unsupported when no decoder can read it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData? Decode(in ImageHeader header, ReadOnlySpan<byte> data) =>
        Decode(header, data, JpegInfo.NoColorTransform);

    /// <summary>Decodes a JPEG image.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The JPEG data.</param>
    /// <param name="colorTransform">The <c>/ColorTransform</c> decode parameter, or <see cref="JpegInfo.NoColorTransform"/>.</param>
    /// <returns>The image, <see langword="null"/> when it is too large, or an image marked unsupported when no decoder can read it.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData? Decode(in ImageHeader header, ReadOnlySpan<byte> data, int colorTransform) =>
        Decode(header, data, colorTransform, null, 0, null, CancellationToken.None);

    /// <summary>Decodes a JPEG image, reporting data that ends before its end-of-image marker.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The JPEG data.</param>
    /// <param name="colorTransform">The <c>/ColorTransform</c> decode parameter, or <see cref="JpegInfo.NoColorTransform"/>.</param>
    /// <param name="context">The open context that receives the report, or <see langword="null"/>.</param>
    /// <param name="objectNumber">The image stream's object number, or 0.</param>
    /// <param name="imageCodec">The optional provider codec.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The image, <see langword="null"/> when it is too large, or an image marked unsupported when no decoder can read it.</returns>
    internal static PdfImageData? Decode(
        in ImageHeader header,
        ReadOnlySpan<byte> data,
        int colorTransform,
        PdfOpenContext? context,
        int objectNumber,
        IPdfImageCodec? imageCodec,
        CancellationToken cancellationToken)
    {
        var scratchPools = ScratchFor(context);
        cancellationToken.ThrowIfCancellationRequested();
        ReportIfTruncated(data, context, objectNumber);

        if (!JpegMarkers.TryReadInfo(data, out var info) || !info.IsSupported)
        {
            return Unsupported(header);
        }

        if ((long)info.Width * info.Height > ImageHeader.MaxPixels)
        {
            return null;
        }

        // A header that claims far more image than the data could hold is damaged or hostile; refuse it before renting buffers.
        if (!JpegDecoder.IsPlausibleSize(info, data.Length))
        {
            return Unsupported(header);
        }

        var convert = info.UsesColorTransform(colorTransform);
        var image = NeedsRawSamples(info, convert)
            ? DecodeManaged(header, data, info, convert, scratchPools, cancellationToken)
            : DecodeProvider(header, data, info, imageCodec, scratchPools) ?? DecodeManaged(header, data, info, convert, scratchPools, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        return image ?? Unsupported(header);
    }

    /// <summary>Determines whether only the managed decoder can read the file: CMYK, untransformed RGB, two channels, or more than 8 bits.</summary>
    /// <param name="info">The JPEG's headers.</param>
    /// <param name="convert">Whether the samples are YCbCr or YCCK.</param>
    /// <returns><see langword="true"/> when the provider codec is not used.</returns>
    private static bool NeedsRawSamples(in JpegInfo info, bool convert) =>
        info.Components == CmykComponents
        || info.Precision != SampleBits
        || info.Components == TwoComponents
        || (info.Components == RgbComponents && !convert);

    /// <summary>Reports JPEG data that ends before its end-of-image marker.</summary>
    /// <param name="data">The JPEG data.</param>
    /// <param name="context">The open context, or <see langword="null"/>.</param>
    /// <param name="objectNumber">The image stream's object number, or 0.</param>
    private static void ReportIfTruncated(ReadOnlySpan<byte> data, PdfOpenContext? context, int objectNumber)
    {
        // A cut-off scan leaves no end marker, so one tail check covers both decoders. The context keeps each report once per stream.
        if (context is not null && !JpegMarkers.EndsWithEndOfImage(data))
        {
            PdfOpenContext.Report(context, PdfDiagnosticCode.TruncatedStream, TruncatedMessage, objectNumber, -1);
        }
    }

    /// <summary>Creates the result for a JPEG that no decoder can read.</summary>
    /// <param name="header">The image header.</param>
    /// <returns>An image with no pixels that names the JPEG codec.</returns>
    private static PdfImageData Unsupported(in ImageHeader header) =>
        new(header.Width, header.Height, [], header.IsStencil, header.Interpolate, PdfImageCodec.Jpeg);

    /// <summary>Gets the open document's cache, or one owned by a standalone decode.</summary>
    /// <param name="context">The open context, or null.</param>
    /// <returns>The bounded cache.</returns>
    private static ScratchPools ScratchFor(PdfOpenContext? context) => context?.ScratchPools ?? new();

    /// <summary>Gets the header an 8-bit JPEG decodes with: the JPEG's size, and a colour space that has its component count.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="info">The JPEG's headers.</param>
    /// <returns>The header for sample decoding.</returns>
    private static ImageHeader ForSamples(in ImageHeader header, in JpegInfo info)
    {
        var space = header.ColorSpace;
        var matches = space is not null && space.Components == info.Components;
        return header with
        {
            Width = info.Width,
            Height = info.Height,
            BitsPerComponent = SampleBits,
            IsStencil = false,
            ColorSpace = matches ? space : PdfColorSpace.FromComponents(info.Components),
            Decode = matches ? header.Decode : null,
        };
    }

    /// <summary>Decodes with the managed decoder and runs the raw samples through the colour space.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The JPEG data.</param>
    /// <param name="info">The JPEG's headers.</param>
    /// <param name="convert">Whether the samples are YCbCr or YCCK and need converting.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The image, or <see langword="null"/> when the JPEG cannot be decoded.</returns>
    private static PdfImageData? DecodeManaged(in ImageHeader header, ReadOnlySpan<byte> data, in JpegInfo info, bool convert, ScratchPools scratchPools, CancellationToken cancellationToken)
    {
        // No device colour space has two components, so such a JPEG needs an image colour space that does.
        if (info.Components == TwoComponents && header.ColorSpace?.Components != TwoComponents)
        {
            return null;
        }

        var length = info.Width * info.Height * info.Components;
        var pool = scratchPools.Get<byte>();
        var samples = pool.Rent(length);
        try
        {
            var raw = samples.AsSpan(0, length);
            return JpegDecoder.TryDecode(data, convert, raw, scratchPools, cancellationToken)
                ? PdfImageDecoder.DecodeSamples(ForSamples(header, info), raw, scratchPools)
                : null;
        }
        finally
        {
            pool.Return(samples);
        }
    }

    /// <summary>Decodes ordinary gray or RGB JPEG pixels using the configured image codec.</summary>
    /// <param name="header">The PDF image header.</param>
    /// <param name="data">The JPEG bytes.</param>
    /// <param name="info">The JPEG dimensions and component layout.</param>
    /// <param name="codec">The optional provider codec.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <returns>The decoded image, or null when the provider cannot decode it.</returns>
    private static PdfImageData? DecodeProvider(in ImageHeader header, ReadOnlySpan<byte> data, in JpegInfo info, IPdfImageCodec? codec, ScratchPools scratchPools)
    {
        if (codec is null)
        {
            return null;
        }

        if (info.Components == 1)
        {
            return DecodeGray(codec, data, ForSamples(header, info), scratchPools);
        }

        var pixels = PixelMemory.Allocate((long)info.Width * info.Height * PixelConverter.BytesPerPixel, out var pinned);
        if (!codec.TryDecodeJpeg(data, new(info.Width, info.Height, PdfImagePixelFormat.Bgra8888), pixels))
        {
            return null;
        }

        Recolor(header with { Width = info.Width, Height = info.Height }, pixels, scratchPools);
        return new(info.Width, info.Height, pixels, false, header.Interpolate, PdfImageCodec.None) { IsPinned = pinned };
    }

    /// <summary>Runs decoded gray samples through the PDF color space and decode array.</summary>
    /// <param name="codec">The configured image decoder.</param>
    /// <param name="data">The JPEG bytes.</param>
    /// <param name="header">The image header with JPEG dimensions.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <returns>The interpreted image, or null when decoding fails.</returns>
    private static PdfImageData? DecodeGray(IPdfImageCodec codec, ReadOnlySpan<byte> data, in ImageHeader header, ScratchPools scratchPools)
    {
        var length = header.Width * header.Height;
        var pool = scratchPools.Get<byte>();
        var gray = pool.Rent(length);
        try
        {
            var samples = gray.AsSpan(0, length);
            return codec.TryDecodeJpeg(data, new(header.Width, header.Height, PdfImagePixelFormat.Gray8), samples)
                ? PdfImageDecoder.DecodeSamples(header, samples, scratchPools)
                : null;
        }
        finally
        {
            pool.Return(gray);
        }
    }

    /// <summary>Applies a /Decode array or a non-RGB three-component colour space to decoded RGB pixels.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="pixels">The BGRA pixels, converted in place.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    private static void Recolor(in ImageHeader header, byte[] pixels, ScratchPools scratchPools)
    {
        var space = header.ColorSpace;
        if (space is null || space.Components != RgbComponents || (header.Decode is null && space.Kind == PdfColorSpaceKind.DeviceRgb))
        {
            return;
        }

        var unpacker = new SampleUnpacker(SampleBits, RgbComponents, header.GetDecode(), space, null);
        var pool = scratchPools.Get<byte>();
        var samples = pool.Rent(header.Width * RgbComponents);
        try
        {
            for (var y = 0; y < header.Height; y++)
            {
                var row = pixels.AsSpan(y * header.Width * PixelConverter.BytesPerPixel, header.Width * PixelConverter.BytesPerPixel);
                for (var x = 0; x < header.Width; x++)
                {
                    var pixel = row.Slice(x * PixelConverter.BytesPerPixel, PixelConverter.BytesPerPixel);
                    var sample = samples.AsSpan(x * RgbComponents, RgbComponents);
                    sample[0] = unpacker.Map(0, pixel[RedIndex]);
                    sample[1] = unpacker.Map(1, pixel[1]);
                    sample[RedIndex] = unpacker.Map(RedIndex, pixel[0]);
                }

                space.ConvertRow(samples, row, header.Width);
            }
        }
        finally
        {
            pool.Return(samples);
        }
    }
}
