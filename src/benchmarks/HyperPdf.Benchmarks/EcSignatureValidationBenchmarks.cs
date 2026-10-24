// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Tests.Signatures;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures validating one signature of an open document for each ISO/TS 32002 algorithm family, from OpenSSL's
/// signatures over the same signed bytes: NIST P-256 (the baseline, checked by SignedCms), Brainpool, Ed25519 and Ed448
/// (checked by the library's own EdDSA code); allocations come from the EventPipe trace.
/// </summary>
public class EcSignatureValidationBenchmarks
{
    /// <summary>The P-256 signer.</summary>
    private X509Certificate2 _p256 = null!;

    /// <summary>The P-384 signer.</summary>
    private X509Certificate2 _p384 = null!;

    /// <summary>The brainpoolP256r1 signer.</summary>
    private X509Certificate2 _brainpool = null!;

    /// <summary>The Ed25519 signer.</summary>
    private X509Certificate2 _ed25519 = null!;

    /// <summary>The Ed448 signer.</summary>
    private X509Certificate2 _ed448 = null!;

    /// <summary>The document with a P-256 signature.</summary>
    private PdfDocument _p256Document = null!;

    /// <summary>The document with a P-384 signature.</summary>
    private PdfDocument _p384Document = null!;

    /// <summary>The document with a brainpoolP256r1 signature.</summary>
    private PdfDocument _brainpoolDocument = null!;

    /// <summary>The document with an Ed25519 signature.</summary>
    private PdfDocument _ed25519Document = null!;

    /// <summary>The document with an Ed448 signature.</summary>
    private PdfDocument _ed448Document = null!;

    /// <summary>Validation trusting the P-256 signer.</summary>
    private PdfSignatureValidationOptions _p256Options = null!;

    /// <summary>Validation trusting the P-384 signer.</summary>
    private PdfSignatureValidationOptions _p384Options = null!;

    /// <summary>Validation trusting the brainpoolP256r1 signer.</summary>
    private PdfSignatureValidationOptions _brainpoolOptions = null!;

    /// <summary>Validation trusting the Ed25519 signer.</summary>
    private PdfSignatureValidationOptions _ed25519Options = null!;

    /// <summary>Validation trusting the Ed448 signer.</summary>
    private PdfSignatureValidationOptions _ed448Options = null!;

    /// <summary>Opens the documents.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _p256 = EcSignatures.Certificate("p256.crt");
        _p384 = EcSignatures.Certificate("p384.crt");
        _brainpool = EcSignatures.Certificate("bp256.crt");
        _ed25519 = EcSignatures.Certificate("ed25519.crt");
        _ed448 = EcSignatures.Certificate("ed448.crt");
        _p256Document = Open("p256-sha256.p7s");
        _p384Document = Open("p384-sha384.p7s");
        _brainpoolDocument = Open("bp256-sha256.p7s");
        _ed25519Document = Open("ed25519.p7s");
        _ed448Document = Open("ed448.p7s");
        _p256Options = new() { TrustedRoots = [_p256] };
        _p384Options = new() { TrustedRoots = [_p384] };
        _brainpoolOptions = new() { TrustedRoots = [_brainpool] };
        _ed25519Options = new() { TrustedRoots = [_ed25519] };
        _ed448Options = new() { TrustedRoots = [_ed448] };
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _p256Document.Dispose();
        _p384Document.Dispose();
        _brainpoolDocument.Dispose();
        _ed25519Document.Dispose();
        _ed448Document.Dispose();
        _p256.Dispose();
        _p384.Dispose();
        _brainpool.Dispose();
        _ed25519.Dispose();
        _ed448.Dispose();
    }

    /// <summary>Validates the P-256 signature, the baseline.</summary>
    /// <returns>Whether it is valid.</returns>
    [Benchmark(Baseline = true)]
    public bool ValidateP256() => PdfDocumentSignatureValidation.ValidateSignatures(_p256Document, _p256Options)[0].SignatureValid;

    /// <summary>Validates the P-384 signature.</summary>
    /// <returns>Whether it is valid.</returns>
    [Benchmark]
    public bool ValidateP384() => PdfDocumentSignatureValidation.ValidateSignatures(_p384Document, _p384Options)[0].SignatureValid;

    /// <summary>Validates the brainpoolP256r1 signature.</summary>
    /// <returns>Whether it is valid.</returns>
    [Benchmark]
    public bool ValidateBrainpoolP256() => PdfDocumentSignatureValidation.ValidateSignatures(_brainpoolDocument, _brainpoolOptions)[0].SignatureValid;

    /// <summary>Validates the Ed25519 signature, checked without SignedCms.</summary>
    /// <returns>Whether it is valid.</returns>
    [Benchmark]
    public bool ValidateEd25519() => PdfDocumentSignatureValidation.ValidateSignatures(_ed25519Document, _ed25519Options)[0].SignatureValid;

    /// <summary>Validates the Ed448 signature, checked without SignedCms.</summary>
    /// <returns>Whether it is valid.</returns>
    [Benchmark]
    public bool ValidateEd448() => PdfDocumentSignatureValidation.ValidateSignatures(_ed448Document, _ed448Options)[0].SignatureValid;

    /// <summary>Opens the signed document for a signature file.</summary>
    /// <param name="name">The signature file.</param>
    /// <returns>The document.</returns>
    private static PdfDocument Open(string name) => PdfDocumentReader.Open(EcSignatures.Sign(EcSignatures.Signature(name)), null);
}
