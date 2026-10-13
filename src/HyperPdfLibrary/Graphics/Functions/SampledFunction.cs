// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Functions;

/// <summary>
/// A type 0 sampled function. Samples are decoded to floats once, so evaluation is multilinear interpolation over the
/// table. Cubic spline interpolation (/Order 3) falls back to linear. A stream with fewer bits than the declared samples
/// need is rejected.
/// </summary>
internal sealed class SampledFunction : PdfFunction
{
    /// <summary>The most inputs a sampled function may have; interpolation visits 2^m corners.</summary>
    private const int MaxInputs = 8;

    /// <summary>The most sample values kept, bounding memory for hostile /Size arrays.</summary>
    private const long MaxSamples = 1 << 24;

    /// <summary>The widest sample read.</summary>
    private const int MaxBitsPerSample = 32;

    /// <summary>The bits in a byte.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The samples, decoded, with the outputs of one grid point adjacent.</summary>
    private readonly float[] _samples;

    /// <summary>The number of grid points along each input.</summary>
    private readonly int[] _size;

    /// <summary>The distance in grid points between neighbours along each input.</summary>
    private readonly int[] _strides;

    /// <summary>The domain, kept for the encode mapping.</summary>
    private readonly float[] _domain;

    /// <summary>The mapping of each input onto the sample grid, as pairs.</summary>
    private readonly float[] _encode;

    /// <summary>Initializes a new instance of the <see cref="SampledFunction"/> class.</summary>
    /// <param name="domain">The input domain.</param>
    /// <param name="range">The output range.</param>
    /// <param name="size">The grid size along each input.</param>
    /// <param name="encode">The encode mapping.</param>
    /// <param name="samples">The decoded samples.</param>
    private SampledFunction(float[] domain, float[] range, int[] size, float[] encode, float[] samples)
        : base(domain, range, range.Length / FunctionReader.PairSize)
    {
        _domain = domain;
        _size = size;
        _encode = encode;
        _samples = samples;
        _strides = new int[size.Length];
        var stride = 1;
        for (var i = 0; i < size.Length; i++)
        {
            _strides[i] = stride;
            stride *= size[i];
        }
    }

    /// <summary>Gets the /BitsPerSample values the specification allows.</summary>
    private static ReadOnlySpan<byte> ValidBits => [0x01, 0x02, 0x04, 0x08, 0x0C, 0x10, 0x18, 0x20];

    /// <summary>Parses a sampled function.</summary>
    /// <param name="stream">The function stream.</param>
    /// <param name="domain">The input domain.</param>
    /// <param name="range">The output range, required.</param>
    /// <returns>The function, or <see langword="null"/> when invalid.</returns>
    internal static SampledFunction? Parse(PdfStream? stream, float[] domain, float[]? range)
    {
        var inputs = domain.Length / FunctionReader.PairSize;
        if (stream is null || range is null || inputs > MaxInputs)
        {
            return null;
        }

        var dictionary = stream.Dictionary;
        var size = ReadSize(dictionary.GetArray(KnownName.Size), inputs, range.Length / FunctionReader.PairSize);
        var bits = dictionary.GetInt32(KnownName.BitsPerSample);
        if (size is null || !IsValidBits(bits))
        {
            return null;
        }

        var encode = FunctionReader.ReadExact(dictionary.GetArray(KnownName.Encode), FunctionReader.PairSize * inputs, DefaultEncode(size));
        var decode = FunctionReader.ReadExact(dictionary.GetArray(KnownName.Decode), range.Length, range);
        if (encode is null || decode is null)
        {
            return null;
        }

        var samples = ReadSamples(stream, size, range.Length / FunctionReader.PairSize, bits, decode);
        return samples is null ? null : new(domain, range, size, encode, samples);
    }

    /// <inheritdoc/>
    [SkipLocalsInit]
    private protected override void EvaluateCore(ReadOnlySpan<float> input, Span<float> output)
    {
        Span<int> floor = stackalloc int[MaxInputs];
        Span<float> fraction = stackalloc float[MaxInputs];
        for (var i = 0; i < _size.Length; i++)
        {
            var pair = FunctionReader.PairSize * i;
            var position = Interpolate(input[i], _domain[pair], _domain[pair + 1], _encode[pair], _encode[pair + 1]);
            position = Clamp(position, 0, _size[i] - 1);
            var whole = Math.Min((int)position, _size[i] - 1);
            floor[i] = whole;
            fraction[i] = position - whole;
        }

        if (_size.Length == 1)
        {
            InterpolateOne(floor[0], fraction[0], output);
            return;
        }

        InterpolateCorners(floor[.._size.Length], fraction[.._size.Length], output);
    }

    /// <summary>Checks a /BitsPerSample value.</summary>
    /// <param name="bits">The value.</param>
    /// <returns><see langword="true"/> when supported.</returns>
    private static bool IsValidBits(int bits) => bits is > 0 and <= MaxBitsPerSample && ValidBits.Contains((byte)bits);

    /// <summary>Reads the /Size array.</summary>
    /// <param name="array">The array.</param>
    /// <param name="inputs">The number of inputs.</param>
    /// <param name="outputs">The number of outputs.</param>
    /// <returns>The sizes, or <see langword="null"/> when invalid or too large.</returns>
    private static int[]? ReadSize(PdfArray? array, int inputs, int outputs)
    {
        if (array is null || array.Count < inputs)
        {
            return null;
        }

        var size = new int[inputs];
        long total = outputs;
        for (var i = 0; i < inputs; i++)
        {
            size[i] = array.GetInt32(i);
            total *= Math.Max(size[i], 0);
            if (size[i] < 1 || total > MaxSamples)
            {
                return null;
            }
        }

        return size;
    }

    /// <summary>Builds the default encode mapping, [0 Size-1] per input.</summary>
    /// <param name="size">The grid sizes.</param>
    /// <returns>The mapping.</returns>
    private static float[] DefaultEncode(int[] size)
    {
        var encode = new float[FunctionReader.PairSize * size.Length];
        for (var i = 0; i < size.Length; i++)
        {
            encode[(FunctionReader.PairSize * i) + 1] = size[i] - 1;
        }

        return encode;
    }

    /// <summary>Reads and decodes every sample. Data shorter than the declared sample count (ISO 32000-2 7.10.2) makes the function invalid.</summary>
    /// <param name="stream">The function stream.</param>
    /// <param name="size">The grid sizes.</param>
    /// <param name="outputs">The number of outputs.</param>
    /// <param name="bits">The bits per sample.</param>
    /// <param name="decode">The decode mapping.</param>
    /// <returns>The decoded samples, or <see langword="null"/> when the data is too short.</returns>
    private static float[]? ReadSamples(PdfStream stream, int[] size, int outputs, int bits, float[] decode)
    {
        var count = outputs;
        foreach (var points in size)
        {
            count *= points;
        }

        var maximum = (double)(bits == MaxBitsPerSample ? uint.MaxValue : (1U << bits) - 1);
        var data = default(PooledBuffer);
        try
        {
            _ = stream.Decode(ref data);
            var bytes = data.WrittenSpan;

            // The table is allocated only once the data is known to fill it.
            if (((long)count * bits) > (long)bytes.Length * BitsPerByte)
            {
                return null;
            }

            var samples = new float[count];
            for (var i = 0; i < samples.Length; i++)
            {
                var output = i % outputs;
                var raw = ReadBits(bytes, (long)i * bits, bits);
                samples[i] = (float)(decode[FunctionReader.PairSize * output] + (raw * (decode[(FunctionReader.PairSize * output) + 1] - decode[FunctionReader.PairSize * output]) / maximum));
            }

            return samples;
        }
        finally
        {
            data.Dispose();
        }
    }

    /// <summary>Reads an unsigned big-endian value of up to 32 bits at a bit offset; bits past the end read as zero.</summary>
    /// <param name="data">The data.</param>
    /// <param name="bitOffset">The first bit.</param>
    /// <param name="bits">The number of bits.</param>
    /// <returns>The value.</returns>
    private static uint ReadBits(ReadOnlySpan<byte> data, long bitOffset, int bits)
    {
        ulong value = 0;
        var remaining = bits;
        var position = bitOffset;
        while (remaining > 0)
        {
            var index = position / BitsPerByte;
            var bitInByte = (int)(position % BitsPerByte);
            var take = Math.Min(BitsPerByte - bitInByte, remaining);
            var current = index < data.Length ? data[(int)index] : 0;
            var chunk = (current >> (BitsPerByte - bitInByte - take)) & ((1 << take) - 1);
            value = (value << take) | (uint)chunk;
            remaining -= take;
            position += take;
        }

        return (uint)value;
    }

    /// <summary>Interpolates along a single input.</summary>
    /// <param name="floor">The grid point at or below the input.</param>
    /// <param name="fraction">The distance to the next grid point.</param>
    /// <param name="output">Receives the outputs.</param>
    private void InterpolateOne(int floor, float fraction, Span<float> output)
    {
        var outputs = output.Length;
        var low = _samples.AsSpan(floor * outputs, outputs);
        if (fraction == 0)
        {
            low.CopyTo(output);
            return;
        }

        var high = _samples.AsSpan((floor + 1) * outputs, outputs);
        for (var j = 0; j < outputs; j++)
        {
            output[j] = low[j] + (fraction * (high[j] - low[j]));
        }
    }

    /// <summary>Interpolates over every corner of the grid cell holding the input.</summary>
    /// <param name="floor">The grid point at or below the input, per input.</param>
    /// <param name="fraction">The distance to the next grid point, per input.</param>
    /// <param name="output">Receives the outputs.</param>
    private void InterpolateCorners(ReadOnlySpan<int> floor, ReadOnlySpan<float> fraction, Span<float> output)
    {
        output.Clear();
        var corners = 1 << floor.Length;
        for (var corner = 0; corner < corners; corner++)
        {
            var weight = CornerWeight(corner, floor, fraction, out var point);
            if (weight == 0)
            {
                continue;
            }

            var values = _samples.AsSpan(point * output.Length, output.Length);
            for (var j = 0; j < output.Length; j++)
            {
                output[j] += weight * values[j];
            }
        }
    }

    /// <summary>Computes one corner's weight and grid point.</summary>
    /// <param name="corner">The corner, one bit per input: set means the upper neighbour.</param>
    /// <param name="floor">The grid point at or below the input, per input.</param>
    /// <param name="fraction">The distance to the next grid point, per input.</param>
    /// <param name="point">The corner's grid point index.</param>
    /// <returns>The weight; zero when the corner does not contribute.</returns>
    private float CornerWeight(int corner, ReadOnlySpan<int> floor, ReadOnlySpan<float> fraction, out int point)
    {
        var weight = 1F;
        point = 0;
        for (var i = 0; i < floor.Length; i++)
        {
            var upper = ((corner >> i) & 1) != 0;
            if (upper && fraction[i] == 0)
            {
                return 0;
            }

            weight *= upper ? fraction[i] : 1 - fraction[i];
            point += (floor[i] + (upper ? 1 : 0)) * _strides[i];
        }

        return weight;
    }
}
