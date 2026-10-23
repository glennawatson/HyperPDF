// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdf.Benchmarks;

/// <summary>Asks the font stream cache for a 1 MB font program it already holds, as saving a second document that embeds the same font does.</summary>
public class HyperPdfFontStreamCacheBenchmarks
{
    /// <summary>The bytes of the font program.</summary>
    private const int FontBytes = 1 << 20;

    /// <summary>The most programs kept.</summary>
    private const int MaxEntries = 16;

    /// <summary>The most bytes kept.</summary>
    private const long MaxBytes = 8L << 20;

    /// <summary>The font program.</summary>
    private byte[] _font = [];

    /// <summary>The cache.</summary>
    private FontStreamCacheState _state = FontStreamCacheState.Empty;

    /// <summary>Builds the font and fills the cache outside the measured operations.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _font = new byte[FontBytes];
        RandomNumberGenerator.Fill(_font);
        _ = FontStreamCache.GetOrCompress(ref _state, _font, MaxEntries, MaxBytes);
    }

    /// <summary>Finds the program in the cache.</summary>
    /// <returns>The length of the cached stream.</returns>
    [Benchmark]
    public int FindCachedProgram() => FontStreamCache.GetOrCompress(ref _state, _font, MaxEntries, MaxBytes).Length;
}
