// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signing;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures signing documents with a certificate: the whole signature, the file with room reserved and the byte range patched in,
/// the container made from a signature value, and long-term validation data merged into the security store.
/// </summary>
public class CertificateSigningBenchmarks
{
    /// <summary>The pages in the classic document.</summary>
    private const int Pages = 20;

    /// <summary>The signing certificate.</summary>
    private X509Certificate2 _certificate = null!;

    /// <summary>The signer made from the certificate.</summary>
    private PdfCertificateSigner _signer = null!;

    /// <summary>A document with a classic cross-reference table.</summary>
    private byte[] _classic = [];

    /// <summary>A document with an object stream and a cross-reference stream.</summary>
    private byte[] _compressed = [];

    /// <summary>A document whose cross-reference offsets are all wrong, so signing must rebuild them.</summary>
    private byte[] _damaged = [];

    /// <summary>A signed document to add validation data to.</summary>
    private byte[] _signed = [];

    /// <summary>The validation data added to the signed document.</summary>
    private PdfSignatureValidationData[] _validation = [];

    /// <summary>A prepared signature and the value that completes it.</summary>
    private PdfPendingSignature _pending = null!;

    /// <summary>The signature value over the pending signature's signed attributes.</summary>
    private byte[] _value = [];

    /// <summary>The options of every signature.</summary>
    private PdfSigningOptions _options = null!;

    /// <summary>Creates the certificate and documents.</summary>
    /// <returns>A task.</returns>
    [GlobalSetup]
    public async Task Setup()
    {
        _certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        _signer = new(_certificate);
        _classic = TestPdf.Create(Pages);
        _compressed = TestPdf.CreateCompressed();

        // A comment after the header moves every object, so each offset in the cross-reference table is wrong.
        var header = Array.IndexOf(_classic, (byte)'\n') + 1;
        _damaged = [.. _classic.AsSpan(0, header), .. "% shifted by a damaged copy\n"u8, .. _classic.AsSpan(header)];
        _options = new() { Reason = "Approved", Location = "Brisbane", SigningTime = TimeProvider.System.GetUtcNow() };
        using var classic = PdfDocumentReader.Open(_classic, null);
        _signed = PdfDocumentSigning.Sign(classic, _options, _signer).Bytes;
        _validation = [new([], [_certificate.RawData], [], [])];
        using var document = PdfDocumentReader.Open(_classic, null);
        var identity = PdfSignerIdentity.From(_signer);
        _pending = await PdfDocumentSigning.PrepareAsync(document, _options, identity, CancellationToken.None);
        _value = await _signer.SignAsync(_pending.SignedAttributes, _pending.Digest, CancellationToken.None);
    }

    /// <summary>Releases the certificate.</summary>
    [GlobalCleanup]
    public void Cleanup() => _certificate.Dispose();

    /// <summary>Signs a document with a classic cross-reference table.</summary>
    /// <returns>The signed length.</returns>
    [Benchmark]
    public int SignClassic() => Sign(_classic);

    /// <summary>Signs a damaged document, rebuilding its cross-reference information first.</summary>
    /// <returns>The signed length.</returns>
    [Benchmark]
    public int SignDamaged() => Sign(_damaged);

    /// <summary>Signs a document whose structure is compressed into streams.</summary>
    /// <returns>The signed length.</returns>
    [Benchmark]
    public int SignCompressed() => Sign(_compressed);

    /// <summary>Builds the update with room reserved for the signature, patches the byte range and hashes the signed bytes.</summary>
    /// <returns>The length of the signed attributes.</returns>
    [Benchmark]
    public async Task<int> PrepareWithPlaceholder()
    {
        using var document = PdfDocumentReader.Open(_classic, null);
        var pending = await PdfDocumentSigning.PrepareAsync(document, _options, PdfSignerIdentity.From(_signer), CancellationToken.None);
        return pending.SignedAttributes.Length;
    }

    /// <summary>Makes the signature container from a signature value and writes it into the reserved room.</summary>
    /// <returns>The signed length.</returns>
    [Benchmark]
    public async Task<int> CompleteContainer() => (await PdfDocumentSigning.CompleteAsync(_pending, _value, CancellationToken.None)).Bytes.Length;

    /// <summary>Merges validation data into the security store of a signed document.</summary>
    /// <returns>The updated length.</returns>
    [Benchmark]
    public async Task<int> MergeSecurityStore()
    {
        using var document = PdfDocumentReader.Open(_signed, null);
        return (await PdfDocumentSigning.AddValidationDataAsync(document, _validation, true, CancellationToken.None)).Bytes.Length;
    }

    /// <summary>Signs a document.</summary>
    /// <param name="file">The document.</param>
    /// <returns>The signed length.</returns>
    private int Sign(byte[] file)
    {
        using var document = PdfDocumentReader.Open(file, null);
        return PdfDocumentSigning.Sign(document, _options, _signer).Bytes.Length;
    }
}
