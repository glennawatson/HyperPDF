// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Filters;

namespace HyperPdf.Benchmarks;

/// <summary>Encodes and decodes a megabyte of compressible data with the LZW filter that older files use for page content and images.</summary>
public class HyperPdfLzwBenchmarks
{
    /// <summary>The bytes of data.</summary>
    private const int DataBytes = 1 << 20;

    /// <summary>The length of the repeating pattern.</summary>
    private const int PatternBytes = 97;

    /// <summary>The distance between changed bytes.</summary>
    private const int NoiseEvery = 31;

    /// <summary>The early change value PDF files use.</summary>
    private const int EarlyChange = 1;

    /// <summary>The plain data.</summary>
    private byte[] _plain = [];

    /// <summary>The encoded data.</summary>
    private byte[] _encoded = [];

    /// <summary>Builds and encodes the data outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _plain = new byte[DataBytes];
        for (var i = 0; i < _plain.Length; i++)
        {
            _plain[i] = (byte)((i % PatternBytes) + (i % NoiseEvery == 0 ? i / PatternBytes : 0));
        }

        var output = default(PooledBuffer);
        try
        {
            LzwEncoder.Encode(_plain, EarlyChange, ref output);
            _encoded = output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Decodes the data.</summary>
    /// <returns>The decoded length.</returns>
    [Benchmark]
    public int Decode()
    {
        var output = default(PooledBuffer);
        try
        {
            LzwFilter.Decode(_encoded, EarlyChange, ref output);
            return output.Length;
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Encodes the data.</summary>
    /// <returns>The encoded length.</returns>
    [Benchmark]
    public int Encode()
    {
        var output = default(PooledBuffer);
        try
        {
            LzwEncoder.Encode(_plain, EarlyChange, ref output);
            return output.Length;
        }
        finally
        {
            output.Dispose();
        }
    }
}
