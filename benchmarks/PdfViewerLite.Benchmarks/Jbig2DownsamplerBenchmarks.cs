// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Graphics.Images.Jbig2;

namespace PdfViewerLite.Benchmarks;

/// <summary>Compares binary-pixel averaging with grouped bit counting on scanned-book image sizes.</summary>
public class Jbig2DownsamplerBenchmarks
{
    /// <summary>The Internet Archive scan width.</summary>
    private const int ArchiveWidth = 1879;

    /// <summary>The Internet Archive scan height.</summary>
    private const int ArchiveHeight = 3059;

    /// <summary>The Google Books scan width.</summary>
    private const int GoogleWidth = 3406;

    /// <summary>The Google Books scan height.</summary>
    private const int GoogleHeight = 5270;

    /// <summary>The largest gray sample.</summary>
    private const int White = 255;

    /// <summary>The multiplier used to vary the packed sample bytes.</summary>
    private const int PatternMultiplier = 73;

    /// <summary>The shift used to vary the packed sample bytes.</summary>
    private const int PatternShift = 3;

    /// <summary>The packed source rows.</summary>
    private byte[] _rows = [];

    /// <summary>The reduced output samples.</summary>
    private byte[] _samples = [];

    /// <summary>The source width.</summary>
    private int _width;

    /// <summary>The source height.</summary>
    private int _height;

    /// <summary>The packed row length.</summary>
    private int _rowBytes;

    /// <summary>Gets or sets the image geometry from a scanned book.</summary>
    [Params("Archive", "Google")]
    public string Fixture { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of native display halvings.</summary>
    [Params(1, 2, 3, 4)]
    public int Levels { get; set; }

    /// <summary>Creates repeatable packed rows and an output buffer outside the measured methods.</summary>
    /// <exception cref="InvalidOperationException">The two methods produce different samples.</exception>
    [GlobalSetup]
    public void Setup()
    {
        (_width, _height) = Fixture == "Archive" ? (ArchiveWidth, ArchiveHeight) : (GoogleWidth, GoogleHeight);
        _rowBytes = Jbig2Bits.Stride(_width);
        _rows = new byte[_rowBytes * _height];
        for (var i = 0; i < _rows.Length; i++)
        {
            _rows[i] = (byte)((i * PatternMultiplier) ^ (i >> PatternShift));
        }

        var side = 1 << Levels;
        _samples = new byte[((_width + side - 1) / side) * ((_height + side - 1) / side)];
        _ = Scalar();
        var expected = (byte[])_samples.Clone();
        _ = BitCount();
        if (!expected.AsSpan().SequenceEqual(_samples))
        {
            throw new InvalidOperationException("The downsampling methods disagree.");
        }
    }

    /// <summary>Averages every binary source pixel, the original display-size decode.</summary>
    /// <returns>The first output sample.</returns>
    [Benchmark(Baseline = true)]
    public byte Scalar()
    {
        var side = 1 << Levels;
        var width = (_width + side - 1) / side;
        var height = (_height + side - 1) / side;
        for (var y = 0; y < height; y++)
        {
            var y0 = y * side;
            var y1 = Math.Min(y0 + side, _height);
            for (var x = 0; x < width; x++)
            {
                var x0 = x * side;
                var x1 = Math.Min(x0 + side, _width);
                var white = CountWhite(x0, x1, y0, y1);
                _samples[(y * width) + x] = (byte)((white * White) / ((x1 - x0) * (y1 - y0)));
            }
        }

        return _samples[0];
    }

    /// <summary>Counts packed bit groups with hardware popcount when available.</summary>
    /// <returns>The first output sample.</returns>
    [Benchmark]
    public byte BitCount()
    {
        Jbig2Downsampler.Average(_rows, _width, _height, _rowBytes, Levels, _samples);
        return _samples[0];
    }

    /// <summary>Counts one block as the scalar reference does.</summary>
    /// <param name="x0">The first column.</param>
    /// <param name="x1">One past the last column.</param>
    /// <param name="y0">The first row.</param>
    /// <param name="y1">One past the last row.</param>
    /// <returns>The number of white source pixels.</returns>
    private int CountWhite(int x0, int x1, int y0, int y1)
    {
        var white = 0;
        for (var y = y0; y < y1; y++)
        {
            var row = _rows.AsSpan(y * _rowBytes, _rowBytes);
            for (var x = x0; x < x1; x++)
            {
                white += Jbig2Bits.Get(row, x, _width);
            }
        }

        return white;
    }
}
