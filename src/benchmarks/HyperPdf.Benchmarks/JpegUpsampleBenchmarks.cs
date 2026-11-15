// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Graphics.Images.Jpeg;

namespace HyperPdf.Benchmarks;

/// <summary>Doubles the chroma samples of every row of a photograph, as decoding a JPEG with half-width colour planes does.</summary>
public class JpegUpsampleBenchmarks
{
    /// <summary>The chroma samples in a row, half the width of a 4096 pixel photograph.</summary>
    private const int RowSamples = 2048;

    /// <summary>The rows.</summary>
    private const int Rows = 1024;

    /// <summary>The seed of the samples.</summary>
    private const uint Seed = 0x2545F491;

    /// <summary>The first xorshift shift.</summary>
    private const int ShiftA = 13;

    /// <summary>The second xorshift shift.</summary>
    private const int ShiftB = 17;

    /// <summary>The third xorshift shift.</summary>
    private const int ShiftC = 5;

    /// <summary>The samples of one row.</summary>
    private byte[] _samples = null!;

    /// <summary>The doubled row.</summary>
    private byte[] _doubled = null!;

    /// <summary>Builds one row of samples.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _samples = new byte[RowSamples];
        var state = Seed;
        for (var i = 0; i < _samples.Length; i++)
        {
            state ^= state << ShiftA;
            state ^= state >> ShiftB;
            state ^= state << ShiftC;
            _samples[i] = (byte)state;
        }

        _doubled = new byte[RowSamples + RowSamples];
    }

    /// <summary>Doubles the samples of every row.</summary>
    /// <returns>The samples written for the last row.</returns>
    [Benchmark]
    public int DoubleEveryRow()
    {
        var written = 0;
        for (var row = 0; row < Rows; row++)
        {
            written = JpegUpsample.Double(_samples, _doubled);
        }

        return written;
    }
}
