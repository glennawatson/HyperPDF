// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using BenchmarkDotNet.Attributes;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Tests.Signatures;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures validating one signature of an open document with SHA-256, SHA3-256 and SHAKE256 digests, from OpenSSL's
/// signatures over the same signed bytes; allocations come from the EventPipe trace.
/// </summary>
public class Sha3SignatureValidationBenchmarks
{
    /// <summary>The RSA signer, trusted as a root.</summary>
    private X509Certificate2 _rsa = null!;

    /// <summary>OpenSSL's RSA signer, trusted as a root.</summary>
    private X509Certificate2 _openSslRsa = null!;

    /// <summary>The P-256 signer, trusted as a root.</summary>
    private X509Certificate2 _p256 = null!;

    /// <summary>The document with a SHA-256 signature.</summary>
    private PdfDocument _sha256 = null!;

    /// <summary>The document with OpenSSL's RSA SHA3-256 signature.</summary>
    private PdfDocument _sha3 = null!;

    /// <summary>The document with OpenSSL's P-256 SHAKE256 signature.</summary>
    private PdfDocument _shake = null!;

    /// <summary>Validation trusting the RSA signer.</summary>
    private PdfSignatureValidationOptions _rsaOptions = null!;

    /// <summary>Validation trusting the P-256 signer.</summary>
    private PdfSignatureValidationOptions _p256Options = null!;

    /// <summary>Opens the documents.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _rsa = PdfSigning.CreateCertificate("Benchmark Signer", null, false, TimeProvider.System);
        _p256 = Sha3Signatures.P256Certificate();
        _openSslRsa = Sha3Signatures.RsaCertificate();
        var prepared = Sha3Signatures.Prepare();
        _sha256 = PdfDocumentReader.Open(PdfSigning.WriteContents(prepared, PdfSigning.CreateCms(prepared.SignedBytes, _rsa)), null);
        _sha3 = PdfDocumentReader.Open(Sha3Signatures.Sign(Sha3Signatures.RsaSha3x256), null);
        _shake = PdfDocumentReader.Open(Sha3Signatures.Sign(Sha3Signatures.P256Shake256), null);
        _rsaOptions = new() { TrustedRoots = [_rsa, _openSslRsa] };
        _p256Options = new() { TrustedRoots = [_p256] };
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _sha256.Dispose();
        _sha3.Dispose();
        _shake.Dispose();
        _rsa.Dispose();
        _openSslRsa.Dispose();
        _p256.Dispose();
    }

    /// <summary>Validates the SHA-256 signature, the baseline.</summary>
    /// <returns>Whether it is valid.</returns>
    [Benchmark(Baseline = true)]
    public bool ValidateSha256() => PdfDocumentSignatureValidation.ValidateSignatures(_sha256, _rsaOptions)[0].SignatureValid;

    /// <summary>Validates the SHA3-256 signature.</summary>
    /// <returns>Whether it is valid.</returns>
    [Benchmark]
    public bool ValidateSha3x256() => PdfDocumentSignatureValidation.ValidateSignatures(_sha3, _rsaOptions)[0].SignatureValid;

    /// <summary>Validates the SHAKE256 signature, checked without SignedCms.</summary>
    /// <returns>Whether it is valid.</returns>
    [Benchmark]
    public bool ValidateShake256() => PdfDocumentSignatureValidation.ValidateSignatures(_shake, _p256Options)[0].SignatureValid;
}
