// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Graphics.Images;

namespace HyperPdf.Benchmarks;

/// <summary>Enlarges a 300 by 200 BGRA image to 1200 by 800, as a small soft-masked image is before it is drawn.</summary>
public class HyperPdfBgraUpscaleBenchmarks
{
    /// <summary>The source width.</summary>
    private const int SourceWidth = 300;

    /// <summary>The source height.</summary>
    private const int SourceHeight = 200;

    /// <summary>The target width.</summary>
    private const int Width = 1_200;

    /// <summary>The target height.</summary>
    private const int Height = 800;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The seed of the sample pixels.</summary>
    private const uint Seed = 0x2545F491;

    /// <summary>The first xorshift shift.</summary>
    private const int ShiftA = 13;

    /// <summary>The second xorshift shift.</summary>
    private const int ShiftB = 17;

    /// <summary>The third xorshift shift.</summary>
    private const int ShiftC = 5;

    /// <summary>The source pixels.</summary>
    private readonly byte[] _source = new byte[SourceWidth * SourceHeight * PixelBytes];

    /// <summary>The enlarged pixels.</summary>
    private readonly byte[] _target = new byte[Width * Height * PixelBytes];

    /// <summary>Fills the source with noise.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var state = Seed;
        for (var i = 0; i < _source.Length; i++)
        {
            state ^= state << ShiftA;
            state ^= state >> ShiftB;
            state ^= state << ShiftC;
            _source[i] = (byte)state;
        }
    }

    /// <summary>Enlarges the image.</summary>
    /// <returns>The first output byte.</returns>
    [Benchmark]
    public byte Upscale()
    {
        ImageAlpha.UpscaleBgra(_source, SourceWidth, SourceHeight, _target, Width, Height);
        return _target[0];
    }
}
