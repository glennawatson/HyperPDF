// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Decodes JPXDecode (JPEG 2000) images (ISO 32000-2 section 7.4.9). The managed decoder returns the component planes;
/// the channels become 8-bit samples that go through the image's colour space like any 8-bit image. The image
/// dictionary's /ColorSpace overrides the file's colour boxes (see <see cref="JpxOutputPlan"/>) and then its /Decode
/// applies; without /ColorSpace /Decode is ignored. An Indexed space reads the raw palette indices. With
/// <c>/SMaskInData 1</c> or <c>2</c> the opacity channel is the soft mask (for 2 the colours are divided by it first), and
/// an image mask reads the first channel as its 1-bit samples. High-throughput (T.814 | ISO 15444-15) code-blocks decode like
/// regular ones, including the MIXED mode, where each code-block may use either coder. Damaged data gives
/// <see langword="null"/>.
/// </summary>
internal static class JpxImageDecoder
{
    /// <summary>The bits of an output sample.</summary>
    private const int SampleBits = 8;

    /// <summary>The <c>/SMaskInData</c> value that makes the alpha channel the soft mask.</summary>
    private const int SoftMaskInData = 1;

    /// <summary>The <c>/SMaskInData</c> value for colour channels that are premultiplied by the alpha channel.</summary>
    private const int PremultipliedSoftMask = 2;

    /// <summary>The first 8-bit sample that is a one bit in an image mask.</summary>
    private const int HalfScale = 128;

    /// <summary>The most significant bit of a byte.</summary>
    private const int HighBit = 0x80;

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
    /// <param name="cancellationToken">Cancels this operation.</param>
    /// <returns>The decoded image, or null when damaged.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfImageData? Decode(in ImageHeader header, ReadOnlySpan<byte> data, int softMaskInData, int reductionLevels, CancellationToken cancellationToken = default) =>
        Decode(header, data, softMaskInData, reductionLevels, new(), cancellationToken);

    /// <summary>Decodes a JPX image with an explicitly owned scratch cache.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The JPX bytes.</param>
    /// <param name="softMaskInData">The alpha channel marker.</param>
    /// <param name="reductionLevels">The omitted finest levels.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The image, or null when damaged.</returns>
    internal static PdfImageData? Decode(in ImageHeader header, ReadOnlySpan<byte> data, int softMaskInData, int reductionLevels, ScratchPools scratchPools, CancellationToken cancellationToken)
    {
        try
        {
            return DecodeFile(header, data, softMaskInData, reductionLevels, scratchPools, cancellationToken);
        }
        catch (Exception exception) when (exception is IndexOutOfRangeException or ArgumentException or OverflowException or InvalidCastException)
        {
            // The decoder checks what it reads; a slip past those checks on damaged data must not break the page.
            return null;
        }
    }

    /// <summary>Reads the file, decodes the codestream and converts the channels.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The JPX data.</param>
    /// <param name="softMaskInData">The <c>/SMaskInData</c> entry.</param>
    /// <param name="reductionLevels">The requested reduction.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <param name="cancellationToken">Cancels codestream decoding.</param>
    /// <returns>The image, <see langword="null"/>, or an image marked unsupported.</returns>
    private static PdfImageData? DecodeFile(in ImageHeader header, ReadOnlySpan<byte> data, int softMaskInData, int reductionLevels, ScratchPools scratchPools, CancellationToken cancellationToken)
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

        if (Plan(header, data, file, codestream.Geometry) is not { } planned)
        {
            return null;
        }

        var (set, plan) = planned;

        // Only the components the output reads are decoded: a codestream with more components than colour channels, such
        // as an opacity component the dictionary does not ask for, skips them.
        var alpha = header.IsStencil ? -1 : OpacityChannel(set, plan, softMaskInData);
        var selectedReduction = SelectReduction(codestream, stream, reductionLevels);
        using var image = JpxDecoder.Open(codestream, selectedReduction, scratchPools, WantedComponents(set, plan, alpha), cancellationToken);

        // The output takes the size of the largest channel plane, so subsampled channels stretch to the full image and
        // a codestream whose size differs from the dictionary's still draws, scaled to the image's unit square. The
        // decoder works on rows as the conversion asks for them, so no plane holds the whole image.
        var area = LargestArea(image, set.Channels);

        // The codestream, not the dictionary, sets the output size, so a small file may not claim a huge image.
        if ((long)area.Width * area.Height * PixelConverter.BytesPerPixel > ImageHeader.MaxDecodedBytes)
        {
            return null;
        }

        var source = new JpxSampleSource(image, stream, set.Channels, file.Palette, area.Width, area.Height);
        return header.IsStencil
            ? ConvertStencil(header, source, plan, scratchPools)
            : Convert(header, source, plan, new(alpha, softMaskInData == PremultipliedSoftMask), scratchPools);
    }

    /// <summary>Builds the channels and decides how they become samples.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="data">The JPX data.</param>
    /// <param name="file">The JP2 boxes.</param>
    /// <param name="geometry">The codestream geometry.</param>
    /// <returns>The channels and plan, or <see langword="null"/> when the file has none or too few for the colour space.</returns>
    private static ChannelPlan? Plan(in ImageHeader header, ReadOnlySpan<byte> data, JpxFileInfo file, JpxGeometry geometry)
    {
        var indexed = header.ColorSpace?.Kind == PdfColorSpaceKind.Indexed;
        if (JpxChannels.Build(file, geometry, indexed) is not { Channels.Length: > 0 } set)
        {
            return null;
        }

        // An image mask reads the first channel as 1-bit samples (ISO 32000-2 section 7.4.9); the dictionary has no colour space then.
        if (header.IsStencil)
        {
            return new(set, new(PdfColorSpace.DeviceGray, 1, JpxSampleMode.Scaled, false, false));
        }

        var profileSpace = header.ColorSpace is null && file.Profile.Length > 0 ? JpxProfileSpace.Create(data.Slice(file.Profile.Offset, file.Profile.Length)) : null;

        // CIELab is the one enumerated space that is neither a device space nor a conversion to RGB, so it takes the place of a profile that cannot be used.
        if (profileSpace is null && header.ColorSpace is null && file.ColorSpace == JpxColorSpace.Lab)
        {
            profileSpace = JpxLabSpace.Create(file.Lab, set.Channels);
        }

        return JpxOutputPlan.Create(file.ColorSpace, set, geometry, header.ColorSpace, profileSpace) is { } plan ? new(set, plan) : null;
    }

    /// <summary>Finds the channel that carries the soft mask in the data (<c>/SMaskInData</c> 1 or 2).</summary>
    /// <param name="set">The channels.</param>
    /// <param name="plan">The output plan.</param>
    /// <param name="softMaskInData">The <c>/SMaskInData</c> entry.</param>
    /// <returns>The channel index, or -1 when the data has no soft mask or the dictionary does not ask for it.</returns>
    private static int OpacityChannel(in JpxChannelSet set, JpxOutputPlan plan, int softMaskInData)
    {
        if (softMaskInData is not (SoftMaskInData or PremultipliedSoftMask) || plan.Mode != JpxSampleMode.Scaled)
        {
            return -1;
        }

        // A channel definition box names the opacity channel; without one the channel after the colour channels is taken as it.
        var channel = set.Opacity switch
        {
            JpxChannelSet.NoOpacity => -1,
            JpxChannelSet.Undeclared => plan.Components,
            _ => set.Opacity,
        };
        return channel >= 0 && channel < set.Channels.Length ? channel : -1;
    }

    /// <summary>Finds the components the output reads: the planned colour channels, and the opacity channel when it is used.</summary>
    /// <param name="set">The channels.</param>
    /// <param name="plan">The output plan.</param>
    /// <param name="alpha">The index of the soft mask channel, or -1.</param>
    /// <returns>The components to decode.</returns>
    private static uint WantedComponents(in JpxChannelSet set, JpxOutputPlan plan, int alpha)
    {
        var wanted = 0U;
        for (var i = 0; i < plan.Components; i++)
        {
            wanted = JpxComponentMask.With(wanted, set.Channels[i].Component);
        }

        return alpha < 0 ? wanted : JpxComponentMask.With(wanted, set.Channels[alpha].Component);
    }

    /// <summary>Finds the channel plane with the most samples.</summary>
    /// <param name="image">The decoded planes.</param>
    /// <param name="channels">The channels.</param>
    /// <returns>The largest area.</returns>
    private static JpxRectangle LargestArea(JpxDecoder image, JpxChannel[] channels)
    {
        var largest = image.Areas[channels[0].Component];
        foreach (var channel in channels)
        {
            var area = image.Areas[channel.Component];
            if ((long)area.Width * area.Height > (long)largest.Width * largest.Height)
            {
                largest = area;
            }
        }

        return largest;
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
    /// <param name="softMask">The soft mask in the data, if the dictionary asks for it.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <returns>The image.</returns>
    private static PdfImageData Convert(in ImageHeader header, in JpxSampleSource source, JpxOutputPlan plan, SoftMask softMask, ScratchPools scratchPools)
    {
        // Each row goes from the decoder straight through the colour space, so the whole image is never held as one
        // sample buffer as well.
        if (softMask.Channel < 0)
        {
            return PdfImageDecoder.DecodeRows(ForSamples(header, source, plan), new JpxImageRows(source, plan), scratchPools);
        }

        // A soft mask in the data multiplies into BGRA pixels, so those images are never gray. Only the alpha channel is
        // kept beside the pixels.
        var pixels = source.Width * source.Height;
        var pool = scratchPools.Get<byte>();
        var alpha = pool.Rent(pixels);
        try
        {
            var rows = new JpxImageRows(source, plan, alpha.AsSpan(0, pixels), softMask.Channel, softMask.Premultiplied);
            var image = PdfImageDecoder.DecodeRows(ForSamples(header, source, plan) with { Compact = false }, rows, scratchPools);
            ImageAlpha.Multiply(image.Pixels, alpha.AsSpan(0, pixels));
            return image;
        }
        finally
        {
            pool.Return(alpha);
        }
    }

    /// <summary>Reads the first channel as the 1-bit samples of an image mask and decodes it to coverage.</summary>
    /// <param name="header">The image header, of an image mask.</param>
    /// <param name="source">The decoded channels.</param>
    /// <param name="plan">The output plan, for the first channel as grey.</param>
    /// <param name="scratchPools">The bounded cache.</param>
    /// <returns>The coverage image.</returns>
    private static PdfImageData ConvertStencil(in ImageHeader header, in JpxSampleSource source, JpxOutputPlan plan, ScratchPools scratchPools)
    {
        var mask = header with { Width = source.Width, Height = source.Height, BitsPerComponent = 1 };
        var length = mask.RowBytes * mask.Height;
        var bytes = scratchPools.Get<byte>();
        var rows = bytes.Rent(length);
        var samples = bytes.Rent(source.Width);
        try
        {
            var packed = rows.AsSpan(0, length);
            packed.Clear();
            for (var y = 0; y < source.Height; y++)
            {
                JpxSampleWriter.WritePlanRow(source, plan, y, samples.AsSpan(0, source.Width));
                PackRow(samples.AsSpan(0, source.Width), packed.Slice(y * mask.RowBytes, mask.RowBytes));
            }

            return PdfImageDecoder.DecodeStencil(mask, packed);
        }
        finally
        {
            bytes.Return(samples);
            bytes.Return(rows);
        }
    }

    /// <summary>Packs 8-bit samples to bits, most significant first: a sample of at least half scale is a one.</summary>
    /// <param name="samples">The samples of a row.</param>
    /// <param name="bits">The packed row, cleared.</param>
    private static void PackRow(ReadOnlySpan<byte> samples, Span<byte> bits)
    {
        for (var x = 0; x < samples.Length; x++)
        {
            if (samples[x] >= HalfScale)
            {
                bits[x / SampleBits] |= (byte)(HighBit >> (x % SampleBits));
            }
        }
    }

    /// <summary>Gets the header the 8-bit samples decode with.</summary>
    /// <param name="header">The image header.</param>
    /// <param name="source">The decoded channels.</param>
    /// <param name="plan">The output plan.</param>
    /// <returns>The header for sample decoding.</returns>
    private static ImageHeader ForSamples(in ImageHeader header, in JpxSampleSource source, JpxOutputPlan plan) => header with
    {
        Width = source.Width,
        Height = source.Height,
        BitsPerComponent = SampleBits,
        IsStencil = false,

        // The Decode array applies only when the dictionary gives the colour space (ISO 32000-2 section 7.4.9); the
        // raw indices of an Indexed space are not scaled samples, so they are never decoded.
        Decode = plan.KeepsDictionarySpace && plan.Mode == JpxSampleMode.Scaled ? header.Decode : null,
        ColorSpace = plan.ColorSpace,
        ColorKey = plan.KeepsDictionarySpace ? header.ColorKey : null,
    };

    /// <summary>The channels of a file and how they become samples.</summary>
    /// <param name="Set">The channels.</param>
    /// <param name="Plan">The output plan.</param>
    private readonly record struct ChannelPlan(JpxChannelSet Set, JpxOutputPlan Plan);

    /// <summary>The soft mask in the data: which channel holds it and whether the colour channels are multiplied by it.</summary>
    /// <param name="Channel">The index of the opacity channel, or -1 for none.</param>
    /// <param name="Premultiplied">Whether the colour channels are premultiplied (<c>/SMaskInData 2</c>).</param>
    private readonly record struct SoftMask(int Channel, bool Premultiplied);
}
