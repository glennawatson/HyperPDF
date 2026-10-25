// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Graphics.Images;

namespace HyperPdf.Benchmarks;

/// <summary>Resamples a soft mask plane to a smaller and to a larger size, as an image whose mask has another resolution is before it is drawn.</summary>
public class HyperPdfPlaneResampleBenchmarks
{
    /// <summary>The side of the large plane.</summary>
    private const int LargeSide = 2_048;

    /// <summary>The side of the plane it shrinks to.</summary>
    private const int ShrunkSide = 1_000;

    /// <summary>The side of the small plane.</summary>
    private const int SmallSide = 300;

    /// <summary>The side of the plane it grows to.</summary>
    private const int GrownSide = 1_200;

    /// <summary>The seed of the sample bytes.</summary>
    private const uint Seed = 0x2545F491;

    /// <summary>The first xorshift shift.</summary>
    private const int ShiftA = 13;

    /// <summary>The second xorshift shift.</summary>
    private const int ShiftB = 17;

    /// <summary>The third xorshift shift.</summary>
    private const int ShiftC = 5;

    /// <summary>The large plane.</summary>
    private readonly byte[] _large = new byte[LargeSide * LargeSide];

    /// <summary>The shrunk plane.</summary>
    private readonly byte[] _shrunk = new byte[ShrunkSide * ShrunkSide];

    /// <summary>The small plane.</summary>
    private readonly byte[] _small = new byte[SmallSide * SmallSide];

    /// <summary>The grown plane.</summary>
    private readonly byte[] _grown = new byte[GrownSide * GrownSide];

    /// <summary>Fills the planes with noise.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var state = Seed;
        Fill(_large, ref state);
        Fill(_small, ref state);
    }

    /// <summary>Averages the source pixels each target pixel covers.</summary>
    /// <returns>The first output byte.</returns>
    [Benchmark]
    public byte Shrink()
    {
        ImageAlpha.ResamplePlane(_large, LargeSide, LargeSide, _shrunk, ShrunkSide, ShrunkSide);
        return _shrunk[0];
    }

    /// <summary>Reads the source pixel under each target pixel.</summary>
    /// <returns>The first output byte.</returns>
    [Benchmark]
    public byte Grow()
    {
        ImageAlpha.ResamplePlane(_small, SmallSide, SmallSide, _grown, GrownSide, GrownSide);
        return _grown[0];
    }

    /// <summary>Fills a plane with noise.</summary>
    /// <param name="plane">The plane.</param>
    /// <param name="state">The generator state.</param>
    private static void Fill(byte[] plane, ref uint state)
    {
        for (var i = 0; i < plane.Length; i++)
        {
            state ^= state << ShiftA;
            state ^= state >> ShiftB;
            state ^= state << ShiftC;
            plane[i] = (byte)state;
        }
    }
}
