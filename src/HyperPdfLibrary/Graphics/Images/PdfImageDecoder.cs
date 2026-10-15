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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfImageData? Decode(PdfStream image, PdfDictionary? resources) => Decode(image, resources, CancellationToken.None);

    /// <summary>Decodes an image XObject while observing the caller's token.</summary>
    /// <param name="image">The image stream.</param>
    /// <param name="resources">The page or form resources.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The decoded image, or null.</returns>
    public static PdfImageData? Decode(PdfStream image, PdfDictionary? resources, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(image);
        return DecodeStream(image, ImageRole.Image, resources?.GetDictionary(KnownName.ColorSpace) ?? resources, false, 0, cancellationToken);
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
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfImageData? DecodeInline(PdfDictionary dictionary, ReadOnlySpan<byte> data, PdfDictionary? resources) =>
        DecodeInline(dictionary, data, resources, CancellationToken.None);

    /// <summary>Decodes an inline image while observing the caller's token.</summary>
    /// <param name="dictionary">The inline image dictionary.</param>
    /// <param name="data">The encoded image bytes.</param>
    /// <param name="resources">The colour-space resources.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The decoded image, or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfImageData? DecodeInline(PdfDictionary dictionary, ReadOnlySpan<byte> data, PdfDictionary? resources, CancellationToken cancellationToken) =>
        DecodeInline(dictionary, data, resources, null, cancellationToken);

    /// <summary>Decodes an inline image with explicit output-intent colours.</summary>
    /// <param name="dictionary">The inline image dictionary.</param>
    /// <param name="data">The encoded bytes.</param>
    /// <param name="resources">The colour-space resources.</param>
    /// <param name="colors">The output-intent substitutions, or null.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The decoded image, or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData? DecodeInline(PdfDictionary dictionary, ReadOnlySpan<byte> data, PdfDictionary? resources, OutputIntentColors? colors, CancellationToken cancellationToken) =>
        DecodeInline(dictionary, data, resources, colors, IccRendering.Default, cancellationToken);

    /// <summary>Decodes an inline image with explicit output-intent colours and ICC rendering.</summary>
    /// <param name="dictionary">The inline image dictionary.</param>
    /// <param name="data">The encoded bytes.</param>
    /// <param name="resources">The colour-space resources.</param>
    /// <param name="colors">The output-intent substitutions, or null.</param>
    /// <param name="rendering">The rendering intent and black point choice ICC colours convert with.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The decoded image, or null.</returns>
    internal static PdfImageData? DecodeInline(
        PdfDictionary dictionary,
        ReadOnlySpan<byte> data,
        PdfDictionary? resources,
        OutputIntentColors? colors,
        IccRendering rendering,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dictionary);
        var scratchPools = ScratchFor(dictionary);
        var colorSpaces = resources?.GetDictionary(KnownName.ColorSpace) ?? resources;
        if (ImageHeader.FromInline(dictionary, colorSpaces, colors) is not { } header)
        {
            return null;
        }

        header = header with { ColorSpace = header.ColorSpace?.ForRendering(rendering) };

        var filters = ImageHeader.Get(dictionary, KnownName.Filter, KnownName.F, true);
        var parameters = ImageHeader.Get(dictionary, KnownName.DecodeParms, KnownName.DP, true);
        var buffer = default(PooledBuffer);
        try
        {
            var codec = PdfStreamDecoder.Apply(data, filters, parameters, ref buffer, cancellationToken);
            return DecodeData(header, codec, buffer.WrittenSpan, CodecParameters(filters, parameters), 0, new(dictionary.Owner?.Context, 0, 0, scratchPools), cancellationToken);
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
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The decoded image, or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData? DecodeStream(PdfStream image, ImageRole role, PdfDictionary? colorSpaces, bool compact, int reductionLevels, CancellationToken cancellationToken = default) =>
        DecodeStream(image, role, colorSpaces, compact, reductionLevels, null, cancellationToken);

    /// <summary>Decodes an image stream with explicit output-intent colours.</summary>
    /// <param name="image">The image stream.</param>
    /// <param name="role">The image role.</param>
    /// <param name="colorSpaces">The colour-space resources.</param>
    /// <param name="compact">Whether gray samples may stay compact.</param>
    /// <param name="reductionLevels">The finest resolution levels omitted.</param>
    /// <param name="colors">The output-intent substitutions, or null.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The decoded image, or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData? DecodeStream(
        PdfStream image,
        ImageRole role,
        PdfDictionary? colorSpaces,
        bool compact,
        int reductionLevels,
        OutputIntentColors? colors,
        CancellationToken cancellationToken) =>
        DecodeStream(image, role, colorSpaces, compact, reductionLevels, new ImageColors(colors, IccRendering.Default), cancellationToken);

    /// <summary>Decodes an image stream with explicit output-intent colours and ICC rendering.</summary>
    /// <param name="image">The image stream.</param>
    /// <param name="role">The image role.</param>
    /// <param name="colorSpaces">The colour-space resources.</param>
    /// <param name="compact">Whether gray samples may stay compact.</param>
    /// <param name="reductionLevels">The finest resolution levels omitted.</param>
    /// <param name="colors">The output-intent substitutions and the rendering the image's ICC colours convert with.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The decoded image, or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData? DecodeStream(
        PdfStream image,
        ImageRole role,
        PdfDictionary? colorSpaces,
        bool compact,
        int reductionLevels,
        ImageColors colors,
        CancellationToken cancellationToken) =>
        DecodeStream(image, role, colorSpaces, compact, new ImageLevels(reductionLevels, 0), colors, cancellationToken);

    /// <summary>Decodes an image stream, optionally averaging the rows down while converting them.</summary>
    /// <param name="image">The image stream.</param>
    /// <param name="role">The image role.</param>
    /// <param name="colorSpaces">The colour-space resources.</param>
    /// <param name="compact">Whether gray samples may stay compact.</param>
    /// <param name="levels">The resolution levels the codec omits and the halvings averaged into the pixels while converting rows.</param>
    /// <param name="colors">The output-intent substitutions and ICC rendering.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The decoded image, or null.</returns>
    internal static PdfImageData? DecodeStream(
        PdfStream image,
        ImageRole role,
        PdfDictionary? colorSpaces,
        bool compact,
        ImageLevels levels,
        ImageColors colors,
        CancellationToken cancellationToken)
    {
        var dictionary = image.Dictionary;
        var scratchPools = ScratchFor(dictionary);
        cancellationToken.ThrowIfCancellationRequested();
        if (ImageHeader.FromXObject(dictionary, colorSpaces, colors.Substitutes) is not { } read)
        {
            return null;
        }

        var header = ForRole(read, role, compact, dictionary, colors.Rendering);
        var buffer = default(PooledBuffer);
        try
        {
            var codec = image.Decode(ref buffer, cancellationToken);
            header = WithBoxLevels(header, role, codec, dictionary, levels.Box);
            var softMaskInData = role == ImageRole.Image ? dictionary.GetInt32(KnownName.SMaskInData, 0) : 0;
            var parameters = CodecParameters(dictionary.Get(KnownName.Filter), dictionary.Get(KnownName.DecodeParms));
            var report = new ImageDecodeReport(dictionary.Owner?.Context, image.Id.Number, SelectReduction(role, levels.Codec), scratchPools);
            var result = DecodeData(header, codec, buffer.WrittenSpan, parameters, softMaskInData, report, cancellationToken);
            return role == ImageRole.Image && result is { IsStencilMask: false, UnsupportedCodec: PdfImageCodec.None }
                ? ApplyMasks(result, dictionary, header.ColorSpace, colorSpaces, report, colors.Substitutes, cancellationToken)
                : result;
        }
        catch (Exception e) when (e is InvalidDataException or ArgumentException)
        {
            // An out-of-memory error is not a verdict on the image, so it propagates and the image cache does not remember it.
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

    /// <summary>Decodes an image stream, keeping an opaque gray image as one byte per pixel.</summary>
    /// <param name="image">The image stream.</param>
    /// <param name="resources">The resources a named colour space is looked up in, or <see langword="null"/>.</param>
    /// <returns>The decoded image, or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData? DecodeCompact(PdfStream image, PdfDictionary? resources) =>
        DecodeStream(image, ImageRole.Image, resources?.GetDictionary(KnownName.ColorSpace) ?? resources, true);

    /// <summary>Decodes an image for drawing at a selected scan resolution.</summary>
    /// <param name="image">The image stream.</param>
    /// <param name="reductionLevels">The finest resolution levels omitted.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The decoded image, or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData? DecodeCompact(PdfStream image, int reductionLevels, CancellationToken cancellationToken = default) =>
        DecodeStream(image, ImageRole.Image, null, true, reductionLevels, cancellationToken);

    /// <summary>Decodes a compact image with explicit output-intent colours.</summary>
    /// <param name="image">The image stream.</param>
    /// <param name="reductionLevels">The finest resolution levels omitted.</param>
    /// <param name="colors">The output-intent substitutions, or null.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The decoded image, or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData? DecodeCompact(PdfStream image, int reductionLevels, OutputIntentColors? colors, CancellationToken cancellationToken) =>
        DecodeStream(image, ImageRole.Image, null, true, reductionLevels, colors, cancellationToken);

    /// <summary>Decodes an image for drawing, averaging an opaque colour image down by area while its rows are converted.</summary>
    /// <param name="image">The image stream.</param>
    /// <param name="levels">The levels the codec omits and the halvings to average in, for unmasked images without a colour key; <see cref="PdfImageData.IsBoxReduced"/> reports it.</param>
    /// <param name="colors">The output-intent substitutions and the rendering the image's ICC colours convert with.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The decoded image, or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData? DecodeCompact(PdfStream image, ImageLevels levels, ImageColors colors, CancellationToken cancellationToken) =>
        DecodeStream(image, ImageRole.Image, null, true, levels, colors, cancellationToken);

    /// <summary>Decodes a compact image with explicit output-intent colours and ICC rendering.</summary>
    /// <param name="image">The image stream.</param>
    /// <param name="reductionLevels">The finest resolution levels omitted.</param>
    /// <param name="colors">The output-intent substitutions, or null.</param>
    /// <param name="rendering">The rendering intent and black point choice the image's ICC colours convert with.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The decoded image, or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData? DecodeCompact(PdfStream image, int reductionLevels, OutputIntentColors? colors, IccRendering rendering, CancellationToken cancellationToken) =>
        DecodeStream(image, ImageRole.Image, null, true, reductionLevels, new ImageColors(colors, rendering), cancellationToken);

    /// <summary>
    /// Decodes an image for a caller that can use no more than a given size, such as a thumbnail. JPEG 2000 and JBIG2
    /// scans decode at the smallest resolution that is still at least that size in both directions, which skips the
    /// finest wavelet levels (ISO/IEC 15444-1 annex F) and so costs less time and memory; other images decode in full.
    /// </summary>
    /// <param name="image">The image stream.</param>
    /// <param name="maxWidth">The width in pixels the caller can use.</param>
    /// <param name="maxHeight">The height in pixels the caller can use.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The decoded image, which may be smaller than the stream's pixel size but not smaller than the given size; or null.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData? DecodeFitting(PdfStream image, int maxWidth, int maxHeight, CancellationToken cancellationToken = default) =>
        DecodeCompact(image, ImageReduction.Select(image.Dictionary, maxWidth, maxHeight), cancellationToken);

    /// <summary>Decodes unpacked sample data: stencil coverage or BGRA through the colour space.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The samples, rows padded to whole bytes.</param>
    /// <returns>The image.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData DecodeSamples(in ImageHeader header, ReadOnlySpan<byte> data) =>
        DecodeSamples(header, data, new());

    /// <summary>Decodes samples using an explicitly owned scratch cache.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The padded sample rows.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <returns>The image.</returns>
    internal static PdfImageData DecodeSamples(in ImageHeader header, ReadOnlySpan<byte> data, ScratchPools scratchPools)
    {
        var needed = (long)header.RowBytes * header.Height;
        if (data.Length >= needed)
        {
            return DecodeComplete(header, data, scratchPools);
        }

        // Short data reads as zero samples, as other viewers do.
        var pool = scratchPools.Get<byte>();
        var padded = pool.Rent((int)needed);
        try
        {
            data.CopyTo(padded);
            padded.AsSpan(data.Length, (int)needed - data.Length).Clear();
            return DecodeComplete(header, padded.AsSpan(0, (int)needed), scratchPools);
        }
        finally
        {
            pool.Return(padded);
        }
    }

    /// <summary>
    /// Decodes an image whose rows a codec produces one at a time, so the codec need not hold every sample of the image
    /// beside its pixels. The rows are 8-bit samples in the header's colour space.
    /// </summary>
    /// <typeparam name="TRows">The row source.</typeparam>
    /// <param name="header">The image header.</param>
    /// <param name="rows">The rows.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <returns>The BGRA image.</returns>
    internal static PdfImageData DecodeRows<TRows>(in ImageHeader header, TRows rows, ScratchPools scratchPools)
        where TRows : IImageRows, allows ref struct
    {
        var space = header.ColorSpace ?? PdfColorSpace.DeviceGray;
        var unpacker = new SampleUnpacker(header.BitsPerComponent, space.Components, header.GetDecode(), space, header.ColorKey);
        return (header.Compact ? GrayImages.TryDecodeRows(header, space, unpacker, rows, scratchPools) : null)
            ?? DecodeColor(header, rows, space, unpacker, scratchPools);
    }

    /// <summary>Decodes a stencil mask to coverage.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The 1-bit samples.</param>
    /// <returns>The coverage image.</returns>
    internal static PdfImageData DecodeStencil(in ImageHeader header, ReadOnlySpan<byte> data)
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

    /// <summary>Determines whether an image gets a soft mask or stencil mask stream, which need BGRA pixels to multiply into.</summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <returns><see langword="true"/> when the image has a mask stream.</returns>
    private static bool HasMask(PdfDictionary dictionary) =>
        dictionary.ContainsKey(KnownName.SMask) || dictionary.Get(KnownName.Mask).AsStream() is not null;

    /// <summary>Asks the decoder to average rows while converting them, when the pixels are the image's own opaque colours.</summary>
    /// <param name="header">The header.</param>
    /// <param name="role">The image role.</param>
    /// <param name="codec">The codec still to apply; only raw samples convert row by row at the image's own size.</param>
    /// <param name="dictionary">The image dictionary.</param>
    /// <param name="boxLevels">The halvings requested.</param>
    /// <returns>The header, with <see cref="ImageHeader.BoxLevels"/> set when the image qualifies.</returns>
    private static ImageHeader WithBoxLevels(in ImageHeader header, ImageRole role, PdfImageCodec codec, PdfDictionary dictionary, int boxLevels) =>
        boxLevels > 0 && role == ImageRole.Image && codec == PdfImageCodec.None && header is { ColorKey: null, IsStencil: false } && !HasMask(dictionary)
            ? header with { BoxLevels = boxLevels }
            : header;

    /// <summary>Determines whether the colour decoder averages rows while converting them.</summary>
    /// <param name="header">The header.</param>
    /// <param name="unpacker">The sample unpacker.</param>
    /// <returns><see langword="true"/> when no colour key needs the full-size pixels.</returns>
    private static bool ReducesWhileDecoding(in ImageHeader header, in SampleUnpacker unpacker) => header.BoxLevels > 0 && !unpacker.HasColorKey;

    /// <summary>Adjusts a header for the role the stream is decoded in.</summary>
    /// <param name="header">The header as the dictionary gives it.</param>
    /// <param name="role">The image role.</param>
    /// <param name="compact">Whether gray samples may stay compact.</param>
    /// <param name="dictionary">The image dictionary.</param>
    /// <param name="rendering">The rendering an image's ICC colours convert with; masks have no colours to convert.</param>
    /// <returns>The header to decode with.</returns>
    private static ImageHeader ForRole(in ImageHeader header, ImageRole role, bool compact, PdfDictionary dictionary, IccRendering rendering) => role switch
    {
        ImageRole.SoftMask => header with { IsStencil = false, ColorSpace = PdfColorSpace.DeviceGray, ColorKey = null, Compact = true },
        ImageRole.StencilMask => header with { IsStencil = true, BitsPerComponent = OneBit, ColorSpace = null, ColorKey = null },
        _ => header with { Compact = compact && !HasMask(dictionary), ColorSpace = header.ColorSpace?.ForRendering(rendering) },
    };

    /// <summary>Uses the requested resolution for images and soft masks.</summary>
    /// <param name="role">The image role.</param>
    /// <param name="requested">The requested reduction.</param>
    /// <returns>The safe reduction level.</returns>
    private static int SelectReduction(ImageRole role, int requested) =>
        (role is ImageRole.Image or ImageRole.SoftMask or ImageRole.StencilMask) && requested > 0 ? requested : 0;

    /// <summary>Decodes data after the byte filters, by codec, reporting damage to a context.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="codec">The image codec still to apply.</param>
    /// <param name="data">The data.</param>
    /// <param name="codecParameters">The codec's decode parameters, or <see langword="null"/>.</param>
    /// <param name="softMaskInData">The image's /SMaskInData value; 0 for masks and inline images.</param>
    /// <param name="report">The damage-report context and display resolution.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The image, or <see langword="null"/>.</returns>
    private static PdfImageData? DecodeData(
        in ImageHeader header,
        PdfImageCodec codec,
        ReadOnlySpan<byte> data,
        PdfDictionary? codecParameters,
        int softMaskInData,
        ImageDecodeReport report,
        CancellationToken cancellationToken) => codec switch
    {
        PdfImageCodec.None => IsTooShort(header, data) ? null : DecodeSamples(header, data, report.ScratchPools),
        PdfImageCodec.Jpeg => JpegImageDecoder.Decode(
            header,
            data,
            ColorTransform(codecParameters),
            report.Context,
            report.ObjectNumber,
            report.Context?.DrawingServices.Images,
            cancellationToken),
        PdfImageCodec.Jpeg2000 => JpxImageDecoder.Decode(header, data, softMaskInData, report.ReductionLevels, report.ScratchPools, cancellationToken),
        PdfImageCodec.Ccitt => DecodeFax(header, data, codecParameters, report.ScratchPools),
        PdfImageCodec.Jbig2 => Jbig2ImageDecoder.Decode(header, data, codecParameters, report.ReductionLevels, report.ScratchPools, cancellationToken),
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
    /// <param name="scratchPools">The bounded cache.</param>
    /// <returns>The image.</returns>
    private static PdfImageData DecodeFax(in ImageHeader header, ReadOnlySpan<byte> data, PdfDictionary? parameters, ScratchPools scratchPools)
    {
        var fax = header with { BitsPerComponent = OneBit };
        if (fax.ColorSpace?.Components > 1)
        {
            fax = fax with { ColorSpace = PdfColorSpace.DeviceGray, Decode = null };
        }

        var length = fax.RowBytes * fax.Height;
        var pool = scratchPools.Get<byte>();
        var rows = pool.Rent(length);
        try
        {
            var output = rows.AsSpan(0, length);
            CcittFaxDecoder.Decode(data, CcittParameters.FromDictionary(parameters), fax.Width, fax.Height, output, scratchPools);
            return DecodeSamples(fax, output, scratchPools);
        }
        finally
        {
            pool.Return(rows);
        }
    }

    /// <summary>Decodes data holding every row.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The samples.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <returns>The image.</returns>
    private static PdfImageData DecodeComplete(in ImageHeader header, ReadOnlySpan<byte> data, ScratchPools scratchPools)
    {
        if (header.IsStencil)
        {
            return DecodeStencil(header, data);
        }

        var space = header.ColorSpace ?? PdfColorSpace.DeviceGray;
        var unpacker = new SampleUnpacker(header.BitsPerComponent, space.Components, header.GetDecode(), space, header.ColorKey);
        return (header.Compact ? GrayImages.TryDecode(header, space, unpacker, data, scratchPools) : null)
            ?? DecodeColor(header, new SpanImageRows(data, header.RowBytes), space, unpacker, scratchPools);
    }

    /// <summary>Decodes rows of samples to BGRA through the colour space.</summary>
    /// <typeparam name="TRows">The row source.</typeparam>
    /// <param name="header">The image header.</param>
    /// <param name="rows">The rows.</param>
    /// <param name="space">The colour space.</param>
    /// <param name="unpacker">Unpacks the samples.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <returns>The image.</returns>
    private static PdfImageData DecodeColor<TRows>(in ImageHeader header, TRows rows, PdfColorSpace space, in SampleUnpacker unpacker, ScratchPools scratchPools)
        where TRows : IImageRows, allows ref struct =>
        ReducesWhileDecoding(header, unpacker)
            ? ReducedColorDecoder.Decode(header, rows, space, unpacker, scratchPools)
            : DecodeFullColor(header, rows, space, unpacker, scratchPools);

    /// <summary>Decodes rows of samples to full-size BGRA through the colour space.</summary>
    /// <typeparam name="TRows">The row source.</typeparam>
    /// <param name="header">The image header.</param>
    /// <param name="rows">The rows.</param>
    /// <param name="space">The colour space.</param>
    /// <param name="unpacker">Unpacks the samples.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <returns>The image.</returns>
    private static PdfImageData DecodeFullColor<TRows>(in ImageHeader header, TRows rows, PdfColorSpace space, in SampleUnpacker unpacker, ScratchPools scratchPools)
        where TRows : IImageRows, allows ref struct
    {
        var width = header.Width;
        var pixels = PixelMemory.Allocate((long)width * header.Height * PixelConverter.BytesPerPixel, out var pinned);
        var pool = scratchPools.Get<byte>();
        var samples = pool.Rent(width * space.Components);
        var rowBuffer = rows.UsesBuffer ? pool.Rent(header.RowBytes) : null;
        var alpha = unpacker.HasColorKey ? pool.Rent(width * header.Height) : null;
        try
        {
            for (var y = 0; y < header.Height; y++)
            {
                var row = rows.GetRow(y, rowBuffer is null ? [] : rowBuffer.AsSpan(0, header.RowBytes));
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
            pool.Return(samples);
            if (rowBuffer is not null)
            {
                pool.Return(rowBuffer);
            }

            if (alpha is not null)
            {
                pool.Return(alpha);
            }
        }
    }

    /// <summary>Applies the image's /SMask, or else its stencil /Mask stream.</summary>
    /// <param name="image">The decoded image.</param>
    /// <param name="dictionary">The image dictionary.</param>
    /// <param name="space">The image's colour space, used to convert a soft mask's /Matte.</param>
    /// <param name="colorSpaces">The /ColorSpace resource dictionary, or <see langword="null"/>.</param>
    /// <param name="report">The display resolution and scratch cache.</param>
    /// <param name="colors">The output-intent substitutions, or null.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The image with the mask applied; the input when there is no usable mask.</returns>
    private static PdfImageData ApplyMasks(
        PdfImageData image,
        PdfDictionary dictionary,
        PdfColorSpace? space,
        PdfDictionary? colorSpaces,
        ImageDecodeReport report,
        OutputIntentColors? colors,
        CancellationToken cancellationToken)
    {
        var softMask = dictionary.GetStream(KnownName.SMask);
        var mask = softMask is null ? dictionary.Get(KnownName.Mask).AsStream() : null;
        var decoded = softMask is not null
            ? DecodeStream(softMask, ImageRole.SoftMask, colorSpaces, true, report.ReductionLevels, colors, cancellationToken)
            : DecodeMask(mask, colorSpaces, report.ReductionLevels, colors, cancellationToken);
        if (decoded is null || decoded.Pixels.Length == 0)
        {
            return image;
        }

        var matte = softMask is null ? null : ImageMasks.ReadMatte(softMask.Dictionary, space);
        return ImageMasks.Apply(image, decoded, matte, report.ScratchPools);
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
    /// <param name="colors">The output-intent substitutions, or null.</param>
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The coverage, or <see langword="null"/>.</returns>
    private static PdfImageData? DecodeMask(PdfStream? mask, PdfDictionary? colorSpaces, int reductionLevels, OutputIntentColors? colors, CancellationToken cancellationToken) =>
        mask is null ? null : DecodeStream(mask, ImageRole.StencilMask, colorSpaces, false, reductionLevels, colors, cancellationToken);

    /// <summary>Gets the decode parameters of the last filter, the one an image codec must be.</summary>
    /// <param name="filters">The /Filter value.</param>
    /// <param name="parameters">The /DecodeParms value.</param>
    /// <returns>The parameters, or <see langword="null"/>.</returns>
    private static PdfDictionary? CodecParameters(PdfValue filters, PdfValue parameters)
    {
        var count = filters.AsArray()?.Count ?? 1;
        return parameters.AsArray()?.GetDictionary(count - 1) ?? parameters.AsDictionary();
    }

    /// <summary>Gets the dictionary's document cache, or a cache owned by this standalone decode.</summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <returns>The cache.</returns>
    private static ScratchPools ScratchFor(PdfDictionary dictionary) => dictionary.Owner?.ScratchPools ?? new();

    /// <summary>Context attached to a decoded image for damage reports and display resolution.</summary>
    /// <param name="Context">The PDF open context.</param>
    /// <param name="ObjectNumber">The image object number.</param>
    /// <param name="ReductionLevels">The finest levels omitted.</param>
    /// <param name="ScratchPools">The bounded cache.</param>
    private readonly record struct ImageDecodeReport(PdfOpenContext? Context, int ObjectNumber, int ReductionLevels, ScratchPools ScratchPools);
}
