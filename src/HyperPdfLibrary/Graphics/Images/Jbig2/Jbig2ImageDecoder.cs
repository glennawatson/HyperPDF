// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>
/// Decodes a JBIG2Decode image for <see cref="PdfImageDecoder"/>: the page becomes 1-bit samples, 0 for black, which
/// then go through the image's colour space and /Decode array like any 1-bit image, or become a stencil mask.
/// </summary>
internal static class Jbig2ImageDecoder
{
    /// <summary>The bits of a JBIG2 sample.</summary>
    private const int OneBit = 1;

    /// <summary>The bits of an averaged gray sample.</summary>
    private const int GrayBits = 8;

    /// <summary>The most binary downsampling levels used for display.</summary>
    private const int MaximumReduction = 4;

    /// <summary>Decodes a JBIG2 image.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The JBIG2 data after the stream's other filters.</param>
    /// <param name="parameters">The JBIG2Decode parameters, or <see langword="null"/>; /JBIG2Globals is read from them.</param>
    /// <returns>The image, or <see langword="null"/> when no page could be decoded.</returns>
    /// <exception cref="InvalidDataException">The /JBIG2Globals stream's filters fail.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData? Decode(in ImageHeader header, ReadOnlySpan<byte> data, PdfDictionary? parameters) =>
        Decode(header, data, parameters, 0);

    /// <summary>Decodes a JBIG2 page and averages its packed pixels to a smaller display image.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The JBIG2 data.</param>
    /// <param name="parameters">The codec parameters.</param>
    /// <param name="reductionLevels">The number of halvings requested.</param>
    /// <returns>The decoded display image, or null when decoding fails.</returns>
    /// <exception cref="InvalidDataException">The /JBIG2Globals stream's filters fail.</exception>
    internal static PdfImageData? Decode(in ImageHeader header, ReadOnlySpan<byte> data, PdfDictionary? parameters, int reductionLevels)
    {
        var bits = header with { BitsPerComponent = OneBit };
        if (bits.ColorSpace?.Components > 1)
        {
            bits = bits with { ColorSpace = PdfColorSpace.DeviceGray, Decode = null };
        }

        var length = bits.RowBytes * bits.Height;
        var rows = ScratchPool<byte>.Shared.Rent(length);
        var globals = default(PooledBuffer);
        try
        {
            _ = parameters?.GetStream(KnownName.JBIG2Globals)?.Decode(ref globals);
            var output = rows.AsSpan(0, length);
            if (!Jbig2Decoder.TryDecode(data, globals.WrittenSpan, bits.Width, bits.Height, output))
            {
                return null;
            }

            var selected = bits.ColorKey is not null ? 0 : Math.Clamp(reductionLevels, 0, MaximumReduction);
            if (selected == 0)
            {
                return PdfImageDecoder.DecodeSamples(bits, output);
            }

            return bits.IsStencil ? DecodeReducedStencil(bits, output, selected) : DecodeReduced(bits, output, selected);
        }
        finally
        {
            globals.Dispose();
            ScratchPool<byte>.Shared.Return(rows);
        }
    }

    /// <summary>Averages each square of packed source pixels to one eight-bit gray sample.</summary>
    /// <param name="header">The one-bit source header.</param>
    /// <param name="rows">The decoded packed rows.</param>
    /// <param name="levels">The number of halvings.</param>
    /// <returns>The reduced gray image.</returns>
    private static PdfImageData DecodeReduced(in ImageHeader header, ReadOnlySpan<byte> rows, int levels)
    {
        if (header.Compact && ReferenceEquals(header.ColorSpace, PdfColorSpace.DeviceGray) && TrySimpleDecode(header.Decode, out var inverted))
        {
            return DecodeReducedCompact(header, rows, levels, inverted);
        }

        var step = 1 << levels;
        var width = (header.Width + step - 1) / step;
        var height = (header.Height + step - 1) / step;
        var samples = ScratchPool<byte>.Shared.Rent(width * height);
        try
        {
            Jbig2Downsampler.Average(rows, header.Width, header.Height, header.RowBytes, levels, samples);
            return PdfImageDecoder.DecodeSamples(header with { Width = width, Height = height, BitsPerComponent = GrayBits }, samples.AsSpan(0, width * height));
        }
        finally
        {
            ScratchPool<byte>.Shared.Return(samples);
        }
    }

    /// <summary>Writes a reduced DeviceGray scan directly into its retained pixel buffer.</summary>
    /// <param name="header">The source image header.</param>
    /// <param name="rows">The packed source rows.</param>
    /// <param name="levels">The number of halvings.</param>
    /// <param name="inverted">Whether /Decode reverses black and white.</param>
    /// <returns>The retained compact gray image.</returns>
    private static PdfImageData DecodeReducedCompact(in ImageHeader header, ReadOnlySpan<byte> rows, int levels, bool inverted)
    {
        var step = 1 << levels;
        var width = (header.Width + step - 1) / step;
        var height = (header.Height + step - 1) / step;
        var pixels = PixelMemory.Allocate((long)width * height, out var pinned);
        Jbig2Downsampler.Average(rows, header.Width, header.Height, header.RowBytes, levels, pixels);
        if (inverted)
        {
            Invert(pixels);
        }

        return new(width, height, pixels, false, header.Interpolate, PdfImageCodec.None) { IsGray = true, IsPinned = pinned };
    }

    /// <summary>Builds reduced coverage for a stencil mask paired with a reduced colour scan.</summary>
    /// <param name="header">The one-bit stencil header.</param>
    /// <param name="rows">The decoded packed rows.</param>
    /// <param name="levels">The number of halvings.</param>
    /// <returns>The reduced coverage image.</returns>
    private static PdfImageData DecodeReducedStencil(in ImageHeader header, ReadOnlySpan<byte> rows, int levels)
    {
        var step = 1 << levels;
        var width = (header.Width + step - 1) / step;
        var height = (header.Height + step - 1) / step;
        var coverage = PixelMemory.Allocate((long)width * height, out var pinned);
        Jbig2Downsampler.Average(rows, header.Width, header.Height, header.RowBytes, levels, coverage);
        var decode = header.GetDecode();
        if (decode[0] <= decode[1])
        {
            Invert(coverage);
        }

        return new(width, height, coverage, true, header.Interpolate, PdfImageCodec.None) { IsPinned = pinned };
    }

    /// <summary>Accepts only the DeviceGray decode ranges whose output can be written directly.</summary>
    /// <param name="decode">The declared decode range, or null for its default.</param>
    /// <param name="inverted">Receives whether the range reverses gray.</param>
    /// <returns>Whether a direct gray output is equivalent to sample conversion.</returns>
    private static bool TrySimpleDecode(float[]? decode, out bool inverted)
    {
        inverted = decode is [1F, 0F];
        return decode is null or [0F, 1F] || inverted;
    }

    /// <summary>Reverses the coverage or gray values in place.</summary>
    /// <param name="pixels">The samples to invert.</param>
    private static void Invert(Span<byte> pixels)
    {
        for (var i = 0; i < pixels.Length; i++)
        {
            pixels[i] = (byte)(byte.MaxValue - pixels[i]);
        }
    }
}
