// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.Core.Signatures;
using HyperPdf.Core.Signatures.Signing;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Tests.Signatures;

/// <summary>
/// Tests for the viewer's signer with elliptic-curve certificates: each permitted curve and digest pair reopens and
/// validates, the file records the ISO/TS 32002 developer extension and PDF 2.0 (clause 4), and a pair the specification
/// does not permit is refused before anything is written.
/// </summary>
public sealed class EcSigningTests
{
    /// <summary>The ISO/TS 32002 extension level.</summary>
    private const int CurveExtensionLevel = 32_002;

    /// <summary>The ISO/TS 32001 extension level.</summary>
    private const int HashExtensionLevel = 32_001;

    /// <summary>Each permitted pair signs, reopens and validates in the library and the viewer, with the extension recorded.</summary>
    /// <param name="curve">The curve's name.</param>
    /// <param name="digest">The digest to sign with.</param>
    /// <param name="expected">The digest the library reads back.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("nistP256", SigningDigest.Sha256, PdfDigestMethod.Sha256)]
    [Arguments("nistP384", SigningDigest.Sha384, PdfDigestMethod.Sha384)]
    [Arguments("nistP521", SigningDigest.Sha512, PdfDigestMethod.Sha512)]
    [Arguments("brainpoolP256r1", SigningDigest.Sha256, PdfDigestMethod.Sha256)]
    [Arguments("brainpoolP256r1", SigningDigest.Sha512, PdfDigestMethod.Sha512)]
    [Arguments("brainpoolP384r1", SigningDigest.Sha384, PdfDigestMethod.Sha384)]
    [Arguments("brainpoolP512r1", SigningDigest.Sha512, PdfDigestMethod.Sha512)]
    public async Task PermittedPairSignsAndValidates(string curve, SigningDigest digest, PdfDigestMethod expected)
    {
        RequireCurve(curve);
        using var certificate = Create(curve);
        var file = Sign(certificate, digest);
        var report = SignatureFixtures.Validate(file, certificate)[0];

        using (Assert.Multiple())
        {
            await Assert.That(report.Cms.Status).IsEqualTo(PdfCmsStatus.Checked);
            await Assert.That(report.DigestValid).IsTrue();
            await Assert.That(report.SignatureValid).IsTrue();
            await Assert.That(report.Cms.Digest).IsEqualTo(expected);
            await Assert.That(IsoLevels(file)).IsEquivalentTo([CurveExtensionLevel]);
            await Assert.That(CatalogVersion(file)).IsEqualTo("2.0");
            await Assert.That(SignatureFixtures.ViewerCheck(file, certificate).Integrity).IsEqualTo(SignatureIntegrity.Intact);
        }
    }

    /// <summary>A SHAKE256 signature with a curve keeps both the hash and the curve extension.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShakeSignatureRecordsBothExtensions()
    {
        Sha3SignatureValidationTests.RequireDigest(PdfDigestMethod.Shake256);
        using var certificate = Create("nistP256");
        var file = Sign(certificate, SigningDigest.Shake256);

        using (Assert.Multiple())
        {
            await Assert.That(IsoLevels(file)).IsEquivalentTo([HashExtensionLevel, CurveExtensionLevel]);
            await Assert.That(SignatureFixtures.ValidateSingle(file).Cms.Status).IsEqualTo(PdfCmsStatus.Checked);
            await Assert.That(SignatureFixtures.Validate(file, certificate)[0].SignatureValid).IsTrue();
        }
    }

    /// <summary>A curve and digest the specification does not pair are refused with a plain reason, and nothing is written.</summary>
    /// <param name="curve">The curve's name.</param>
    /// <param name="digest">The digest.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("nistP384", SigningDigest.Sha256)]
    [Arguments("nistP256", SigningDigest.Sha512)]
    [Arguments("nistP521", SigningDigest.Sha256)]
    public async Task DisallowedPairIsRefused(string curve, SigningDigest digest)
    {
        using var certificate = Create(curve);
        var exception = Assert.Throws<HyperPdfLibrary.Signing.PdfSigningRefusedException>(() => Sign(certificate, digest));

        await Assert.That(exception!.Message).Contains("cannot sign with");
    }

    /// <summary>An RSA certificate is not an elliptic-curve signer, so it adds no ISO/TS 32002 extension.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RsaSignatureAddsNoCurveExtension()
    {
        var file = Sign(SignatureFixtures.Signer, SigningDigest.Sha256);

        await Assert.That(IsoLevels(file).Length).IsEqualTo(0);
    }

    /// <summary>Skips a test when the platform lacks a Brainpool curve, saying why.</summary>
    /// <param name="curve">The curve's name.</param>
    private static void RequireCurve(string curve)
    {
        if (curve.StartsWith("brainpool", StringComparison.Ordinal) && !EcSignatures.PlatformHasCurve(curve))
        {
            Skip.Test($"This platform's cryptography library lacks {curve}, so it cannot sign with it.");
        }
    }

    /// <summary>Creates a self-signed certificate on a curve.</summary>
    /// <param name="curve">The .NET name of the curve.</param>
    /// <returns>The certificate with its key.</returns>
    private static X509Certificate2 Create(string curve)
    {
        var named = curve switch
        {
            "nistP256" => ECCurve.NamedCurves.nistP256,
            "nistP384" => ECCurve.NamedCurves.nistP384,
            "nistP521" => ECCurve.NamedCurves.nistP521,
            "brainpoolP256r1" => ECCurve.NamedCurves.brainpoolP256r1,
            "brainpoolP384r1" => ECCurve.NamedCurves.brainpoolP384r1,
            _ => ECCurve.NamedCurves.brainpoolP512r1,
        };
        using var key = ECDsa.Create(named);
        var request = new CertificateRequest($"CN=EC Signer {curve}", key, HashAlgorithmName.SHA512);
        return request.CreateSelfSigned(PdfSigning.SigningTime.AddDays(-1), PdfSigning.SigningTime.AddYears(1));
    }

    /// <summary>Signs a blank file with a digest.</summary>
    /// <param name="certificate">The signer.</param>
    /// <param name="digest">The digest.</param>
    /// <returns>The signed file.</returns>
    private static byte[] Sign(X509Certificate2 certificate, SigningDigest digest) =>
        HyperPdfLibrary.Tests.Signing.SigningFixtures.SignWithDigest(SignatureFixtures.Blank(), certificate, digest);

    /// <summary>Reads the /ExtensionLevel of each ISO_ developer extensions dictionary in the catalog.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The levels.</returns>
    private static int[] IsoLevels(byte[] file) => Sha3SigningTests.IsoLevels(file);

    /// <summary>Reads the catalog's /Version.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The version, or an empty string.</returns>
    private static string CatalogVersion(byte[] file) => Sha3SigningTests.CatalogVersion(file);
}
