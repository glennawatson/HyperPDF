// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Tests.Signatures;

/// <summary>Tests that <see cref="PdfRevocationChecker"/> reads the status a signed OCSP response gives a certificate, and only trusts a response its issuer signed.</summary>
public sealed class RevocationCheckerTests : IDisposable
{
    /// <summary>The first byte of the serial numbers, clear of the sign bit.</summary>
    private const byte SerialPrefix = 0x10;

    /// <summary>The days a certificate is valid for.</summary>
    private const int ValidDays = 30;

    /// <summary>The second serial number byte of the second certificate.</summary>
    private const byte SecondSerial = 2;

    /// <summary>The issuer's key.</summary>
    private readonly ECDsa _issuerKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);

    /// <summary>The issuing certificate.</summary>
    private readonly X509Certificate2 _issuer;

    /// <summary>The first certificate.</summary>
    private readonly X509Certificate2 _first;

    /// <summary>The second certificate.</summary>
    private readonly X509Certificate2 _second;

    /// <summary>When the responses were produced.</summary>
    private readonly DateTimeOffset _at = TimeProvider.System.GetUtcNow().AddDays(-1);

    /// <summary>Initializes a new instance of the <see cref="RevocationCheckerTests"/> class.</summary>
    public RevocationCheckerTests()
    {
        var authority = new CertificateRequest("CN=Test Authority", _issuerKey, HashAlgorithmName.SHA256);
        authority.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        _issuer = authority.CreateSelfSigned(_at, _at.AddDays(ValidDays));
        _first = Leaf("First", 1);
        _second = Leaf("Second", SecondSerial);
    }

    /// <summary>Verifies a signed response that says a certificate is good gives not revoked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GoodResponseGivesNotRevoked() =>
        await Assert.That(PdfRevocationChecker.Check(_first, _issuer, Store(Response(_first, false)))).IsEqualTo(PdfRevocationStatus.NotRevoked);

    /// <summary>Verifies a signed response that says a certificate is revoked gives revoked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RevokedResponseGivesRevoked() =>
        await Assert.That(PdfRevocationChecker.Check(_first, _issuer, Store(Response(_first, true)))).IsEqualTo(PdfRevocationStatus.Revoked);

    /// <summary>Verifies a response about another certificate says nothing about this one, and the right response is still found among several.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OnlyTheResponseAboutTheCertificateCounts()
    {
        var store = Store(Response(_first, false), Response(_second, true));

        await Assert.That(PdfRevocationChecker.Check(_first, _issuer, store)).IsEqualTo(PdfRevocationStatus.NotRevoked);
        await Assert.That(PdfRevocationChecker.Check(_second, _issuer, store)).IsEqualTo(PdfRevocationStatus.Revoked);
        await Assert.That(PdfRevocationChecker.Check(_second, _issuer, Store(Response(_first, false)))).IsEqualTo(PdfRevocationStatus.Unknown);
    }

    /// <summary>Verifies a response about the certificate whose signature is damaged is not trusted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedSignatureGivesUnknown()
    {
        var response = Response(_first, true);
        response[^1] ^= 1;

        await Assert.That(PdfRevocationChecker.Check(_first, _issuer, Store(response))).IsEqualTo(PdfRevocationStatus.Unknown);
    }

    /// <summary>Verifies a response signed by another key is not trusted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResponseSignedByAnotherKeyGivesUnknown()
    {
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var response = OcspResponses.Build(other, _issuer, _first, _at, true);

        await Assert.That(PdfRevocationChecker.Check(_first, _issuer, Store(response))).IsEqualTo(PdfRevocationStatus.Unknown);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _first.Dispose();
        _second.Dispose();
        _issuer.Dispose();
        _issuerKey.Dispose();
    }

    /// <summary>Makes a certificate the issuer signed.</summary>
    /// <param name="name">The common name.</param>
    /// <param name="serial">The last byte of the serial number.</param>
    /// <returns>The certificate.</returns>
    private X509Certificate2 Leaf(string name, byte serial)
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = new CertificateRequest($"CN={name}", key, HashAlgorithmName.SHA256);
        return request.Create(_issuer, _at, _at.AddDays(ValidDays), [SerialPrefix, serial]);
    }

    /// <summary>Builds the issuer's response about a certificate.</summary>
    /// <param name="subject">The certificate.</param>
    /// <param name="revoked">Whether it says revoked.</param>
    /// <returns>The DER response.</returns>
    private byte[] Response(X509Certificate2 subject, bool revoked) => OcspResponses.Build(_issuerKey, _issuer, subject, _at, revoked);

    /// <summary>Puts responses in a store.</summary>
    /// <param name="responses">The responses.</param>
    /// <returns>The store.</returns>
    private PdfSecurityStore Store(params byte[][] responses) =>
        new([_issuer.RawData], responses, [], new ReadOnlyDictionary<string, PdfValidationRelatedInfo>(new Dictionary<string, PdfValidationRelatedInfo>(StringComparer.Ordinal)));
}
