// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;

namespace HyperPdf.Codecs.Video;

/// <summary>Resizes one 8 bit plane of samples with bilinear weights, worked out once for a pair of sizes. Each row is blended vertically with vectors, then sampled across.</summary>
[DebuggerDisplay("PlaneResampler: {_sourceWidth}x{_sourceHeight} to {_targetWidth}x{_targetHeight}")]
public sealed class PlaneResampler
{
    /// <summary>The fractional bits of the weights.</summary>
    private const int WeightBits = 8;

    /// <summary>The weight that stands for a whole sample.</summary>
    private const int WeightOne = 1 << WeightBits;

    /// <summary>The fractional bits of the two stages together.</summary>
    private const int BothStagesBits = 2 * WeightBits;

    /// <summary>The rounding added before the two stages' fractional bits are dropped.</summary>
    private const int Rounding = 1 << (BothStagesBits - 1);

    /// <summary>Half a sample, which moves between a sample's corner and its centre.</summary>
    private const double Half = 0.5;

    /// <summary>The samples one vector of bytes holds.</summary>
    private const int VectorBytes = 16;

    /// <summary>The samples one vector of 16 bit numbers holds.</summary>
    private const int VectorShorts = VectorBytes >> 1;

    /// <summary>The width of the source plane.</summary>
    private readonly int _sourceWidth;

    /// <summary>The height of the source plane.</summary>
    private readonly int _sourceHeight;

    /// <summary>The width of the target plane.</summary>
    private readonly int _targetWidth;

    /// <summary>The height of the target plane.</summary>
    private readonly int _targetHeight;

    /// <summary>The left sample of each target column.</summary>
    private readonly int[] _columnFirst;

    /// <summary>The right sample of each target column.</summary>
    private readonly int[] _columnSecond;

    /// <summary>The weight of the right sample of each target column, from 0 to 255.</summary>
    private readonly int[] _columnWeight;

    /// <summary>The upper source row of each target row.</summary>
    private readonly int[] _rowFirst;

    /// <summary>The lower source row of each target row.</summary>
    private readonly int[] _rowSecond;

    /// <summary>The weight of the lower source row of each target row, from 0 to 255.</summary>
    private readonly int[] _rowWeight;

    /// <summary>A source row after the vertical blend, with the weights' fractional bits still in it.</summary>
    private readonly ushort[] _blended;

    /// <summary>Initializes a new instance of the <see cref="PlaneResampler"/> class.</summary>
    /// <param name="sourceWidth">The width of the source plane in samples.</param>
    /// <param name="sourceHeight">The height of the source plane in samples.</param>
    /// <param name="targetWidth">The width of the target plane in samples.</param>
    /// <param name="targetHeight">The height of the target plane in samples.</param>
    /// <exception cref="ArgumentOutOfRangeException">A size is not above zero.</exception>
    public PlaneResampler(int sourceWidth, int sourceHeight, int targetWidth, int targetHeight)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sourceHeight);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetWidth);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetHeight);
        _sourceWidth = sourceWidth;
        _sourceHeight = sourceHeight;
        _targetWidth = targetWidth;
        _targetHeight = targetHeight;
        _columnFirst = new int[targetWidth];
        _columnSecond = new int[targetWidth];
        _columnWeight = new int[targetWidth];
        _rowFirst = new int[targetHeight];
        _rowSecond = new int[targetHeight];
        _rowWeight = new int[targetHeight];
        _blended = new ushort[sourceWidth];
        Plan(sourceWidth, targetWidth, _columnFirst, _columnSecond, _columnWeight);
        Plan(sourceHeight, targetHeight, _rowFirst, _rowSecond, _rowWeight);
    }

    /// <summary>Resizes a plane.</summary>
    /// <param name="source">The source plane, row after row with no padding.</param>
    /// <param name="target">Receives the target plane, row after row with no padding.</param>
    /// <exception cref="ArgumentException">A plane is too short.</exception>
    public void Resample(ReadOnlySpan<byte> source, Span<byte> target)
    {
        if (source.Length < _sourceWidth * _sourceHeight || target.Length < _targetWidth * _targetHeight)
        {
            throw new ArgumentException("A plane is too short for its size.");
        }

        for (var row = 0; row < _targetHeight; row++)
        {
            BlendRows(source.Slice(_rowFirst[row] * _sourceWidth, _sourceWidth), source.Slice(_rowSecond[row] * _sourceWidth, _sourceWidth), _rowWeight[row], _blended);
            Sample(target.Slice(row * _targetWidth, _targetWidth));
        }
    }

    /// <summary>Works out which two source samples each target sample blends, and their weights, with the centres of the samples aligned.</summary>
    /// <param name="sourceSize">The samples in the source along the axis.</param>
    /// <param name="targetSize">The samples in the target along the axis.</param>
    /// <param name="first">Receives the lower source sample of each target sample.</param>
    /// <param name="second">Receives the higher source sample of each target sample.</param>
    /// <param name="weight">Receives the weight of the higher sample, from 0 to 255.</param>
    private static void Plan(int sourceSize, int targetSize, int[] first, int[] second, int[] weight)
    {
        var ratio = (double)sourceSize / targetSize;
        var last = sourceSize - 1;
        for (var i = 0; i < targetSize; i++)
        {
            var position = Math.Clamp(((i + Half) * ratio) - Half, 0, last);
            var whole = Math.Min((int)position, last);
            first[i] = whole;
            second[i] = Math.Min(whole + 1, last);
            weight[i] = Math.Min((int)(((position - whole) * WeightOne) + Half), WeightOne - 1);
        }
    }

    /// <summary>Blends two rows with vectors: <c>top * (256 - weight) + bottom * weight</c> for every sample.</summary>
    /// <param name="top">The upper row.</param>
    /// <param name="bottom">The lower row.</param>
    /// <param name="weight">The weight of the lower row, from 0 to 255.</param>
    /// <param name="output">Receives the blended row.</param>
    private static void BlendRows(ReadOnlySpan<byte> top, ReadOnlySpan<byte> bottom, int weight, Span<ushort> output)
    {
        var keep = WeightOne - weight;
        var x = 0;
        if (Vector128.IsHardwareAccelerated)
        {
            var keepLanes = Vector128.Create((ushort)keep);
            var takeLanes = Vector128.Create((ushort)weight);
            ref readonly var upper = ref MemoryMarshal.GetReference(top);
            ref readonly var lower = ref MemoryMarshal.GetReference(bottom);
            ref var written = ref MemoryMarshal.GetReference(output);
            for (; x + VectorBytes <= top.Length; x += VectorBytes)
            {
                var a = Vector128.LoadUnsafe(in upper, (nuint)x);
                var b = Vector128.LoadUnsafe(in lower, (nuint)x);
                ((Vector128.WidenLower(a) * keepLanes) + (Vector128.WidenLower(b) * takeLanes)).StoreUnsafe(ref written, (nuint)x);
                ((Vector128.WidenUpper(a) * keepLanes) + (Vector128.WidenUpper(b) * takeLanes)).StoreUnsafe(ref written, (nuint)(x + VectorShorts));
            }
        }

        for (; x < top.Length; x++)
        {
            output[x] = (ushort)((top[x] * keep) + (bottom[x] * weight));
        }
    }

    /// <summary>Samples the blended row across into a target row.</summary>
    /// <param name="row">Receives the row.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Sample(Span<byte> row)
    {
        for (var x = 0; x < row.Length; x++)
        {
            var weight = _columnWeight[x];
            row[x] = (byte)(((_blended[_columnFirst[x]] * (WeightOne - weight)) + (_blended[_columnSecond[x]] * weight) + Rounding) >> BothStagesBits);
        }
    }
}
