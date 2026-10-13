// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdfLibrary.Signing;

namespace HyperPdfLibrary.Tests.Signing;

/// <summary>Tests that <see cref="PdfSignerCertificate"/> reads from the certificate bytes what the platform certificate object reports.</summary>
public sealed class PdfSignerCertificateTests
{
    /// <summary>The size of the RSA test key in bits.</summary>
    private const int RsaBits = 2048;

    /// <summary>The size of a larger RSA test key in bits.</summary>
    private const int LargeRsaBits = 3072;

    /// <summary>The size of the P-256 field in bits.</summary>
    private const int P256Bits = 256;

    /// <summary>The size of the P-384 field in bits.</summary>
    private const int P384Bits = 384;

    /// <summary>The size of the P-521 field in bits.</summary>
    private const int P521Bits = 521;

    /// <summary>The days a test certificate is valid for.</summary>
    private const int ValidDays = 30;

    /// <summary>The common name of the test certificates.</summary>
    private const string CommonName = "Ünïcode Signer";

    /// <summary>Reading an RSA certificate gives the issuer, serial number, key size and common name the platform reports.</summary>
    /// <param name="bits">The key size.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(RsaBits)]
    [Arguments(LargeRsaBits)]
    public async Task RsaCertificateMatchesThePlatform(int bits)
    {
        using var key = RSA.Create(bits);
        var request = new CertificateRequest(new X500DistinguishedName($"CN={CommonName}, O=Org"), key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = SelfSign(request);

        await CheckAgainstPlatform(certificate, PdfSignerKeyKind.Rsa, bits);
    }

    /// <summary>Reading an elliptic-curve certificate gives the curve parameters and field size the platform reports.</summary>
    /// <param name="curveName">The curve.</param>
    /// <param name="bits">The size of the curve's field in bits.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("nistP256", P256Bits)]
    [Arguments("nistP384", P384Bits)]
    [Arguments("nistP521", P521Bits)]
    public async Task EllipticCurveCertificateMatchesThePlatform(string curveName, int bits)
    {
        using var key = ECDsa.Create(ECCurve.CreateFromFriendlyName(curveName));
        var request = new CertificateRequest(new X500DistinguishedName($"CN={CommonName}"), key, HashAlgorithmName.SHA256);
        using var certificate = SelfSign(request);

        await CheckAgainstPlatform(certificate, PdfSignerKeyKind.EllipticCurve, bits);
        await Assert.That(PdfSignerCertificate.Read(certificate.RawData).Parameters.SequenceEqual(certificate.PublicKey.EncodedParameters!.RawData)).IsTrue();
    }

    /// <summary>A subject without a common name has none to read, so the caller asks the platform.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SubjectWithoutCommonNameHasNone()
    {
        using var key = RSA.Create(RsaBits);
        var request = new CertificateRequest(new X500DistinguishedName("O=Org, OU=Unit"), key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = SelfSign(request);

        await Assert.That(PdfSignerCertificate.Read(certificate.RawData).CommonName()).IsNull();
    }

    /// <summary>Bytes that are not a certificate are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedBytesAreRefused()
    {
        byte[] damaged = [0x30, 0x03, 0x02, 0x01, 0x05];

        await Assert.That(() => PdfSignerCertificate.Read(damaged)).Throws<CryptographicException>();
    }

    /// <summary>Makes a self-signed certificate that is valid now.</summary>
    /// <param name="request">The request.</param>
    /// <returns>The certificate.</returns>
    private static X509Certificate2 SelfSign(CertificateRequest request)
    {
        var now = TimeProvider.System.GetUtcNow();
        return request.CreateSelfSigned(now.AddDays(-1), now.AddDays(ValidDays));
    }

    /// <summary>Compares what is read from the bytes with the platform's view of the same certificate.</summary>
    /// <param name="certificate">The certificate.</param>
    /// <param name="kind">The kind of key expected.</param>
    /// <param name="bits">The key size expected.</param>
    /// <returns>A task.</returns>
    private static async Task CheckAgainstPlatform(X509Certificate2 certificate, PdfSignerKeyKind kind, int bits)
    {
        var parts = PdfSignerCertificate.Read(certificate.RawData);
        using (Assert.Multiple())
        {
            await Assert.That(parts.IssuerName.SequenceEqual(certificate.IssuerName.RawData)).IsTrue();
            await Assert.That(parts.SerialNumber.SequenceEqual(certificate.SerialNumberBytes.Span)).IsTrue();
            await Assert.That(parts.KeyKind).IsEqualTo(kind);
            await Assert.That(parts.KeyBits).IsEqualTo(bits);
            await Assert.That(parts.CommonName()).IsEqualTo(certificate.GetNameInfo(X509NameType.SimpleName, false));
        }
    }
}
