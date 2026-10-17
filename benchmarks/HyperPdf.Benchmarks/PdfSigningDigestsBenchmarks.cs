// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Signing;

namespace HyperPdf.Benchmarks;

/// <summary>Measures caller-owned signing contexts and chunked two-range digest input.</summary>
public class PdfSigningDigestsBenchmarks
{
    /// <summary>The signed bytes on each side of a signature reservation.</summary>
    private const int RangeLength = 128 * 1024;

    /// <summary>The longest supported signing digest, SHA-512 or SHAKE256.</summary>
    private const int DigestLength = 64;

    /// <summary>The caller that owns the reusable contexts.</summary>
    private PdfDocument _document = null!;

    /// <summary>The first signed range.</summary>
    private byte[] _first = [];

    /// <summary>The second signed range.</summary>
    private byte[] _second = [];

    /// <summary>The digest output reused by each invocation.</summary>
    private byte[] _digest = [];

    /// <summary>Creates signed ranges and warms the caller's native contexts.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = PdfDocumentReader.Open(TestPdf.Create(1), null);
        _first = new byte[RangeLength];
        _second = new byte[RangeLength];
        _first.AsSpan().Fill(1);
        _digest = new byte[DigestLength];
        _ = HashSha256();
        _ = HashShake256();
    }

    /// <summary>Releases the caller's retained native contexts.</summary>
    [GlobalCleanup]
    public void Cleanup() => _document.Dispose();

    /// <summary>Hashes two ranges with a reusable fixed-length digest context.</summary>
    /// <returns>The digest length.</returns>
    [Benchmark]
    public int HashSha256() =>
        PdfSigningDigests.Hash(PdfSigningDigests.ToCms(PdfDigestMethod.Sha256), _first, _second, _digest, _document.Objects.ScratchPools, CancellationToken.None);

    /// <summary>Hashes two ranges with a reusable variable-length digest context when supported.</summary>
    /// <returns>The digest length, or zero when the platform has no SHAKE256 support.</returns>
    [Benchmark]
    public int HashShake256() =>
        PdfSigningDigests.Hash(PdfSigningDigests.ToCms(PdfDigestMethod.Shake256), _first, _second, _digest, _document.Objects.ScratchPools, CancellationToken.None);
}
