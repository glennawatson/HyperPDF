// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Tests.Filters;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Benchmarks;

/// <summary>Encodes page-content text, noise and runs with the LZW, run-length, ASCII base-85 and ASCII hexadecimal encoders, and with Flate for comparison.</summary>
public class HyperPdfFilterEncodeBenchmarks
{
    /// <summary>The size of each sample in bytes.</summary>
    private const int SampleLength = 65_536;

    /// <summary>The seed of the generated data.</summary>
    private const uint Seed = 0x9E3779B9;

    /// <summary>The number of values a byte can take.</summary>
    private const int ByteValues = 256;

    /// <summary>The longest run in the run sample.</summary>
    private const int MaxRun = 200;

    /// <summary>The range of the horizontal coordinates in the content sample.</summary>
    private const int CoordinateX = 600;

    /// <summary>The range of the vertical coordinates in the content sample.</summary>
    private const int CoordinateY = 800;

    /// <summary>The range of the numbers in the shown text of the content sample.</summary>
    private const int TextNumber = 99;

    /// <summary>The sample being encoded.</summary>
    private byte[] _data = [];

    /// <summary>Gets or sets the sample encoded.</summary>
    [Params("Content", "Noise", "Runs")]
    public string Sample { get; set; } = "Content";

    /// <summary>Builds the sample.</summary>
    [GlobalSetup]
    public void Setup() => _data = Sample switch
    {
        "Noise" => Noise(),
        "Runs" => Runs(),
        _ => Content(),
    };

    /// <summary>Encodes the sample as LZW with /EarlyChange 1.</summary>
    /// <returns>The encoded length.</returns>
    [Benchmark]
    public int Lzw()
    {
        var output = default(PooledBuffer);
        try
        {
            LzwEncoder.Encode(_data, 1, ref output);
            return output.Length;
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Encodes the sample as run-length data.</summary>
    /// <returns>The encoded length.</returns>
    [Benchmark]
    public int RunLength()
    {
        var output = default(PooledBuffer);
        try
        {
            RunLengthFilter.Encode(_data, ref output);
            return output.Length;
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Encodes the sample as ASCII base-85 text.</summary>
    /// <returns>The encoded length.</returns>
    [Benchmark]
    public int Ascii85()
    {
        var output = default(PooledBuffer);
        try
        {
            Ascii85Filter.Encode(_data, ref output);
            return output.Length;
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Encodes the sample as ASCII hexadecimal text.</summary>
    /// <returns>The encoded length.</returns>
    [Benchmark]
    public int AsciiHex()
    {
        var output = default(PooledBuffer);
        try
        {
            AsciiHexFilter.Encode(_data, ref output);
            return output.Length;
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Encodes the sample as Flate at the balanced level, the encoder the writers use.</summary>
    /// <returns>The encoded length.</returns>
    [Benchmark(Baseline = true)]
    public int Flate()
    {
        var output = default(PooledBuffer);
        try
        {
            FlateFilter.Encode(_data, PdfCompressionLevel.Balanced, ref output);
            return output.Length;
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Makes bytes that look like page content: operators, numbers and spaces.</summary>
    /// <returns>The sample.</returns>
    private static byte[] Content()
    {
        var random = new SampleRandom(Seed);
        var data = new byte[SampleLength];
        var rest = data.AsSpan();
        while (!rest.IsEmpty)
        {
            var line = System.Text.Encoding.ASCII.GetBytes(Line(random));
            var count = Math.Min(line.Length, rest.Length);
            line.AsSpan(0, count).CopyTo(rest);
            rest = rest[count..];
        }

        return data;
    }

    /// <summary>Makes two lines of page content with random coordinates.</summary>
    /// <param name="random">The repeatable generator.</param>
    /// <returns>The lines.</returns>
    private static string Line(SampleRandom random)
    {
        var fromX = random.Next(CoordinateX);
        var fromY = random.Next(CoordinateY);
        var toX = random.Next(CoordinateX);
        var toY = random.Next(CoordinateY);
        var textX = random.Next(CoordinateX);
        var textY = random.Next(CoordinateY);
        var number = random.Next(TextNumber);
        return string.Create(CultureInfo.InvariantCulture, $"{fromX} {fromY} m {toX} {toY} l S\nBT /F1 12 Tf {textX} {textY} Td (text {number}) Tj ET\n");
    }

    /// <summary>Makes incompressible bytes.</summary>
    /// <returns>The sample.</returns>
    private static byte[] Noise() => new SampleRandom(Seed).NextBytes(SampleLength);

    /// <summary>Makes runs of one byte with random lengths.</summary>
    /// <returns>The sample.</returns>
    private static byte[] Runs()
    {
        var random = new SampleRandom(Seed);
        var data = new byte[SampleLength];
        var rest = data.AsSpan();
        while (!rest.IsEmpty)
        {
            var count = Math.Min(rest.Length, random.Next(1, MaxRun + 1));
            rest[..count].Fill((byte)random.Next(ByteValues));
            rest = rest[count..];
        }

        return data;
    }
}
