// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.Core.Signatures;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Tests.Signatures;

/// <summary>
/// Tests for SHA-3 and SHAKE256 signatures and timestamps (ISO/TS 32001:2022): independently made signatures validate,
/// a one-byte change fails, SHAKE256 is accepted only in its 512-bit forms, and a digest that cannot be computed gives
/// an unsupported result rather than a failed or valid one.
/// </summary>
public sealed class Sha3SignatureValidationTests
{
    /// <summary>The id-shake256-len output length PDF allows.</summary>
    private const int Shake256Bits = 512;

    /// <summary>A SHAKE256 output length PDF does not allow.</summary>
    private const int ShortShakeBits = 256;

    /// <summary>The MD5 OID, a digest the checker does not support.</summary>
    private const string Md5Oid = "1.2.840.113549.2.5";

    /// <summary>The SHA3-256 OID.</summary>
    private const string Sha3x256Oid = "2.16.840.1.101.3.4.2.8";

    /// <summary>A text field value inside every signed range.</summary>
    private const string SignedText = "(Alice)";

    /// <summary>The same value with one byte changed.</summary>
    private const string ChangedText = "(Alicf)";

    /// <summary>The signatures in a file with a document timestamp.</summary>
    private const int SignaturesWithTimestamp = 2;

    /// <summary>Gets the digests the fixture's seed value lists.</summary>
    private static PdfDigestMethod[] SeedDigests => [PdfDigestMethod.Sha3_256, PdfDigestMethod.Sha3_384, PdfDigestMethod.Sha3_512, PdfDigestMethod.Shake256];

    /// <summary>OpenSSL's SHA-3 and SHAKE256 signatures validate in the library and the viewer, and meet the seed value.</summary>
    /// <param name="name">The signature file.</param>
    /// <param name="digest">Its digest.</param>
    /// <param name="ecdsa">Whether the P-256 key made it, rather than the RSA key.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("rsa-sha3-256.p7s", PdfDigestMethod.Sha3_256, false)]
    [Arguments("rsa-sha3-384.p7s", PdfDigestMethod.Sha3_384, false)]
    [Arguments("p256-shake256.p7s", PdfDigestMethod.Shake256, true)]
    public async Task IndependentSignatureValidates(string name, PdfDigestMethod digest, bool ecdsa)
    {
        RequireDigest(digest);
        using var root = ecdsa ? Sha3Signatures.P256Certificate() : Sha3Signatures.RsaCertificate();
        var file = Sha3Signatures.Sign(Sha3Signatures.Read(name));
        var report = SignatureFixtures.Validate(file, root)[0];
        var viewer = SignatureFixtures.ViewerCheck(file, root);

        using (Assert.Multiple())
        {
            await Assert.That(report.Cms.Status).IsEqualTo(PdfCmsStatus.Checked);
            await Assert.That(report.DigestValid).IsTrue();
            await Assert.That(report.SignatureValid).IsTrue();
            await Assert.That(report.Cms.Digest).IsEqualTo(digest);
            await Assert.That(report.Chain!.IsTrusted).IsTrue();
            await Assert.That(report.DigestMatchesSeedValue).IsTrue();
            await Assert.That(report.Signature.SeedDigest!.Methods).IsEquivalentTo(SeedDigests);
            await Assert.That(report.Signature.SeedDigest.IsRequired).IsTrue();
            await Assert.That(report.Signature.ReferenceDigestMethods).IsEquivalentTo([PdfDigestMethod.Sha3_256]);
            await Assert.That(viewer.Integrity).IsEqualTo(SignatureIntegrity.Intact);
        }
    }

    /// <summary>Changing one signed byte fails each independent signature in the library and the viewer.</summary>
    /// <param name="name">The signature file.</param>
    /// <param name="digest">Its digest.</param>
    /// <param name="ecdsa">Whether the P-256 key made it.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("rsa-sha3-256.p7s", PdfDigestMethod.Sha3_256, false)]
    [Arguments("rsa-sha3-384.p7s", PdfDigestMethod.Sha3_384, false)]
    [Arguments("p256-shake256.p7s", PdfDigestMethod.Shake256, true)]
    public async Task OneByteChangeFails(string name, PdfDigestMethod digest, bool ecdsa)
    {
        RequireDigest(digest);
        using var root = ecdsa ? Sha3Signatures.P256Certificate() : Sha3Signatures.RsaCertificate();
        var file = SignatureFixtures.Patch(Sha3Signatures.Sign(Sha3Signatures.Read(name)), SignedText, ChangedText);
        var report = SignatureFixtures.Validate(file, root)[0];

        using (Assert.Multiple())
        {
            await Assert.That(report.Cms.Status).IsEqualTo(PdfCmsStatus.Checked);
            await Assert.That(report.DigestValid).IsFalse();
            await Assert.That(report.SignatureValid).IsFalse();
            await Assert.That(SignatureFixtures.ViewerCheck(file, root).Integrity).IsEqualTo(SignatureIntegrity.Invalid);
        }
    }

    /// <summary>A SHA-256 signature on a field whose seed value requires SHA-3 or SHAKE256 is flagged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeedValueRejectsOtherDigest()
    {
        var prepared = Sha3Signatures.Prepare();
        var report = SignatureFixtures.ValidateSingle(PdfSigning.WriteContents(prepared, PdfSigning.CreateCms(prepared.SignedBytes, SignatureFixtures.Signer)));

        await Assert.That(report.SignatureValid).IsTrue();
        await Assert.That(report.Cms.Digest).IsEqualTo(PdfDigestMethod.Sha256);
        await Assert.That(report.DigestMatchesSeedValue).IsFalse();
    }

    /// <summary>
    /// SHAKE256 is accepted as id-shake256 without parameters and as id-shake256-len with 512 bits (RFC 8419, 3.1), and
    /// with RSASSA-PSS (RFC 8702, 3.2.1).
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShakeAllowedFormsValidate()
    {
        RequireDigest(PdfDigestMethod.Shake256);
        using var ecdsa = ShakeTestSigner.CreateEcdsaCertificate("SHAKE Signer");
        var lengthForm = Signed(content => ShakeTestSigner.SignEcdsa(content, ecdsa, ShakeTestSigner.Identifier(ShakeTestSigner.Shake256LenOid, Shake256Bits)));
        var pss = Signed(static content => ShakeTestSigner.SignRsaPss(content, SignatureFixtures.Signer));
        var lengthReport = SignatureFixtures.Validate(lengthForm, ecdsa)[0];
        var pssReport = SignatureFixtures.ValidateSingle(pss);
        var pssTampered = SignatureFixtures.ValidateSingle(SignatureFixtures.Patch(pss, SignedText, ChangedText));

        using (Assert.Multiple())
        {
            await Assert.That(lengthReport.Cms.Status).IsEqualTo(PdfCmsStatus.Checked);
            await Assert.That(lengthReport.DigestValid).IsTrue();
            await Assert.That(lengthReport.SignatureValid).IsTrue();
            await Assert.That(lengthReport.Cms.Digest).IsEqualTo(PdfDigestMethod.Shake256);
            await Assert.That(pssReport.DigestValid).IsTrue();
            await Assert.That(pssReport.SignatureValid).IsTrue();
            await Assert.That(pssTampered.SignatureValid).IsFalse();
            await Assert.That(SignatureFixtures.ViewerCheck(pss, SignatureFixtures.Signer).Integrity).IsEqualTo(SignatureIntegrity.Intact);
        }
    }

    /// <summary>
    /// SHAKE256 with parameters on id-shake256, a 256-bit id-shake256-len and SHAKE128 are not allowed: never valid, and
    /// reported as such rather than as a damaged signature (ISO/TS 32001:2022, 5.1.4 and its note).
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShakeVariantsAreNotAllowed()
    {
        // The test signer computes SHAKE256 to make these signatures.
        RequireDigest(PdfDigestMethod.Shake256);
        using var ecdsa = ShakeTestSigner.CreateEcdsaCertificate("SHAKE Signer");
        byte[][] files =
        [
            Signed(content => ShakeTestSigner.SignEcdsa(content, ecdsa, ShakeTestSigner.IdentifierWithNull(ShakeTestSigner.Shake256Oid))),
            Signed(content => ShakeTestSigner.SignEcdsa(content, ecdsa, ShakeTestSigner.Identifier(ShakeTestSigner.Shake256LenOid, ShortShakeBits))),
            Signed(content => ShakeTestSigner.SignEcdsa(content, ecdsa, ShakeTestSigner.Identifier(ShakeTestSigner.Shake128Oid))),
        ];

        foreach (var file in files)
        {
            var report = SignatureFixtures.Validate(file, ecdsa)[0];
            var viewer = SignatureFixtures.ViewerCheck(file, ecdsa);
            using (Assert.Multiple())
            {
                await Assert.That(report.Cms.Status).IsEqualTo(PdfCmsStatus.DigestNotAllowed);
                await Assert.That(report.SignatureValid).IsFalse();
                await Assert.That(report.DigestValid).IsFalse();
                await Assert.That(report.Cms.Detail).Contains("not allow");
                await Assert.That(viewer.Integrity).IsEqualTo(SignatureIntegrity.DigestNotAllowed);
                await Assert.That(viewer.Summary).Contains("not valid");
            }
        }
    }

    /// <summary>A digest the checker does not support is reported as unsupported, not as altered content.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownDigestIsUnsupported()
    {
        var file = Signed(static content => ShakeTestSigner.SignWithDigestOid(content, SignatureFixtures.Signer, Md5Oid));
        var report = SignatureFixtures.ValidateSingle(file);
        var viewer = SignatureFixtures.ViewerCheck(file, SignatureFixtures.Signer);

        using (Assert.Multiple())
        {
            await Assert.That(report.Cms.Status).IsEqualTo(PdfCmsStatus.Unsupported);
            await Assert.That(report.SignatureValid).IsFalse();
            await Assert.That(report.Cms.Detail).Contains(Md5Oid);
            await Assert.That(viewer.Integrity).IsEqualTo(SignatureIntegrity.Unsupported);
            await Assert.That(viewer.Summary).Contains("could not be checked");
        }
    }

    /// <summary>
    /// Where the platform's cryptography library has no SHA-3, such as macOS, a SHA3-256 signature is unsupported, never
    /// valid or invalid. On platforms with SHA-3 this skips; <see cref="IndependentSignatureValidates"/> runs there.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Sha3WithoutPlatformSupportIsUnsupported()
    {
        if (SHA3_256.IsSupported)
        {
            Skip.Test("This platform computes SHA3-256, so the unsupported result cannot occur here.");
        }

        using var root = Sha3Signatures.RsaCertificate();
        var file = Sha3Signatures.Sign(Sha3Signatures.RsaSha3x256);
        var report = SignatureFixtures.Validate(file, root)[0];

        await Assert.That(report.Cms.Status).IsEqualTo(PdfCmsStatus.Unsupported);
        await Assert.That(report.SignatureValid).IsFalse();
        await Assert.That(SignatureFixtures.ViewerCheck(file, root).Integrity).IsEqualTo(SignatureIntegrity.Unsupported);
    }

    /// <summary>Document timestamps whose imprint is SHA3-256 or SHAKE256 are checked, and fail when a stamped byte changes.</summary>
    /// <param name="imprint">The imprint digest OID.</param>
    /// <param name="digest">The same digest.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(Sha3x256Oid, PdfDigestMethod.Sha3_256)]
    [Arguments(ShakeTestSigner.Shake256Oid, PdfDigestMethod.Shake256)]
    public async Task DocumentTimestampWithSha3IsChecked(string imprint, PdfDigestMethod digest)
    {
        RequireDigest(digest);
        using var authority = new TestTimestampAuthority(PdfSigning.SigningTime) { ImprintOid = imprint };
        var update = new Dictionary<int, string>
        {
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Annots [5 0 R 6 0 R 10 0 R] >>",
            [4] = "<< /Fields [5 0 R 6 0 R 10 0 R] /SigFlags 3 >>",
            [9] = PdfSigning.SignatureDictionary("ETSI.RFC3161", string.Empty).Replace("/Type /Sig ", "/Type /DocTimeStamp ", StringComparison.Ordinal),
            [10] = "<< /Type /Annot /Subtype /Widget /FT /Sig /T (Timestamp1) /Rect [0 0 0 0] /F 132 /P 3 0 R /V 9 0 R >>",
        };
        var prepared = PdfSigning.Prepare(IncrementalPdf.Append(SignatureSamples.Signed(SignatureFixtures.Signer, 0, string.Empty), update));
        var file = PdfSigning.WriteContents(prepared, await authority.TimestampAsync(prepared.SignedBytes, CancellationToken.None));
        var reports = ValidateWith(file, authority.Certificate);
        var tampered = ValidateWith(SignatureFixtures.Patch(file, SignedText, ChangedText), authority.Certificate);

        using (Assert.Multiple())
        {
            await Assert.That(reports.Count).IsEqualTo(SignaturesWithTimestamp);
            await Assert.That(reports[1].Cms.Status).IsEqualTo(PdfCmsStatus.Checked);
            await Assert.That(reports[1].Cms.Digest).IsEqualTo(digest);
            await Assert.That(reports[1].DigestValid).IsTrue();
            await Assert.That(reports[1].TimestampValid).IsTrue();
            await Assert.That(tampered[1].DigestValid).IsFalse();
            await Assert.That(tampered[1].TimestampValid).IsFalse();
        }
    }

    /// <summary>A signature timestamp whose imprint is SHAKE256 is checked against the signature value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignatureTimestampWithShakeIsChecked()
    {
        RequireDigest(PdfDigestMethod.Shake256);
        using var authority = new TestTimestampAuthority(PdfSigning.SigningTime) { ImprintOid = ShakeTestSigner.Shake256Oid };
        var options = new HyperPdfLibrary.Signing.PdfSigningOptions
        {
            Reason = "Approved",
            TimestampDigest = PdfDigestMethod.Shake256,
            Timestamp = new HyperPdfLibrary.Tests.Signing.TimestamperClient(authority),
        };
        var file = (await HyperPdfLibrary.Tests.Signing.SigningFixtures.SignResultAsync(SignatureFixtures.Blank(), SignatureFixtures.Signer, options)).Bytes;
        var report = ValidateWith(file, authority.Certificate)[0];

        await Assert.That(report.SignatureValid).IsTrue();
        await Assert.That(report.HasTimestamp).IsTrue();
        await Assert.That(report.TimestampValid).IsTrue();
        await Assert.That(SignatureFixtures.ViewerCheck(file, SignatureFixtures.Signer).Timestamp!.IsValid).IsTrue();
    }

    /// <summary>Skips a test when the platform cannot compute a digest, saying why.</summary>
    /// <param name="digest">The digest.</param>
    internal static void RequireDigest(PdfDigestMethod digest)
    {
        var supported = digest switch
        {
            PdfDigestMethod.Sha3_256 => SHA3_256.IsSupported,
            PdfDigestMethod.Sha3_384 => SHA3_384.IsSupported,
            PdfDigestMethod.Sha3_512 => SHA3_512.IsSupported,
            PdfDigestMethod.Shake256 => Shake256.IsSupported,
            _ => true,
        };
        if (!supported)
        {
            Skip.Test($"This platform's cryptography library cannot compute {digest}; such signatures are reported as unsupported there.");
        }
    }

    /// <summary>Signs the SHA-3 fixture's prepared file with a signer.</summary>
    /// <param name="sign">Makes the DER signature over the signed bytes.</param>
    /// <returns>The signed file.</returns>
    private static byte[] Signed(Func<byte[], byte[]> sign)
    {
        var prepared = Sha3Signatures.Prepare();
        return PdfSigning.WriteContents(prepared, sign(prepared.SignedBytes));
    }

    /// <summary>Validates a file trusting the test signer and a timestamp authority.</summary>
    /// <param name="file">The file.</param>
    /// <param name="authority">The authority's certificate.</param>
    /// <returns>The reports.</returns>
    private static IReadOnlyList<PdfSignatureValidationReport> ValidateWith(byte[] file, X509Certificate2 authority)
    {
        using var document = PdfDocumentReader.Open(file, null);
        return PdfDocumentSignatureValidation.ValidateSignatures(document, new() { TrustedRoots = [SignatureFixtures.Signer, authority] });
    }
}
