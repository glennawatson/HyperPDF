// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Optimizing;

namespace HyperPdf.Benchmarks;

/// <summary>Packs and unpacks a 300 dpi A4 black and white page, the bit by bit way it was written first and the way the encoder does it now.</summary>
public class HyperPdfBilevelPackBenchmarks
{
    /// <summary>The width of the page in pixels.</summary>
    private const int Width = 2480;

    /// <summary>The height of the page in pixels.</summary>
    private const int Height = 3508;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The mask of a pixel's bit index within its byte.</summary>
    private const int BitMask = 7;

    /// <summary>The sample value from which a pixel is white.</summary>
    private const int WhiteFrom = 128;

    /// <summary>The value of a white sample.</summary>
    private const byte White = 0xFF;

    /// <summary>The shift from a pixel index to its byte.</summary>
    private const int ByteShift = 3;

    /// <summary>The step of the pattern of black and white.</summary>
    private const int PatternStep = 7;

    /// <summary>The length of the repeat of the pattern.</summary>
    private const int PatternPeriod = 5;

    /// <summary>The positions of each repeat that are white.</summary>
    private const int PatternWhite = 2;

    /// <summary>The bytes of one packed row.</summary>
    private const int Stride = (Width + BitMask) / ByteBits;

    /// <summary>The samples, one byte a pixel.</summary>
    private readonly byte[] _samples = new byte[Width * Height];

    /// <summary>The packed rows.</summary>
    private readonly byte[] _packed = new byte[Stride * Height];

    /// <summary>The output of the bit by bit version.</summary>
    private readonly byte[] _scratch = new byte[Width * Height];

    /// <summary>Fills the page with a pattern of black and white.</summary>
    [GlobalSetup]
    public void Setup()
    {
        for (var index = 0; index < _samples.Length; index++)
        {
            _samples[index] = (index * PatternStep) % PatternPeriod < PatternWhite ? White : (byte)0;
        }

        var buffer = default(PooledBuffer);
        try
        {
            BilevelEncoder.Pack(_samples, Width, Height, ref buffer);
            buffer.WrittenSpan.CopyTo(_packed);
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Packs one pixel at a time.</summary>
    /// <returns>A byte of the result.</returns>
    [Benchmark(Baseline = true)]
    public byte PackBitByBit()
    {
        var target = _scratch.AsSpan(0, Stride * Height);
        target.Clear();
        for (var y = 0; y < Height; y++)
        {
            var row = _samples.AsSpan(y * Width, Width);
            var line = target.Slice(y * Stride, Stride);
            for (var x = 0; x < Width; x++)
            {
                if (row[x] >= WhiteFrom)
                {
                    line[x >> ByteShift] |= (byte)(1 << (BitMask - (x & BitMask)));
                }
            }
        }

        return target[1];
    }

    /// <summary>Packs with the encoder.</summary>
    /// <returns>A byte of the result.</returns>
    [Benchmark]
    public byte PackVector()
    {
        var buffer = default(PooledBuffer);
        try
        {
            BilevelEncoder.Pack(_samples, Width, Height, ref buffer);
            return buffer.WrittenSpan[1];
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Unpacks one pixel at a time.</summary>
    /// <returns>A byte of the result.</returns>
    [Benchmark]
    public byte UnpackBitByBit()
    {
        for (var y = 0; y < Height; y++)
        {
            var line = _packed.AsSpan(y * Stride, Stride);
            var row = _scratch.AsSpan(y * Width, Width);
            for (var x = 0; x < Width; x++)
            {
                row[x] = (line[x >> ByteShift] & (1 << (BitMask - (x & BitMask)))) != 0 ? White : (byte)0;
            }
        }

        return _scratch[1];
    }

    /// <summary>Unpacks with the encoder.</summary>
    /// <returns>A byte of the result.</returns>
    [Benchmark]
    public byte UnpackVector()
    {
        var buffer = default(PooledBuffer);
        try
        {
            BilevelEncoder.Unpack(_packed, Width, Height, ref buffer);
            return buffer.WrittenSpan[1];
        }
        finally
        {
            buffer.Dispose();
        }
    }
}
