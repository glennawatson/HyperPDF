// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Security;

namespace HyperPdf.Benchmarks;

/// <summary>Hashes a short string and a megabyte with the managed MD5, which every save runs over the whole file to make the document ID.</summary>
public class HyperPdfMd5Benchmarks
{
    /// <summary>The bytes of a short string, as in a password or key step.</summary>
    private const int ShortBytes = 48;

    /// <summary>The bytes of a large file.</summary>
    private const int LargeBytes = 1024 * 1024;

    /// <summary>The short data.</summary>
    private readonly byte[] _short = new byte[ShortBytes];

    /// <summary>The large data.</summary>
    private readonly byte[] _large = new byte[LargeBytes];

    /// <summary>The hash.</summary>
    private readonly byte[] _hash = new byte[Md5.HashLength];

    /// <summary>Fills the data.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _short.AsSpan().Fill(1);
        _large.AsSpan().Fill(1);
    }

    /// <summary>Hashes a short string.</summary>
    /// <returns>A byte of the hash.</returns>
    [Benchmark]
    public byte HashShort()
    {
        _ = Md5.HashData(_short, _hash);
        return _hash[0];
    }

    /// <summary>Hashes a megabyte.</summary>
    /// <returns>A byte of the hash.</returns>
    [Benchmark]
    public byte HashMegabyte()
    {
        _ = Md5.HashData(_large, _hash);
        return _hash[0];
    }
}
