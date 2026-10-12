// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Signatures.Signing;
using HyperPdf.TestAssets;

namespace HyperPdf.Benchmarks;

/// <summary>Measures signing documents with a certificate; allocations come from the EventPipe trace.</summary>
public class CertificateSigningBenchmarks
{
    /// <summary>The pages in the classic document.</summary>
    private const int Pages = 20;

    /// <summary>The signing certificate.</summary>
    private X509Certificate2 _certificate = null!;

    /// <summary>A document with a classic cross-reference table.</summary>
    private byte[] _classic = [];

    /// <summary>A document with an object stream and a cross-reference stream.</summary>
    private byte[] _compressed = [];

    /// <summary>A document whose cross-reference offsets are all wrong, so signing must rebuild them.</summary>
    private byte[] _damaged = [];

    /// <summary>The signing request.</summary>
    private SigningRequest _request;

    /// <summary>Creates the certificate and documents.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        _classic = TestPdf.Create(Pages);
        _compressed = TestPdf.CreateCompressed();

        // A comment after the header moves every object, so each offset in the cross-reference table is wrong.
        var header = Array.IndexOf(_classic, (byte)'\n') + 1;
        _damaged = [.. _classic.AsSpan(0, header), .. "% shifted by a damaged copy\n"u8, .. _classic.AsSpan(header)];
        _request = new(0, "Approved", "Brisbane", TimeProvider.System.GetUtcNow());
    }

    /// <summary>Releases the certificate.</summary>
    [GlobalCleanup]
    public void Cleanup() => _certificate.Dispose();

    /// <summary>Signs a document with a classic cross-reference table.</summary>
    /// <returns>The signed length.</returns>
    [Benchmark]
    public int SignClassic() => PdfSigner.Sign(_classic, _certificate, _request).Length;

    /// <summary>Signs a damaged document, rebuilding its cross-reference information first.</summary>
    /// <returns>The signed length.</returns>
    [Benchmark]
    public int SignDamaged() => PdfSigner.Sign(_damaged, _certificate, _request).Length;

    /// <summary>Signs a document whose structure is compressed into streams.</summary>
    /// <returns>The signed length.</returns>
    [Benchmark]
    public int SignCompressed() => PdfSigner.Sign(_compressed, _certificate, _request).Length;
}
