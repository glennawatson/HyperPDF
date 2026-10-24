// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Graphics.Images.Jpeg;

namespace HyperPdf.Benchmarks;

/// <summary>Converts the rows of a 4096 pixel wide JPEG scan line from YCbCr to RGB, as the decoder does for every row.</summary>
public class HyperPdfJpegColorBenchmarks
{
    /// <summary>The pixels in a row.</summary>
    private const int Width = 4_096;

    /// <summary>The components of a pixel.</summary>
    private const int Components = 3;

    /// <summary>The rows converted by one call, so the time is measurable.</summary>
    private const int Rows = 256;

    /// <summary>The seed of the sample rows.</summary>
    private const uint Seed = 0x2545F491;

    /// <summary>The first xorshift shift.</summary>
    private const int ShiftA = 13;

    /// <summary>The second xorshift shift.</summary>
    private const int ShiftB = 17;

    /// <summary>The third xorshift shift.</summary>
    private const int ShiftC = 5;

    /// <summary>The Y, Cb and Cr rows.</summary>
    private readonly byte[] _rows = new byte[Width * Components];

    /// <summary>The RGB output.</summary>
    private readonly byte[] _rgb = new byte[Width * Components];

    /// <summary>Fills the rows with noise.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var state = Seed;
        for (var i = 0; i < _rows.Length; i++)
        {
            state ^= state << ShiftA;
            state ^= state >> ShiftB;
            state ^= state << ShiftC;
            _rows[i] = (byte)state;
        }
    }

    /// <summary>Converts the row many times.</summary>
    /// <returns>The first output byte.</returns>
    [Benchmark]
    public byte YccToRgb()
    {
        for (var row = 0; row < Rows; row++)
        {
            JpegColor.Interleave(_rows, Components, Width, true, _rgb);
        }

        return _rgb[0];
    }
}
