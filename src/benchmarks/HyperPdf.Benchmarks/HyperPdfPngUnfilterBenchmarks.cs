// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.ImageFiles;

namespace HyperPdf.Benchmarks;

/// <summary>Undoes one PNG row filter over every row of a three-byte-per-pixel image.</summary>
public class HyperPdfPngUnfilterBenchmarks
{
    /// <summary>The bytes in a pixel.</summary>
    private const int PixelBytes = 3;

    /// <summary>The bytes in a row.</summary>
    private const int RowBytes = 1024 * PixelBytes;

    /// <summary>The rows.</summary>
    private const int Rows = 512;

    /// <summary>The seed of the filtered bytes.</summary>
    private const uint Seed = 0x2545F491;

    /// <summary>The first xorshift shift.</summary>
    private const int ShiftA = 13;

    /// <summary>The second xorshift shift.</summary>
    private const int ShiftB = 17;

    /// <summary>The third xorshift shift.</summary>
    private const int ShiftC = 5;

    /// <summary>The filtered bytes.</summary>
    private byte[] _filtered = null!;

    /// <summary>The working copy, refreshed before each pass.</summary>
    private byte[] _work = null!;

    /// <summary>Gets or sets the PNG filter type under test: 1 Sub, 2 Up, 3 Average, 4 Paeth.</summary>
    [Params(1, 2, 3, 4)]
    public int Filter { get; set; }

    /// <summary>Builds the filtered rows.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _filtered = new byte[RowBytes * Rows];
        var state = Seed;
        for (var i = 0; i < _filtered.Length; i++)
        {
            state ^= state << ShiftA;
            state ^= state >> ShiftB;
            state ^= state << ShiftC;
            _filtered[i] = (byte)state;
        }

        _work = new byte[_filtered.Length];
    }

    /// <summary>Undoes the filter on every row.</summary>
    /// <returns>A byte of the result, so the work is not removed.</returns>
    [Benchmark]
    public byte UndoEveryRow()
    {
        _filtered.CopyTo(_work, 0);
        var zeros = new byte[RowBytes];
        for (var row = 0; row < Rows; row++)
        {
            var line = _work.AsSpan(row * RowBytes, RowBytes);
            var prior = row == 0 ? zeros : _work.AsSpan((row - 1) * RowBytes, RowBytes);
            PngUnfilter.Apply(Filter, line, prior, PixelBytes);
        }

        return _work[^1];
    }
}
