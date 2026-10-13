// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using HyperPdf.Core.Signatures;
using HyperPdf.Core.Signatures.Signing;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Tests.Signatures;

/// <summary>
/// Tests for the viewer's signer with SHA-3 and SHAKE256 digests: each variant reopens and validates, and the file
/// records the ISO/TS 32001 developer extension and PDF 2.0 (ISO/TS 32001:2022, clause 4; ISO 32000-2, 7.12).
/// </summary>
public sealed class Sha3SigningTests
{
    /// <summary>The common name of the elliptic-curve signer.</summary>
    private const string EcdsaSigner = "SHA-3 Signer";

    /// <summary>The ISO/TS 32001 extension level.</summary>
    private const int HashExtensionLevel = 32_001;

    /// <summary>The ISO/TS 32002 extension level, which an elliptic-curve signer also records.</summary>
    private const int CurveExtensionLevel = 32_002;

    /// <summary>The level of another ISO technical specification (ISO/TS 32004) already in the merge sample.</summary>
    private const int OtherIsoLevel = 32_004;

    /// <summary>The ADBE extension level in the merge sample.</summary>
    private const int AdobeLevel = 3;

    /// <summary>The ISO_ entries after adding to a file that already records another ISO extension.</summary>
    private const int IsoEntries = 2;

    /// <summary>Each digest signs, reopens and validates in the library and the viewer, with the extension recorded.</summary>
    /// <param name="digest">The digest.</param>
    /// <param name="expected">The digest the library reads back.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(SigningDigest.Sha3_256, PdfDigestMethod.Sha3_256)]
    [Arguments(SigningDigest.Sha3_384, PdfDigestMethod.Sha3_384)]
    [Arguments(SigningDigest.Sha3_512, PdfDigestMethod.Sha3_512)]
    [Arguments(SigningDigest.Shake256, PdfDigestMethod.Shake256)]
    public async Task EachDigestSignsAndValidates(SigningDigest digest, PdfDigestMethod expected)
    {
        Sha3SignatureValidationTests.RequireDigest(expected);
        using var ecdsa = ShakeTestSigner.CreateEcdsaCertificate(EcdsaSigner);
        var certificate = digest == SigningDigest.Shake256 ? ecdsa : SignatureFixtures.Signer;
        var file = Sign(SignatureFixtures.Blank(), certificate, digest);
        var report = SignatureFixtures.Validate(file, certificate)[0];

        using (Assert.Multiple())
        {
            await Assert.That(report.Cms.Status).IsEqualTo(PdfCmsStatus.Checked);
            await Assert.That(report.DigestValid).IsTrue();
            await Assert.That(report.SignatureValid).IsTrue();
            await Assert.That(report.Cms.Digest).IsEqualTo(expected);
            await Assert.That(report.CoversWholeDocument).IsTrue();
            await Assert.That(IsoLevels(file)).IsEquivalentTo(ExpectedLevels(digest));
            await Assert.That(CatalogVersion(file)).IsEqualTo("2.0");
            await Assert.That(SignatureFixtures.ViewerCheck(file, certificate).Integrity).IsEqualTo(SignatureIntegrity.Intact);
        }
    }

    /// <summary>A SHAKE256 signature, which the signer writes itself, takes a timestamp token like any other.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShakeSignatureTakesTimestamp()
    {
        Sha3SignatureValidationTests.RequireDigest(PdfDigestMethod.Shake256);
        using var ecdsa = ShakeTestSigner.CreateEcdsaCertificate(EcdsaSigner);
        using var authority = new TestTimestampAuthority(PdfSigning.SigningTime);
        var options = new HyperPdfLibrary.Signing.PdfSigningOptions
        {
            Reason = "Approved",
            Digest = PdfDigestMethod.Shake256,
            Timestamp = new HyperPdfLibrary.Tests.Signing.TimestamperClient(authority),
        };
        var file = (await HyperPdfLibrary.Tests.Signing.SigningFixtures.SignResultAsync(SignatureFixtures.Blank(), ecdsa, options)).Bytes;
        using var document = PdfDocumentReader.Open(file, null);
        var report = PdfDocumentSignatureValidation.ValidateSignatures(document, new() { TrustedRoots = [ecdsa, authority.Certificate] })[0];

        await Assert.That(report.SignatureValid).IsTrue();
        await Assert.That(report.DigestValid).IsTrue();
        await Assert.That(report.TimestampValid).IsTrue();
        await Assert.That(authority.Issued).IsEqualTo(1);
    }

    /// <summary>A SHA3-256 signature on a file with a SHAKE256 signature keeps one extension entry and both signatures.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SecondSignatureKeepsOneEntry()
    {
        Sha3SignatureValidationTests.RequireDigest(PdfDigestMethod.Shake256);
        using var ecdsa = ShakeTestSigner.CreateEcdsaCertificate(EcdsaSigner);
        var first = Sign(SignatureFixtures.Blank(), ecdsa, SigningDigest.Shake256);
        var second = Sign(first, SignatureFixtures.Signer, SigningDigest.Sha3_256);
        using var document = PdfDocumentReader.Open(second, null);
        var reports = PdfDocumentSignatureValidation.ValidateSignatures(document, new() { TrustedRoots = [ecdsa, SignatureFixtures.Signer] });

        await Assert.That(IsoLevels(second)).IsEquivalentTo([HashExtensionLevel, CurveExtensionLevel]);
        await Assert.That(reports[0].SignatureValid).IsTrue();
        await Assert.That(reports[1].SignatureValid).IsTrue();
    }

    /// <summary>Another developer's extension is kept, and an ISO_ entry for another standard becomes an array with both.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExistingExtensionsAreMerged()
    {
        Sha3SignatureValidationTests.RequireDigest(PdfDigestMethod.Sha3_256);
        var source = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /Extensions << /ADBE << /BaseVersion /1.7 /ExtensionLevel 3 >> /ISO_ << /BaseVersion /2.0 /ExtensionLevel 32004 >> >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] >>");
        var file = Sign(source, SignatureFixtures.Signer, SigningDigest.Sha3_256);
        using var document = PdfDocumentReader.Open(file, null);
        var extensions = document.Catalog.GetDictionary(KnownName.Extensions)!;
        var adobe = extensions.GetDictionary(document.Objects.Names.Intern("ADBE"u8));

        await Assert.That(adobe!.GetInt32(document.Objects.Names.Intern("ExtensionLevel"u8), 0)).IsEqualTo(AdobeLevel);
        await Assert.That(IsoLevels(file).Length).IsEqualTo(IsoEntries);
        await Assert.That(IsoLevels(file)).IsEquivalentTo([OtherIsoLevel, HashExtensionLevel]);
        await Assert.That(SignatureFixtures.ValidateSingle(file).SignatureValid).IsTrue();
    }

    /// <summary>The default SHA-256 signature adds no extension and leaves the version alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Sha256AddsNoExtension()
    {
        var file = Sign(SignatureFixtures.Blank(), SignatureFixtures.Signer, SigningDigest.Sha256);

        await Assert.That(IsoLevels(file).Length).IsEqualTo(0);
        await Assert.That(CatalogVersion(file)).IsEqualTo(string.Empty);
        await Assert.That(SignatureFixtures.ValidateSingle(file).Cms.Digest).IsEqualTo(PdfDigestMethod.Sha256);
    }

    /// <summary>SHAKE256 with an RSA certificate is refused with a plain reason, since only ECDSA can sign it here.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShakeWithRsaIsRefused()
    {
        Sha3SignatureValidationTests.RequireDigest(PdfDigestMethod.Shake256);
        var exception = Assert.Throws<HyperPdfLibrary.Signing.PdfSigningRefusedException>(static () => Sign(SignatureFixtures.Blank(), SignatureFixtures.Signer, SigningDigest.Shake256));

        await Assert.That(exception!.Message).Contains("elliptic-curve");
    }

    /// <summary>Reads the /ExtensionLevel of each ISO_ developer extensions dictionary in the catalog.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The levels.</returns>
    internal static int[] IsoLevels(byte[] file)
    {
        using var document = PdfDocumentReader.Open(file, null);
        var names = document.Objects.Names;
        var level = names.Intern("ExtensionLevel"u8);
        var extensions = document.Catalog.GetDictionary(KnownName.Extensions);
        var iso = extensions?.Get(names.Intern("ISO_"u8)) ?? default;
        if (iso.AsDictionary() is { } single)
        {
            return [single.GetInt32(level, 0)];
        }

        var array = iso.AsArray();
        var levels = new int[array?.Count ?? 0];
        for (var i = 0; i < levels.Length; i++)
        {
            levels[i] = array!.GetDictionary(i)?.GetInt32(level, 0) ?? 0;
        }

        return levels;
    }

    /// <summary>Reads the catalog's /Version.</summary>
    /// <param name="file">The file.</param>
    /// <returns>The version, or an empty string.</returns>
    internal static string CatalogVersion(byte[] file)
    {
        using var document = PdfDocumentReader.Open(file, null);
        var version = document.Catalog.GetName(KnownName.Version);
        return version.IsNone ? string.Empty : document.Objects.Names.GetString(version);
    }

    /// <summary>Signs a file with a digest.</summary>
    /// <param name="source">The file.</param>
    /// <param name="certificate">The signer.</param>
    /// <param name="digest">The digest.</param>
    /// <returns>The signed file.</returns>
    private static byte[] Sign(byte[] source, X509Certificate2 certificate, SigningDigest digest) =>
        HyperPdfLibrary.Tests.Signing.SigningFixtures.SignWithDigest(source, certificate, digest);

    /// <summary>Gets the extension levels a file signed with a digest records: SHAKE256 needs an elliptic-curve signer.</summary>
    /// <param name="digest">The digest.</param>
    /// <returns>The levels.</returns>
    private static int[] ExpectedLevels(SigningDigest digest) =>
        digest == SigningDigest.Shake256 ? [HashExtensionLevel, CurveExtensionLevel] : [HashExtensionLevel];
}
