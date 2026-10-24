// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Codecs.H265;

namespace HyperPdf.Benchmarks;

/// <summary>Applies the H.265 inverse transform to blocks of each size, with few coefficients and with a full block of them.</summary>
public class HyperPdfH265TransformBenchmarks
{
    /// <summary>The bit depth of the samples.</summary>
    private const int BitDepth = 8;

    /// <summary>The largest block side.</summary>
    private const int Largest = 32;

    /// <summary>The base 2 logarithm of the side of the block that uses the DST.</summary>
    private const int DstLog2 = 2;

    /// <summary>The side of the corner that holds coefficients when the block is sparse.</summary>
    private const int SparseSide = 4;

    /// <summary>The multiplier that makes a coefficient depend on its row.</summary>
    private const int RowStep = 31;

    /// <summary>The multiplier that makes a coefficient depend on its column.</summary>
    private const int ColumnStep = 17;

    /// <summary>The number of distinct coefficient levels.</summary>
    private const int Levels = 201;

    /// <summary>The level subtracted so coefficients take both signs.</summary>
    private const int LevelOffset = 100;

    /// <summary>The scale from a level to a coefficient.</summary>
    private const int LevelScale = 7;

    /// <summary>The coefficients of the block, restored before each run.</summary>
    private readonly int[] _source = new int[Largest * Largest];

    /// <summary>The block that is transformed.</summary>
    private readonly int[] _block = new int[Largest * Largest];

    /// <summary>The work area.</summary>
    private readonly int[] _scratch = new int[Largest * Largest];

    /// <summary>Gets or sets the base 2 logarithm of the block side.</summary>
    [Params(2, 3, 4, 5)]
    public int Log2Size { get; set; }

    /// <summary>Gets or sets a value indicating whether every coefficient may be non-zero.</summary>
    [Params(false, true)]
    public bool Full { get; set; }

    /// <summary>Fills the coefficients with a repeatable pattern.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var side = 1 << Log2Size;
        var reach = Full ? side : Math.Min(side, SparseSide);
        for (var y = 0; y < reach; y++)
        {
            for (var x = 0; x < reach; x++)
            {
                _source[(y * side) + x] = ((((y * RowStep) + (x * ColumnStep)) % Levels) - LevelOffset) * LevelScale;
            }
        }
    }

    /// <summary>Transforms one block.</summary>
    /// <returns>The first residual sample.</returns>
    [Benchmark]
    public int Inverse()
    {
        var side = 1 << Log2Size;
        _source.AsSpan(0, side * side).CopyTo(_block);
        var reach = Full ? side : Math.Min(side, SparseSide);
        H265Transform.Inverse(_block, Log2Size, Log2Size == DstLog2, BitDepth, new(reach - 1, reach - 1), _scratch);
        return _block[0];
    }
}
