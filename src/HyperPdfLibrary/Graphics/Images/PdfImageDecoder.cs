// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Images.Jbig2;
using HyperPdfLibrary.Graphics.Images.Jpeg;
using HyperPdfLibrary.Graphics.Images.Jpx;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>
/// Decodes image XObjects and inline images (PDF 32000 section 8.9) to premultiplied BGRA, or to coverage for stencil
/// masks. Handles 1 to 16-bit samples, /Decode arrays, colour-key, stencil and soft masks (an image smaller than its
/// mask is enlarged to the mask), soft-mask /Matte, JPEG (an optional image codec, with a managed decoder for CMYK and progressive
/// cases), JPEG 2000, JBIG2 and CCITT fax. High-throughput JPEG 2000 is reported, not decoded. Damaged images, images over a 512 MB byte budget, and tiny streams that declare far more data
/// than they hold give <see langword="null"/>.
/// </summary>
public static class PdfImageDecoder
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits of a stencil or fax sample.</summary>
    private const int OneBit = 1;

    /// <summary>How many times shorter than declared the sample data may be before the image is rejected.</summary>
    private const int MaxShortfall = 1000;

    /// <summary>Decodes an image XObject.</summary>
    /// <param name="image">The image stream.</param>
    /// <returns>The image, or <see langword="null"/> when it cannot be decoded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfImageData? Decode(PdfStream image) => Decode(image, null);

    /// <summary>Decodes an image XObject whose /ColorSpace may name a page or form resource.</summary>
    /// <param name="image">The image stream.</param>
    /// <param name="resources">
    /// The page or form resource dictionary, or its /ColorSpace dictionary, used to resolve a named colour space; or
    /// <see langword="null"/>.
    /// </param>
    /// <returns>The image, or <see langword="null"/> when it cannot be decoded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="image"/> is <see langword="null"/>.</exception>
    public static PdfImageData? Decode(PdfStream image, PdfDictionary? resources)
    {
        ArgumentNullException.ThrowIfNull(image);
        return DecodeStream(image, ImageRole.Image, resources?.GetDictionary(KnownName.ColorSpace) ?? resources);
    }

    /// <summary>Decodes an inline image.</summary>
    /// <param name="dictionary">The inline image dictionary, which may use abbreviated keys and values.</param>
    /// <param name="data">The image data between ID and EI.</param>
    /// <param name="resources">
    /// The /ColorSpace resource dictionary for named colour spaces, or a resource dictionary holding one, or
    /// <see langword="null"/>.
    /// </param>
    /// <returns>The image, or <see langword="null"/> when it cannot be decoded.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dictionary"/> is <see langword="null"/>.</exception>
    public static PdfImageData? DecodeInline(PdfDictionary dictionary, ReadOnlySpan<byte> data, PdfDictionary? resources)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        var colorSpaces = resources?.GetDictionary(KnownName.ColorSpace) ?? resources;
        if (ImageHeader.FromInline(dictionary, colorSpaces) is not { } header)
        {
            return null;
        }

        var filters = ImageHeader.Get(dictionary, KnownName.Filter, KnownName.F, true);
        var parameters = ImageHeader.Get(dictionary, KnownName.DecodeParms, KnownName.DP, true);
        var buffer = default(PooledBuffer);
        try
        {
            var codec = PdfStreamDecoder.Apply(data, filters, parameters, ref buffer);
            return DecodeData(header, codec, buffer.WrittenSpan, CodecParameters(filters, parameters), 0, default);
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or OutOfMemoryException)
        {
            return null;
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Decodes an image stream in a role.</summary>
    /// <param name="image">The image stream.</param>
    /// <param name="role">What the stream is decoded as.</param>
    /// <param name="colorSpaces">The /ColorSpace resource dictionary, or <see langword="null"/>.</param>
    /// <returns>The image, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData? DecodeStream(PdfStream image, ImageRole role, PdfDictionary? colorSpaces) =>
        DecodeStream(image, role, colorSpaces, false, 0);

    /// <summary>Decodes an image stream in a role.</summary>
    /// <param name="image">The image stream.</param>
    /// <param name="role">What the stream is decoded as.</param>
    /// <param name="colorSpaces">The /ColorSpace resource dictionary, or <see langword="null"/>.</param>
    /// <param name="compact">Whether the image may come out as gray bytes; ignored when the image gets a mask afterwards.</param>
    /// <returns>The image, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData? DecodeStream(PdfStream image, ImageRole role, PdfDictionary? colorSpaces, bool compact) =>
        DecodeStream(image, role, colorSpaces, compact, 0);

    /// <summary>Decodes an image stream at the selected JPEG 2000 or JBIG2 resolution.</summary>
    /// <param name="image">The image stream.</param>
    /// <param name="role">The image role.</param>
    /// <param name="colorSpaces">The colour-space resources.</param>
    /// <param name="compact">Whether opaque gray pixels may stay compact.</param>
    /// <param name="reductionLevels">The finest resolution levels omitted.</param>
    /// <returns>The decoded image, or null.</returns>
    internal static PdfImageData? DecodeStream(PdfStream image, ImageRole role, PdfDictionary? colorSpaces, bool compact, int reductionLevels)
    {
        var dictionary = image.Dictionary;
        PdfCancellation.ThrowIfCancelled();
        if (ImageHeader.FromXObject(dictionary, colorSpaces) is not { } header)
        {
            return null;
        }

        header = role switch
        {
            ImageRole.SoftMask => header with { IsStencil = false, ColorSpace = PdfColorSpace.DeviceGray, ColorKey = null, Compact = true },
            ImageRole.StencilMask => header with { IsStencil = true, BitsPerComponent = OneBit, ColorSpace = null, ColorKey = null },
            _ => header with { Compact = compact && !HasMask(dictionary) },
        };

        var buffer = default(PooledBuffer);
        try
        {
            var codec = image.Decode(ref buffer);
            var softMaskInData = role == ImageRole.Image ? dictionary.GetInt32(KnownName.SMaskInData, 0) : 0;
            var parameters = CodecParameters(dictionary.Get(KnownName.Filter), dictionary.Get(KnownName.DecodeParms));
            var report = new ImageDecodeReport(dictionary.Owner?.Context, image.Id.Number, SelectReduction(role, reductionLevels));
            var result = DecodeData(header, codec, buffer.WrittenSpan, parameters, softMaskInData, report);
            return role == ImageRole.Image && result is { IsStencilMask: false, UnsupportedCodec: PdfImageCodec.None }
                ? ApplyMasks(result, dictionary, header.ColorSpace, colorSpaces, report.ReductionLevels)
                : result;
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException or OutOfMemoryException)
        {
            return null;
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>
    /// Decodes an image XObject for drawing: greyscale and bilevel images come out as one gray byte per pixel when their
    /// colours are all opaque grays, and pinned pixel arrays let a drawing backend retain them without a copy.
    /// </summary>
    /// <param name="image">The image stream.</param>
    /// <returns>The image, or <see langword="null"/> when it cannot be decoded.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData? DecodeCompact(PdfStream image) => DecodeStream(image, ImageRole.Image, null, true, 0);

    /// <summary>Decodes an image for drawing at a selected scan resolution.</summary>
    /// <param name="image">The image stream.</param>
    /// <param name="reductionLevels">The finest resolution levels omitted.</param>
    /// <returns>The decoded image, or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData? DecodeCompact(PdfStream image, int reductionLevels) =>
        DecodeStream(image, ImageRole.Image, null, true, reductionLevels);

    /// <summary>Decodes unpacked sample data: stencil coverage or BGRA through the colour space.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The samples, rows padded to whole bytes.</param>
    /// <returns>The image.</returns>
    internal static PdfImageData DecodeSamples(in ImageHeader header, ReadOnlySpan<byte> data)
    {
        var needed = (long)header.RowBytes * header.Height;
        if (data.Length >= needed)
        {
            return DecodeComplete(header, data);
        }

        // Short data reads as zero samples, as other viewers do.
        var padded = ScratchPool<byte>.Shared.Rent((int)needed);
        try
        {
            data.CopyTo(padded);
            padded.AsSpan(data.Length, (int)needed - data.Length).Clear();
            return DecodeComplete(header, padded.AsSpan(0, (int)needed));
        }
        finally
        {
            ScratchPool<byte>.Shared.Return(padded);
        }
    }

    /// <summary>Determines whether an image gets a soft mask or stencil mask stream, which need BGRA pixels to multiply into.</summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <returns><see langword="true"/> when the image has a mask stream.</returns>
    private static bool HasMask(PdfDictionary dictionary) =>
        dictionary.ContainsKey(KnownName.SMask) || dictionary.Get(KnownName.Mask).AsStream() is not null;

    /// <summary>Uses the requested resolution for images and soft masks.</summary>
    /// <param name="role">The image role.</param>
    /// <param name="requested">The requested reduction.</param>
    /// <returns>The safe reduction level.</returns>
    private static int SelectReduction(ImageRole role, int requested) =>
        (role is ImageRole.Image or ImageRole.SoftMask or ImageRole.StencilMask) && requested > 0 ? requested : 0;

    /// <summary>Decodes data after the byte filters, by codec.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="codec">The image codec still to apply.</param>
    /// <param name="data">The data.</param>
    /// <param name="codecParameters">The codec's decode parameters, or <see langword="null"/>.</param>
    /// <param name="softMaskInData">The image's /SMaskInData value; 0 for masks and inline images.</param>
    /// <returns>The image, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfImageData? DecodeData(in ImageHeader header, PdfImageCodec codec, ReadOnlySpan<byte> data, PdfDictionary? codecParameters, int softMaskInData) =>
        DecodeData(header, codec, data, codecParameters, softMaskInData, default);

    /// <summary>Decodes data after the byte filters, by codec, reporting damage to a context.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="codec">The image codec still to apply.</param>
    /// <param name="data">The data.</param>
    /// <param name="codecParameters">The codec's decode parameters, or <see langword="null"/>.</param>
    /// <param name="softMaskInData">The image's /SMaskInData value; 0 for masks and inline images.</param>
    /// <param name="report">The damage-report context and display resolution.</param>
    /// <returns>The image, or <see langword="null"/>.</returns>
    private static PdfImageData? DecodeData(
        in ImageHeader header,
        PdfImageCodec codec,
        ReadOnlySpan<byte> data,
        PdfDictionary? codecParameters,
        int softMaskInData,
        ImageDecodeReport report) => codec switch
    {
        PdfImageCodec.None => IsTooShort(header, data) ? null : DecodeSamples(header, data),
        PdfImageCodec.Jpeg => JpegImageDecoder.Decode(header, data, ColorTransform(codecParameters), report.Context, report.ObjectNumber),
        PdfImageCodec.Jpeg2000 => JpxImageDecoder.Decode(header, data, softMaskInData, report.ReductionLevels),
        PdfImageCodec.Ccitt => DecodeFax(header, data, codecParameters),
        PdfImageCodec.Jbig2 => Jbig2ImageDecoder.Decode(header, data, codecParameters, report.ReductionLevels),
        _ => new(header.Width, header.Height, [], header.IsStencil, header.Interpolate, codec),
    };

    /// <summary>Gets the <c>/ColorTransform</c> of a JPEG's decode parameters.</summary>
    /// <param name="codecParameters">The decode parameters, or <see langword="null"/>.</param>
    /// <returns>The value, or <see cref="JpegInfo.NoColorTransform"/>.</returns>
    private static int ColorTransform(PdfDictionary? codecParameters) =>
        codecParameters?.GetInt32(KnownName.ColorTransform, JpegInfo.NoColorTransform) ?? JpegInfo.NoColorTransform;

    /// <summary>Decodes CCITT fax data to 1-bit samples, then to pixels.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The fax data.</param>
    /// <param name="parameters">The CCITTFaxDecode parameters, or <see langword="null"/>.</param>
    /// <returns>The image.</returns>
    private static PdfImageData DecodeFax(in ImageHeader header, ReadOnlySpan<byte> data, PdfDictionary? parameters)
    {
        var fax = header with { BitsPerComponent = OneBit };
        if (fax.ColorSpace?.Components > 1)
        {
            fax = fax with { ColorSpace = PdfColorSpace.DeviceGray, Decode = null };
        }

        var length = fax.RowBytes * fax.Height;
        var rows = ScratchPool<byte>.Shared.Rent(length);
        try
        {
            var output = rows.AsSpan(0, length);
            CcittFaxDecoder.Decode(data, CcittParameters.FromDictionary(parameters), fax.Width, fax.Height, output);
            return DecodeSamples(fax, output);
        }
        finally
        {
            ScratchPool<byte>.Shared.Return(rows);
        }
    }

    /// <summary>Decodes data holding every row.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The samples.</param>
    /// <returns>The image.</returns>
    private static PdfImageData DecodeComplete(in ImageHeader header, ReadOnlySpan<byte> data)
    {
        if (header.IsStencil)
        {
            return DecodeStencil(header, data);
        }

        var space = header.ColorSpace ?? PdfColorSpace.DeviceGray;
        var unpacker = new SampleUnpacker(header.BitsPerComponent, space.Components, header.GetDecode(), space, header.ColorKey);
        return (header.Compact ? GrayImages.TryDecode(header, space, unpacker, data) : null) ?? DecodeColor(header, data, space, unpacker);
    }

    /// <summary>Decodes data holding every row to BGRA through the colour space.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The samples.</param>
    /// <param name="space">The colour space.</param>
    /// <param name="unpacker">Unpacks the samples.</param>
    /// <returns>The image.</returns>
    private static PdfImageData DecodeColor(in ImageHeader header, ReadOnlySpan<byte> data, PdfColorSpace space, SampleUnpacker unpacker)
    {
        var width = header.Width;
        var pixels = PixelMemory.Allocate((long)width * header.Height * PixelConverter.BytesPerPixel, out var pinned);
        var samples = ScratchPool<byte>.Shared.Rent(width * space.Components);
        var alpha = unpacker.HasColorKey ? ScratchPool<byte>.Shared.Rent(width * header.Height) : null;
        try
        {
            for (var y = 0; y < header.Height; y++)
            {
                var row = data.Slice(y * header.RowBytes, header.RowBytes);
                var rowAlpha = alpha is null ? [] : alpha.AsSpan(y * width, width);
                if (!unpacker.IsDirect)
                {
                    unpacker.Unpack(row, samples, rowAlpha, width);
                    row = samples;
                }

                space.ConvertRow(row, pixels.AsSpan(y * width * PixelConverter.BytesPerPixel, width * PixelConverter.BytesPerPixel), width);
            }

            if (alpha is not null)
            {
                ImageAlpha.Multiply(pixels, alpha.AsSpan(0, width * header.Height));
            }

            return new(width, header.Height, pixels, false, header.Interpolate, PdfImageCodec.None) { IsPinned = pinned };
        }
        finally
        {
            ScratchPool<byte>.Shared.Return(samples);
            if (alpha is not null)
            {
                ScratchPool<byte>.Shared.Return(alpha);
            }
        }
    }

    /// <summary>Decodes a stencil mask to coverage.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The 1-bit samples.</param>
    /// <returns>The coverage image.</returns>
    private static PdfImageData DecodeStencil(in ImageHeader header, ReadOnlySpan<byte> data)
    {
        var decode = header.GetDecode();
        var paintOnes = decode[0] > decode[1];
        var coverage = PixelMemory.Allocate((long)header.Width * header.Height, out var pinned);
        for (var y = 0; y < header.Height; y++)
        {
            var row = data.Slice(y * header.RowBytes, header.RowBytes);
            ImageAlpha.ExpandStencilRow(row, coverage.AsSpan(y * header.Width, header.Width), paintOnes);
        }

        return new(header.Width, header.Height, coverage, true, header.Interpolate, PdfImageCodec.None) { IsPinned = pinned };
    }

    /// <summary>Applies the image's /SMask, or else its stencil /Mask stream.</summary>
    /// <param name="image">The decoded image.</param>
    /// <param name="dictionary">The image dictionary.</param>
    /// <param name="space">The image's colour space, used to convert a soft mask's /Matte.</param>
    /// <param name="colorSpaces">The /ColorSpace resource dictionary, or <see langword="null"/>.</param>
    /// <param name="reductionLevels">The display resolution also requested of a soft mask.</param>
    /// <returns>The image with the mask applied; the input when there is no usable mask.</returns>
    private static PdfImageData ApplyMasks(PdfImageData image, PdfDictionary dictionary, PdfColorSpace? space, PdfDictionary? colorSpaces, int reductionLevels)
    {
        var softMask = dictionary.GetStream(KnownName.SMask);
        var mask = softMask is null ? dictionary.Get(KnownName.Mask).AsStream() : null;
        var decoded = softMask is not null ? DecodeStream(softMask, ImageRole.SoftMask, colorSpaces, true, reductionLevels) : DecodeMask(mask, colorSpaces, reductionLevels);
        if (decoded is null || decoded.Pixels.Length == 0)
        {
            return image;
        }

        var matte = softMask is null ? null : ImageMasks.ReadMatte(softMask.Dictionary, space);
        return ImageMasks.Apply(image, decoded, matte);
    }

    /// <summary>Determines whether decoded sample data is implausibly short for the declared size: under a thousandth.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The decoded samples.</param>
    /// <returns><see langword="true"/> when a tiny stream declares a huge image.</returns>
    private static bool IsTooShort(in ImageHeader header, ReadOnlySpan<byte> data) =>
        ((long)header.RowBytes * header.Height / MaxShortfall) > data.Length;

    /// <summary>Decodes a stencil /Mask stream.</summary>
    /// <param name="mask">The mask stream, or <see langword="null"/>.</param>
    /// <param name="colorSpaces">The /ColorSpace resource dictionary, or <see langword="null"/>.</param>
    /// <param name="reductionLevels">The display resolution selected for the image and mask pair.</param>
    /// <returns>The coverage, or <see langword="null"/>.</returns>
    private static PdfImageData? DecodeMask(PdfStream? mask, PdfDictionary? colorSpaces, int reductionLevels) =>
        mask is null ? null : DecodeStream(mask, ImageRole.StencilMask, colorSpaces, false, reductionLevels);

    /// <summary>Gets the decode parameters of the last filter, the one an image codec must be.</summary>
    /// <param name="filters">The /Filter value.</param>
    /// <param name="parameters">The /DecodeParms value.</param>
    /// <returns>The parameters, or <see langword="null"/>.</returns>
    private static PdfDictionary? CodecParameters(PdfValue filters, PdfValue parameters)
    {
        var count = filters.AsArray()?.Count ?? 1;
        return parameters.AsArray()?.GetDictionary(count - 1) ?? parameters.AsDictionary();
    }

    /// <summary>Context attached to a decoded image for damage reports and display resolution.</summary>
    /// <param name="Context">The PDF open context.</param>
    /// <param name="ObjectNumber">The image object number.</param>
    /// <param name="ReductionLevels">The finest levels omitted.</param>
    private readonly record struct ImageDecodeReport(PdfOpenContext? Context, int ObjectNumber, int ReductionLevels);
}
