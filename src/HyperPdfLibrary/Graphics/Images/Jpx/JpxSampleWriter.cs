// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Writes decoded channels as interleaved 8-bit samples. A channel of P bits holds values 0 to 2^P - 1 (T.800 annex G
/// level-shifts signed values up by 2^(P-1) so the range is the same), and the writer scales that range linearly onto
/// 0 to 255 with rounding to nearest, so black stays 0 and white stays 255 at every depth. Channels with fewer samples
/// than the output, such as subsampled chroma, are stretched by repeating samples; palette channels look their values up first.
/// </summary>
internal static class JpxSampleWriter
{
    /// <summary>The bits of an output sample.</summary>
    private const int SampleBits = 8;

    /// <summary>The largest output sample.</summary>
    private const int MaxSample = 255;

    /// <summary>The samples per pixel of an RGB pixel.</summary>
    private const int RgbComponents = 3;

    /// <summary>The second chroma sample of a pixel.</summary>
    private const int SecondChroma = 2;

    /// <summary>The fixed-point fraction bits of the YCbCr coefficients.</summary>
    private const int CoefficientShift = 16;

    /// <summary>One half in the coefficient fraction.</summary>
    private const int CoefficientHalf = 1 << (CoefficientShift - 1);

    /// <summary>The 8-bit value of zero chroma.</summary>
    private const int ChromaCentre = 128;

    /// <summary>The red-from-Cr coefficient, 1.402 (ITU-R BT.601 full range, as sYCC uses) times 65,536.</summary>
    private const int RedFromCr = 91_881;

    /// <summary>The green-from-Cb coefficient, 0.344136, times 65,536.</summary>
    private const int GreenFromCb = 22_554;

    /// <summary>The green-from-Cr coefficient, 0.714136, times 65,536.</summary>
    private const int GreenFromCr = 46_802;

    /// <summary>The blue-from-Cb coefficient, 1.772, times 65,536.</summary>
    private const int BlueFromCb = 116_130;

    /// <summary>The pixels one vector of bytes holds.</summary>
    private const int VectorPixels = 16;

    /// <summary>The 32-bit values one 128-bit vector holds.</summary>
    private const int IntsPerVector = 4;

    /// <summary>The offset of the third group of four values in sixteen.</summary>
    private const int ThirdQuad = 2 * IntsPerVector;

    /// <summary>The offset of the fourth group of four values in sixteen.</summary>
    private const int FourthQuad = 3 * IntsPerVector;

    /// <summary>The offset of the second 16 bytes of 48 interleaved output bytes.</summary>
    private const int SecondBlock = VectorPixels;

    /// <summary>The offset of the third 16 bytes of 48 interleaved output bytes.</summary>
    private const int ThirdBlock = 2 * VectorPixels;

    /// <summary>The shuffle index that selects nothing, so the byte is zero.</summary>
    private const byte Unselected = 0xFF;

    /// <summary>Selects the red bytes of the first 16 interleaved output bytes.</summary>
    private static readonly Vector128<byte> RedSelect0 = SelectChannel(0, 0);

    /// <summary>Selects the red bytes of the second 16 interleaved output bytes.</summary>
    private static readonly Vector128<byte> RedSelect1 = SelectChannel(1, 0);

    /// <summary>Selects the red bytes of the third 16 interleaved output bytes.</summary>
    private static readonly Vector128<byte> RedSelect2 = SelectChannel(2, 0);

    /// <summary>Selects the green bytes of the first 16 interleaved output bytes.</summary>
    private static readonly Vector128<byte> GreenSelect0 = SelectChannel(0, 1);

    /// <summary>Selects the green bytes of the second 16 interleaved output bytes.</summary>
    private static readonly Vector128<byte> GreenSelect1 = SelectChannel(1, 1);

    /// <summary>Selects the green bytes of the third 16 interleaved output bytes.</summary>
    private static readonly Vector128<byte> GreenSelect2 = SelectChannel(2, 1);

    /// <summary>Selects the blue bytes of the first 16 interleaved output bytes.</summary>
    private static readonly Vector128<byte> BlueSelect0 = SelectChannel(0, 2);

    /// <summary>Selects the blue bytes of the second 16 interleaved output bytes.</summary>
    private static readonly Vector128<byte> BlueSelect1 = SelectChannel(1, 2);

    /// <summary>Selects the blue bytes of the third 16 interleaved output bytes.</summary>
    private static readonly Vector128<byte> BlueSelect2 = SelectChannel(2, 2);

    /// <summary>Writes the planned components.</summary>
    /// <param name="source">The decoded planes, channels and palette.</param>
    /// <param name="plan">The output plan.</param>
    /// <param name="samples">Receives the interleaved samples.</param>
    internal static void Write(in JpxSampleSource source, JpxOutputPlan plan, Span<byte> samples)
    {
        var rowBytes = source.Width * plan.Components;
        for (var y = 0; y < source.Height; y++)
        {
            WritePlanRow(source, plan, y, samples.Slice(y * rowBytes, rowBytes));
        }
    }

    /// <summary>Writes one row of a channel as single 8-bit samples, for an alpha plane.</summary>
    /// <param name="source">The decoded planes, channels and palette.</param>
    /// <param name="channel">The channel index.</param>
    /// <param name="y">The output row.</param>
    /// <param name="alpha">Receives one sample per pixel of the row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void WriteAlphaRow(in JpxSampleSource source, int channel, int y, Span<byte> alpha) =>
        WriteChannelRow(source, source.Channels[channel], JpxSampleMode.Scaled, 0, 1, y, alpha);

    /// <summary>
    /// Divides colour samples that were multiplied by their opacity (<c>/SMaskInData 2</c>, ISO 32000-2 section 7.4.9) by it
    /// again, so every image gets straight colours and the caller applies the opacity once, to the pixels. A pixel with no
    /// opacity keeps zero samples.
    /// </summary>
    /// <param name="samples">The samples, updated in place.</param>
    /// <param name="alpha">The opacity of each pixel.</param>
    /// <param name="stride">The samples per pixel.</param>
    internal static void Unpremultiply(Span<byte> samples, ReadOnlySpan<byte> alpha, int stride)
    {
        for (var i = 0; i < alpha.Length; i++)
        {
            var a = alpha[i];
            if (a == MaxSample)
            {
                continue;
            }

            var pixel = samples.Slice(i * stride, stride);
            for (var c = 0; c < pixel.Length; c++)
            {
                pixel[c] = a == 0 ? (byte)0 : (byte)Math.Min(MaxSample, ((pixel[c] * MaxSample) + (a >> 1)) / a);
            }
        }
    }

    /// <summary>Scales one value to eight bits.</summary>
    /// <param name="value">The value.</param>
    /// <param name="precision">The value's bits.</param>
    /// <param name="signed">Whether the value is signed.</param>
    /// <returns>The 8-bit sample.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static byte ToByte(int value, int precision, bool signed) => new SampleScale(precision, signed).Apply(value);

    /// <summary>Writes one output row of every planned component, converting sYCC to RGB when the plan asks.</summary>
    /// <param name="source">The decoded planes, channels and palette.</param>
    /// <param name="plan">The output plan.</param>
    /// <param name="y">The output row.</param>
    /// <param name="row">Receives the row's interleaved samples.</param>
    internal static void WritePlanRow(in JpxSampleSource source, JpxOutputPlan plan, int y, Span<byte> row)
    {
        if (!TryWriteDirectRgbRow(source, plan, y, row))
        {
            for (var o = 0; o < plan.Components; o++)
            {
                WriteChannelRow(source, source.Channels[o], plan.Mode, o, plan.Components, y, row);
            }
        }

        if (plan.Sycc)
        {
            ConvertYcc(row[..(source.Width * RgbComponents)]);
        }
    }

    /// <summary>Narrows three rows of 8-bit values to bytes and interleaves them as RGB triples.</summary>
    /// <param name="first">The first channel's values.</param>
    /// <param name="second">The second channel's values.</param>
    /// <param name="third">The third channel's values.</param>
    /// <param name="output">Receives three bytes per pixel.</param>
    internal static void InterleaveRgb(ReadOnlySpan<int> first, ReadOnlySpan<int> second, ReadOnlySpan<int> third, Span<byte> output)
    {
        var count = Math.Min(Math.Min(first.Length, second.Length), Math.Min(third.Length, output.Length / RgbComponents));
        var pixel = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            ref var r = ref MemoryMarshal.GetReference(first);
            ref var g = ref MemoryMarshal.GetReference(second);
            ref var b = ref MemoryMarshal.GetReference(third);
            ref var o = ref MemoryMarshal.GetReference(output);
            for (; pixel <= count - VectorPixels; pixel += VectorPixels)
            {
                var red = NarrowSixteen(ref r, pixel);
                var green = NarrowSixteen(ref g, pixel);
                var blue = NarrowSixteen(ref b, pixel);
                var at = (nuint)(pixel * RgbComponents);
                (Vector128.Shuffle(red, RedSelect0) | Vector128.Shuffle(green, GreenSelect0) | Vector128.Shuffle(blue, BlueSelect0)).StoreUnsafe(ref o, at);
                (Vector128.Shuffle(red, RedSelect1) | Vector128.Shuffle(green, GreenSelect1) | Vector128.Shuffle(blue, BlueSelect1)).StoreUnsafe(ref o, at + SecondBlock);
                (Vector128.Shuffle(red, RedSelect2) | Vector128.Shuffle(green, GreenSelect2) | Vector128.Shuffle(blue, BlueSelect2)).StoreUnsafe(ref o, at + ThirdBlock);
            }
        }

        for (; pixel < count; pixel++)
        {
            output[pixel * RgbComponents] = (byte)first[pixel];
            output[(pixel * RgbComponents) + 1] = (byte)second[pixel];
            output[(pixel * RgbComponents) + SecondChroma] = (byte)third[pixel];
        }
    }

    /// <summary>
    /// Converts 8-bit YCbCr samples to RGB in place. The matrix is the full-range ITU-R BT.601 one that sYCC uses
    /// (IEC 61966-2-1 Amd 1): R = Y + 1.402 Cr, G = Y - 0.344136 Cb - 0.714136 Cr, B = Y + 1.772 Cb, with Cb and Cr
    /// centred on 128. Fixed-point arithmetic rounds to nearest and the results clamp to 0 to 255.
    /// </summary>
    /// <param name="samples">Three samples per pixel.</param>
    internal static void ConvertYcc(Span<byte> samples)
    {
        for (var i = 0; i + RgbComponents <= samples.Length; i += RgbComponents)
        {
            int y = samples[i];
            var cb = samples[i + 1] - ChromaCentre;
            var cr = samples[i + SecondChroma] - ChromaCentre;
            samples[i] = Clamp(y + (((RedFromCr * cr) + CoefficientHalf) >> CoefficientShift));
            samples[i + 1] = Clamp(y - (((GreenFromCb * cb) + (GreenFromCr * cr) + CoefficientHalf) >> CoefficientShift));
            samples[i + SecondChroma] = Clamp(y + (((BlueFromCb * cb) + CoefficientHalf) >> CoefficientShift));
        }
    }

    /// <summary>Writes one row of one channel into every <paramref name="stride"/>-th sample from <paramref name="offset"/>.</summary>
    /// <param name="source">The decoded planes, channels and palette.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="mode">How values become samples.</param>
    /// <param name="offset">The sample within a pixel.</param>
    /// <param name="stride">The samples per pixel.</param>
    /// <param name="y">The output row.</param>
    /// <param name="output">The output from the row's first pixel.</param>
    private static void WriteChannelRow(in JpxSampleSource source, in JpxChannel channel, JpxSampleMode mode, int offset, int stride, int y, Span<byte> output)
    {
        var line = GetPlaneRow(source, channel, y);
        if (IsDirect(source, channel, mode))
        {
            WriteDirectRow(line, output, offset, stride);
        }
        else
        {
            var palette = channel.Column >= 0 ? source.Palette : null;
            WriteRow(source.Width, new(palette, channel.Column, new SampleScale(channel.Precision, channel.Signed), mode), line, output, offset, stride);
        }
    }

    /// <summary>Determines whether a channel's samples are 8-bit unsigned values at the output size, which need no scaling, lookup or stretching.</summary>
    /// <param name="source">The decoded planes, channels and palette.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="mode">How values become samples.</param>
    /// <returns><see langword="true"/> when each plane value is the output sample.</returns>
    private static bool IsDirect(in JpxSampleSource source, in JpxChannel channel, JpxSampleMode mode)
    {
        var area = source.Image.Areas[channel.Component];
        return (channel.Column < 0 || source.Palette is null) && mode == JpxSampleMode.Scaled && channel is { Precision: SampleBits, Signed: false }
            && area.Width == source.Width && area.Height == source.Height;
    }

    /// <summary>Gets the plane row that output row <paramref name="y"/> reads.</summary>
    /// <param name="source">The decoded planes, channels and palette.</param>
    /// <param name="channel">The channel.</param>
    /// <param name="y">The output row.</param>
    /// <returns>The plane's samples for that row.</returns>
    private static ReadOnlySpan<int> GetPlaneRow(in JpxSampleSource source, in JpxChannel channel, int y)
    {
        var area = source.Image.Areas[channel.Component];
        return source.Image.GetRow(channel.Component, (int)((long)y * area.Height / source.Height), source.Data);
    }

    /// <summary>Writes a three-channel row of 8-bit unsigned samples that need no scaling, interleaving them with vector instructions.</summary>
    /// <param name="source">The decoded planes, channels and palette.</param>
    /// <param name="plan">The output plan.</param>
    /// <param name="y">The output row.</param>
    /// <param name="row">Receives the row's interleaved samples.</param>
    /// <returns><see langword="false"/> when the row is not such a row or the hardware has no vectors, and nothing was written.</returns>
    private static bool TryWriteDirectRgbRow(in JpxSampleSource source, JpxOutputPlan plan, int y, Span<byte> row)
    {
        if (!Vector128.IsHardwareAccelerated || plan.Components != RgbComponents)
        {
            return false;
        }

        for (var o = 0; o < RgbComponents; o++)
        {
            if (!IsDirect(source, source.Channels[o], plan.Mode))
            {
                return false;
            }
        }

        InterleaveRgb(
            GetPlaneRow(source, source.Channels[0], y),
            GetPlaneRow(source, source.Channels[1], y),
            GetPlaneRow(source, source.Channels[SecondChroma], y),
            row);
        return true;
    }

    /// <summary>Narrows sixteen values to bytes.</summary>
    /// <param name="values">The first value's reference.</param>
    /// <param name="index">The index of the first of the sixteen values.</param>
    /// <returns>The values as bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<byte> NarrowSixteen(ref int values, int index)
    {
        var low = Vector128.Narrow(Vector128.LoadUnsafe(ref values, (nuint)index), Vector128.LoadUnsafe(ref values, (nuint)(index + IntsPerVector)));
        var high = Vector128.Narrow(Vector128.LoadUnsafe(ref values, (nuint)(index + ThirdQuad)), Vector128.LoadUnsafe(ref values, (nuint)(index + FourthQuad)));
        return Vector128.Narrow(low.AsUInt16(), high.AsUInt16());
    }

    /// <summary>Selects the bytes of one channel for output block <paramref name="block"/>, with every other byte set to an out-of-range index, which shuffles to zero.</summary>
    /// <param name="block">The 16-byte block of the 48 interleaved output bytes.</param>
    /// <param name="channel">The channel: 0, 1 or 2.</param>
    /// <returns>The shuffle indices.</returns>
    private static Vector128<byte> SelectChannel(int block, int channel)
    {
        Span<byte> indices = stackalloc byte[VectorPixels];
        for (var j = 0; j < VectorPixels; j++)
        {
            var position = (block * VectorPixels) + j;
            indices[j] = position % RgbComponents == channel ? (byte)(position / RgbComponents) : Unselected;
        }

        return Vector128.Create(indices);
    }

    /// <summary>Writes a row of 8-bit unsigned values that need no scaling.</summary>
    /// <param name="line">The plane row.</param>
    /// <param name="output">The output from the row's first pixel.</param>
    /// <param name="offset">The sample within a pixel.</param>
    /// <param name="stride">The samples per pixel.</param>
    private static void WriteDirectRow(ReadOnlySpan<int> line, Span<byte> output, int offset, int stride)
    {
        var start = 0;
        if (stride == 1 && Vector128.IsHardwareAccelerated)
        {
            ref var values = ref MemoryMarshal.GetReference(line);
            ref var bytes = ref MemoryMarshal.GetReference(output[offset..]);
            for (; start <= line.Length - VectorPixels; start += VectorPixels)
            {
                NarrowSixteen(ref values, start).StoreUnsafe(ref bytes, (nuint)start);
            }
        }

        var position = offset + (start * stride);
        for (var i = start; i < line.Length; i++)
        {
            output[position] = (byte)line[i];
            position += stride;
        }
    }

    /// <summary>Writes a row that needs a palette, scaling or stretching.</summary>
    /// <param name="width">The output width.</param>
    /// <param name="reading">How the values are read.</param>
    /// <param name="line">The plane row.</param>
    /// <param name="output">The output from the row's first pixel.</param>
    /// <param name="offset">The sample within a pixel.</param>
    /// <param name="stride">The samples per pixel.</param>
    private static void WriteRow(int width, in ChannelReading reading, ReadOnlySpan<int> line, Span<byte> output, int offset, int stride)
    {
        for (var x = 0; x < width; x++)
        {
            var value = line[(int)((long)x * line.Length / width)];
            if (reading.Palette is { } palette)
            {
                value = palette.Get(value, reading.Column);
            }

            output[(x * stride) + offset] = reading.Mode == JpxSampleMode.RawIndices ? (byte)Math.Clamp(value, 0, MaxSample) : reading.Scale.Apply(value);
        }
    }

    /// <summary>Limits a value to the sample range.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The sample.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static byte Clamp(int value) => (byte)Math.Clamp(value, 0, MaxSample);

    /// <summary>How the values of one channel row are read.</summary>
    /// <param name="Palette">The palette, or <see langword="null"/>.</param>
    /// <param name="Column">The palette column.</param>
    /// <param name="Scale">The scale to eight bits.</param>
    /// <param name="Mode">How values become samples.</param>
    private readonly record struct ChannelReading(JpxPalette? Palette, int Column, SampleScale Scale, JpxSampleMode Mode);

    /// <summary>
    /// The linear map from a value of P bits to 0 to 255: sample = (value * 255 / (2^P - 1)) rounded to nearest, computed
    /// as a 32-bit fixed-point multiply so each sample costs one multiply and a shift.
    /// </summary>
    private readonly struct SampleScale
    {
        /// <summary>The width of the fixed-point fraction of the scale factor.</summary>
        private const int ScaleShift = 32;

        /// <summary>One half in the fixed-point fraction, which rounds to nearest.</summary>
        private const ulong ScaleHalf = 1UL << (ScaleShift - 1);

        /// <summary>The most bits of a value the scale handles; wider values are cut to this.</summary>
        private const int MaxScaledBits = 31;

        /// <summary>The divisor that halves a maximum value for rounding.</summary>
        private const ulong Halve = 2;

        /// <summary>The amount added to a signed value to make it unsigned.</summary>
        private readonly int _offset;

        /// <summary>The largest value of the depth.</summary>
        private readonly int _max;

        /// <summary>The fixed-point factor, 255 / max.</summary>
        private readonly ulong _factor;

        /// <summary>Initializes a new instance of the <see cref="SampleScale"/> struct.</summary>
        /// <param name="precision">The value's bits.</param>
        /// <param name="signed">Whether the value is signed.</param>
        internal SampleScale(int precision, bool signed)
        {
            var bits = Math.Clamp(precision, 1, MaxScaledBits);
            _max = (int)((1L << bits) - 1);
            _offset = signed ? 1 << (bits - 1) : 0;
            _factor = (((ulong)MaxSample << ScaleShift) + ((ulong)_max / Halve)) / (ulong)_max;
        }

        /// <summary>Scales a value, limiting it to the depth's range first.</summary>
        /// <param name="value">The value.</param>
        /// <returns>The 8-bit sample.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal byte Apply(int value) => (byte)((((ulong)Math.Clamp(value + _offset, 0, _max) * _factor) + ScaleHalf) >> ScaleShift);
    }
}
