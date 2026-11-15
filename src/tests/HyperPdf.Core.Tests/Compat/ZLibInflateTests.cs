// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;
using HyperPdf.Core.Compat;

namespace HyperPdf.Core.Tests.Compat;

/// <summary>The zlib helper gives the same result on .NET 10 and .NET 11: whole data, damaged data and data that stops early.</summary>
public sealed class ZLibInflateTests
{
    /// <summary>The bytes of the sample, enough to make the first output buffer grow more than once.</summary>
    private const int SampleBytes = 200_000;

    /// <summary>The length of the pattern the sample repeats, so it compresses far below a quarter of its size.</summary>
    private const int PatternBytes = 37;

    /// <summary>Whole data comes back as it went in, however much larger it is than its compressed form.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task WholeDataIsInflated()
    {
        var sample = CreateSample();
        var packed = Deflate(sample);

        var inflated = ZLibInflate.ToArray(packed);

        await Assert.That(packed.Length * PatternBytes).IsLessThan(sample.Length);
        await Assert.That(inflated.AsSpan().SequenceEqual(sample)).IsTrue();
    }

    /// <summary>Nothing compressed inflates to nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmptyDataIsInflated()
    {
        var inflated = ZLibInflate.ToArray(Deflate([]));

        await Assert.That(inflated.Length).IsEqualTo(0);
    }

    /// <summary>Data whose header is damaged is refused with the same exception on both frameworks.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedDataIsRefused()
    {
        var packed = Deflate(CreateSample());

        // No zlib stream starts with a zero byte: its low four bits name the compression method, and only method 8 exists.
        packed[0] = 0;
        var refused = false;
        try
        {
            _ = ZLibInflate.ToArray(packed);
        }
        catch (InvalidDataException)
        {
            refused = true;
        }

        await Assert.That(refused).IsTrue();
    }

    /// <summary>Data that stops early gives back the start of what it held.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DataThatStopsEarlyGivesItsStart()
    {
        var sample = CreateSample();
        var packed = Deflate(sample);

        var inflated = ZLibInflate.ToArray(packed.AsSpan(0, packed.Length >> 1));

        await Assert.That(inflated.Length).IsLessThan(sample.Length);
        await Assert.That(sample.AsSpan().StartsWith(inflated)).IsTrue();
    }

    /// <summary>Makes bytes that repeat a short pattern.</summary>
    /// <returns>The bytes.</returns>
    private static byte[] CreateSample()
    {
        var sample = new byte[SampleBytes];
        for (var i = 0; i < sample.Length; i++)
        {
            sample[i] = (byte)(i % PatternBytes);
        }

        return sample;
    }

    /// <summary>Compresses bytes as zlib data.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The compressed bytes.</returns>
    private static byte[] Deflate(byte[] data)
    {
        using var output = new MemoryStream();
        using (var deflater = new ZLibStream(output, CompressionLevel.Optimal, true))
        {
            deflater.Write(data);
        }

        return output.ToArray();
    }
}
