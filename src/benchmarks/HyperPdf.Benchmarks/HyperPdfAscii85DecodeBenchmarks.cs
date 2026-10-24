// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Filters;

namespace HyperPdf.Benchmarks;

/// <summary>Decodes 1 MB of data that was written as ASCII base-85.</summary>
public class HyperPdfAscii85DecodeBenchmarks
{
    /// <summary>The bytes encoded.</summary>
    private const int DataBytes = 1 << 20;

    /// <summary>The seed of the sample bytes.</summary>
    private const uint Seed = 0x2545F491;

    /// <summary>The first xorshift shift.</summary>
    private const int ShiftA = 13;

    /// <summary>The second xorshift shift.</summary>
    private const int ShiftB = 17;

    /// <summary>The third xorshift shift.</summary>
    private const int ShiftC = 5;

    /// <summary>The encoded data.</summary>
    private byte[] _encoded = [];

    /// <summary>Encodes the sample bytes.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var data = new byte[DataBytes];
        var state = Seed;
        for (var i = 0; i < data.Length; i++)
        {
            state ^= state << ShiftA;
            state ^= state >> ShiftB;
            state ^= state << ShiftC;
            data[i] = (byte)state;
        }

        var output = default(PooledBuffer);
        try
        {
            Ascii85Filter.Encode(data, ref output);
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
            _ = Ascii85Filter.TryDecode(_encoded, ref output);
            return output.Length;
        }
        finally
        {
            output.Dispose();
        }
    }
}
