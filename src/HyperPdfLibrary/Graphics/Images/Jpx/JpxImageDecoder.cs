// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Decodes JPXDecode (JPEG 2000) images the way PDFium does. The managed decoder returns the component planes; the
/// channels become 8-bit samples that go through the image's colour space like any 8-bit image. The image dictionary's
/// /ColorSpace overrides the file's colour box, an Indexed space reads the raw palette indices, and /Decode is ignored as
/// the PDF specification says. With <c>/SMaskInData 1</c> the fourth channel of an RGB image is its soft mask.
/// High-throughput (T.814 | ISO 15444-15) code-blocks decode like regular ones; only the MIXED mode, where each
/// code-block may use either coder, is reported as unsupported, as PDFium refuses it. Damaged data gives
/// <see langword="null"/>.
/// </summary>
internal static class JpxImageDecoder
{
    /// <summary>The bits of an output sample.</summary>
    private const int SampleBits = 8;

    /// <summary>The <c>/SMaskInData</c> value that makes the alpha channel the soft mask.</summary>
    private const int SoftMaskInData = 1;

    /// <summary>The channel that holds alpha in a four-channel RGB image.</summary>
    private const int AlphaChannel = 3;

    /// <summary>Decodes a JPXDecode image.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The JPX data: a JP2 file or a raw codestream.</param>
    /// <param name="softMaskInData">The <c>/SMaskInData</c> entry, or zero.</param>
    /// <returns>The image, <see langword="null"/> when damaged or refused, or an image marked unsupported.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData? Decode(in ImageHeader header, ReadOnlySpan<byte> data, int softMaskInData) =>
        Decode(header, data, softMaskInData, 0);

    /// <summary>Decodes a JPX image at a lower native wavelet resolution when the codestream allows it.</summary>
    /// <param name="header">The PDF image header.</param>
    /// <param name="data">The JPX file or codestream.</param>
    /// <param name="softMaskInData">The alpha channel marker.</param>
    /// <param name="reductionLevels">The finest wavelet levels to omit.</param>
    /// <returns>The decoded image, or null when damaged.</returns>
    internal static PdfImageData? Decode(in ImageHeader header, ReadOnlySpan<byte> data, int softMaskInData, int reductionLevels)
    {
        try
        {
            return DecodeFile(header, data, softMaskInData, reductionLevels);
        }
        catch (Exception exception) when (exception is IndexOutOfRangeException or ArgumentException or OverflowException or InvalidCastException)
        {
            // The decoder checks what it reads; a slip past those checks on damaged data must not break the page.
            return null;
        }
    }

    /// <summary>Creates the result for a JPX image the decoder does not support.</summary>
    /// <param name="header">The image header.</param>
    /// <returns>An image with no pixels that names the JPEG 2000 codec.</returns>
    private static PdfImageData Unsupported(in ImageHeader header) =>
        new(header.Width, header.Height, [], header.IsStencil, header.Interpolate, PdfImageCodec.Jpeg2000);

    /// <summary>Determines whether a codestream's main header asks for mixed HT and regular code-blocks.</summary>
    /// <param name="codestream">The codestream.</param>
    /// <returns><see langword="true"/> when any component does.</returns>
    private static bool UsesMixedBlocks(JpxCodestream codestream)
    {
        if ((codestream.Main.Style!.BlockStyle & JpxBlockStyle.HighThroughputMixed) != 0)
        {
            return true;
        }

        foreach (var style in codestream.Main.ComponentStyles)
        {
            if (style is not null && (style.BlockStyle & JpxBlockStyle.HighThroughputMixed) != 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Reads the file, decodes the codestream and converts the channels.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The JPX data.</param>
    /// <param name="softMaskInData">The <c>/SMaskInData</c> entry.</param>
    /// <param name="reductionLevels">The requested reduction.</param>
    /// <returns>The image, <see langword="null"/>, or an image marked unsupported.</returns>
    private static PdfImageData? DecodeFile(in ImageHeader header, ReadOnlySpan<byte> data, int softMaskInData, int reductionLevels)
    {
        if (JpxFileFormat.Read(data) is not { } file)
        {
            return null;
        }

        var stream = data.Slice(file.Codestream.Offset, file.Codestream.Length);
        if (JpxCodestream.Read(stream) is not { } codestream)
        {
            return null;
        }

        if (UsesMixedBlocks(codestream))
        {
            return Unsupported(header);
        }

        var selectedReduction = SelectReduction(codestream, stream, reductionLevels);
        using var image = JpxDecoder.Decode(codestream, stream, selectedReduction);
        var indexed = header.ColorSpace?.Kind == PdfColorSpaceKind.Indexed;
        if (JpxChannels.Build(file, codestream.Geometry, indexed) is not { Length: > 0 } channels
            || JpxOutputPlan.Create(file.ColorSpace, channels, image, header.ColorSpace) is not { } plan)
        {
            return null;
        }

        var area = image.Areas[channels[0].Component];
        if (area.Width < (header.Width >> selectedReduction) || area.Height < (header.Height >> selectedReduction))
        {
            // PDFium refuses a JPX image smaller than its dictionary says.
            return null;
        }

        var source = new JpxSampleSource(image, channels, file.Palette, area.Width, area.Height);
        var alpha = plan.Action == JpxDecodeAction.ConvertArgbToRgb && softMaskInData == SoftMaskInData;
        return Convert(header, source, plan, alpha);
    }

    /// <summary>Uses only levels available to every component and avoids tile-specific coding overrides.</summary>
    /// <param name="codestream">The parsed codestream.</param>
    /// <param name="data">The codestream bytes.</param>
    /// <param name="requested">The requested reduction.</param>
    /// <returns>The usable number of finest levels to omit.</returns>
    private static int SelectReduction(JpxCodestream codestream, ReadOnlySpan<byte> data, int requested)
    {
        if (requested <= 0 || codestream.Main.Style is not { } style)
        {
            return 0;
        }

        var levels = Math.Min(requested, style.Levels);
        foreach (var component in codestream.Main.ComponentStyles)
        {
            if (component is { } overrideStyle)
            {
                levels = Math.Min(levels, overrideStyle.Levels);
            }
        }

        return levels > 0 && !HasTileStyleOverrides(codestream, data) ? levels : 0;
    }

    /// <summary>Finds coding-style changes that prevent one resolution choice for the whole image.</summary>
    /// <param name="codestream">The parsed codestream.</param>
    /// <param name="data">The codestream bytes.</param>
    /// <returns>True when tile styles differ or a header is damaged.</returns>
    private static bool HasTileStyleOverrides(JpxCodestream codestream, ReadOnlySpan<byte> data)
    {
        foreach (var part in codestream.TileParts)
        {
            var state = new JpxHeaderState(codestream.Geometry.Components.Length);
            if (!JpxCodestream.ReadTileHeader(data, part.Header, state) || state.Style is not null)
            {
                return true;
            }

            foreach (var style in state.ComponentStyles)
            {
                if (style is not null)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Writes the samples and runs them through the colour space, applying any soft mask in the data.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="source">The decoded channels.</param>
    /// <param name="plan">The output plan.</param>
    /// <param name="withAlpha">Whether the fourth channel is the soft mask.</param>
    /// <returns>The image.</returns>
    private static PdfImageData Convert(in ImageHeader header, in JpxSampleSource source, JpxOutputPlan plan, bool withAlpha)
    {
        var pixels = source.Width * source.Height;
        var length = pixels * plan.Components;
        var samples = ScratchPool<byte>.Shared.Rent(length);
        var alpha = withAlpha ? ScratchPool<byte>.Shared.Rent(pixels) : null;
        try
        {
            var written = samples.AsSpan(0, length);
            JpxSampleWriter.Write(source, plan, written);
            if (plan.Action == JpxDecodeAction.UseIndexed && header.BitsPerComponent < SampleBits)
            {
                ShiftIndices(written, SampleBits - header.BitsPerComponent);
            }

            if (alpha is not null)
            {
                JpxSampleWriter.WriteAlpha(source, AlphaChannel, alpha.AsSpan(0, pixels));
                JpxSampleWriter.BlendToWhite(written, alpha.AsSpan(0, pixels));
            }

            // A soft mask in the data multiplies into BGRA pixels, so those images are never gray.
            var image = PdfImageDecoder.DecodeSamples(ForSamples(header, source, plan) with { Compact = header.Compact && alpha is null }, written);
            if (alpha is not null)
            {
                ImageAlpha.Multiply(image.Pixels, alpha.AsSpan(0, pixels));
            }

            return image;
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

    /// <summary>Shifts palette indices back down after they were scaled up to eight bits, as PDFium does.</summary>
    /// <param name="samples">The indices.</param>
    /// <param name="shift">The shift.</param>
    private static void ShiftIndices(Span<byte> samples, int shift)
    {
        foreach (ref var sample in samples)
        {
            sample >>= shift;
        }
    }

    /// <summary>Gets the header the 8-bit samples decode with.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="source">The decoded channels.</param>
    /// <param name="plan">The output plan.</param>
    /// <returns>The header for <see cref="PdfImageDecoder.DecodeSamples"/>.</returns>
    private static ImageHeader ForSamples(in ImageHeader header, in JpxSampleSource source, JpxOutputPlan plan) => header with
    {
        Width = source.Width,
        Height = source.Height,
        BitsPerComponent = SampleBits,
        IsStencil = false,
        ColorSpace = plan.ColorSpace,
        Decode = null,
        ColorKey = plan.KeepsDictionarySpace ? header.ColorKey : null,
    };
}
