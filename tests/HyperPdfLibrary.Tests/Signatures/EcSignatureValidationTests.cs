// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using HyperPdf.Core.Signatures;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Tests.Signatures;

/// <summary>
/// Tests for ISO/TS 32002:2022 elliptic-curve signatures: independently made P-curve, Brainpool, Ed25519 and Ed448
/// signatures validate; a one-byte change, a curve and digest pair the specification does not permit, a certificate with
/// explicit curve parameters and a signature that hashes the signed attributes with another digest each fail with their
/// own result; and a curve this computer lacks gives an unsupported result, never a valid one.
/// </summary>
public sealed class EcSignatureValidationTests
{
    /// <summary>A text field value inside every signed range.</summary>
    private const string SignedText = "(Alice)";

    /// <summary>The same value with one byte changed.</summary>
    private const string ChangedText = "(Alicf)";

    /// <summary>The name of the first Brainpool curve.</summary>
    private const string Brainpool256 = "brainpoolP256r1";

    /// <summary>The name of the second Brainpool curve.</summary>
    private const string Brainpool384 = "brainpoolP384r1";

    /// <summary>The name of the third Brainpool curve.</summary>
    private const string Brainpool512 = "brainpoolP512r1";

    /// <summary>Gets the encoded ecdsa-with-SHA256 OID.</summary>
    private static byte[] EcdsaSha256 => [0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x04, 0x03, 0x02];

    /// <summary>Gets the encoded ecdsa-with-SHA384 OID.</summary>
    private static byte[] EcdsaSha384 => [0x2A, 0x86, 0x48, 0xCE, 0x3D, 0x04, 0x03, 0x03];

    /// <summary>Gets the encoded id-sha512 OID.</summary>
    private static byte[] Sha512 => [0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x03];

    /// <summary>Gets the encoded id-sha256 OID.</summary>
    private static byte[] Sha256 => [0x60, 0x86, 0x48, 0x01, 0x65, 0x03, 0x04, 0x02, 0x01];

    /// <summary>Independent signatures on every listed curve validate in the library and the viewer.</summary>
    /// <param name="name">The signature file.</param>
    /// <param name="certificate">The signer's certificate file.</param>
    /// <param name="digest">The digest the signature uses.</param>
    /// <param name="curve">The Brainpool curve it needs from the platform, or an empty string.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("p256-sha256.p7s", "p256.crt", PdfDigestMethod.Sha256, "")]
    [Arguments("p256-sha3-256.p7s", "p256.crt", PdfDigestMethod.Sha3_256, "")]
    [Arguments("p384-sha384.p7s", "p384.crt", PdfDigestMethod.Sha384, "")]
    [Arguments("p521-sha512.p7s", "p521.crt", PdfDigestMethod.Sha512, "")]
    [Arguments("p521-sha3-512.p7s", "p521.crt", PdfDigestMethod.Sha3_512, "")]
    [Arguments("bp256-sha256.p7s", "bp256.crt", PdfDigestMethod.Sha256, Brainpool256)]
    [Arguments("bp256-sha512.p7s", "bp256.crt", PdfDigestMethod.Sha512, Brainpool256)]
    [Arguments("bp384-sha384.p7s", "bp384.crt", PdfDigestMethod.Sha384, Brainpool384)]
    [Arguments("bp512-sha512.p7s", "bp512.crt", PdfDigestMethod.Sha512, Brainpool512)]
    [Arguments("ed25519.p7s", "ed25519.crt", PdfDigestMethod.Sha512, "")]
    [Arguments("ed448.p7s", "ed448.crt", PdfDigestMethod.Shake256, "")]
    public async Task IndependentSignatureValidates(string name, string certificate, PdfDigestMethod digest, string curve)
    {
        Require(digest, curve);
        using var root = EcSignatures.Certificate(certificate);
        var file = EcSignatures.Sign(EcSignatures.Signature(name));
        var report = SignatureFixtures.Validate(file, root)[0];
        var viewer = SignatureFixtures.ViewerCheck(file, root);

        using (Assert.Multiple())
        {
            await Assert.That(report.Cms.Status).IsEqualTo(PdfCmsStatus.Checked);
            await Assert.That(report.DigestValid).IsTrue();
            await Assert.That(report.SignatureValid).IsTrue();
            await Assert.That(report.Cms.Digest).IsEqualTo(digest);
            await Assert.That(report.Cms.Signer!.Thumbprint).IsEqualTo(root.Thumbprint);
            await Assert.That(viewer.Integrity).IsEqualTo(SignatureIntegrity.Intact);
        }
    }

    /// <summary>Changing one signed byte fails each independent signature in the library and the viewer.</summary>
    /// <param name="name">The signature file.</param>
    /// <param name="certificate">The signer's certificate file.</param>
    /// <param name="digest">The digest the signature uses.</param>
    /// <param name="curve">The Brainpool curve it needs from the platform, or an empty string.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("p256-sha256.p7s", "p256.crt", PdfDigestMethod.Sha256, "")]
    [Arguments("p384-sha384.p7s", "p384.crt", PdfDigestMethod.Sha384, "")]
    [Arguments("p521-sha512.p7s", "p521.crt", PdfDigestMethod.Sha512, "")]
    [Arguments("bp256-sha256.p7s", "bp256.crt", PdfDigestMethod.Sha256, Brainpool256)]
    [Arguments("bp512-sha512.p7s", "bp512.crt", PdfDigestMethod.Sha512, Brainpool512)]
    [Arguments("ed25519.p7s", "ed25519.crt", PdfDigestMethod.Sha512, "")]
    [Arguments("ed448.p7s", "ed448.crt", PdfDigestMethod.Shake256, "")]
    public async Task OneByteChangeFails(string name, string certificate, PdfDigestMethod digest, string curve)
    {
        Require(digest, curve);
        using var root = EcSignatures.Certificate(certificate);
        var file = SignatureFixtures.Patch(EcSignatures.Sign(EcSignatures.Signature(name)), SignedText, ChangedText);
        var report = SignatureFixtures.Validate(file, root)[0];

        using (Assert.Multiple())
        {
            await Assert.That(report.Cms.Status).IsEqualTo(PdfCmsStatus.Checked);
            await Assert.That(report.DigestValid).IsFalse();
            await Assert.That(report.SignatureValid).IsFalse();
            await Assert.That(SignatureFixtures.ViewerCheck(file, root).Integrity).IsEqualTo(SignatureIntegrity.Invalid);
        }
    }

    /// <summary>A damaged signature value fails with the altered result, not a rule result.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DamagedSignatureValueFails()
    {
        using var root = EcSignatures.Certificate("ed25519.crt");
        var signature = EcSignatures.Signature("ed25519.p7s");
        signature[^1] ^= 1;
        var file = EcSignatures.Sign(signature);
        var report = SignatureFixtures.Validate(file, root)[0];

        using (Assert.Multiple())
        {
            await Assert.That(report.Cms.Status).IsEqualTo(PdfCmsStatus.Checked);
            await Assert.That(report.SignatureValid).IsFalse();
            await Assert.That(SignatureFixtures.ViewerCheck(file, root).Integrity).IsEqualTo(SignatureIntegrity.Invalid);
        }
    }

    /// <summary>
    /// A curve and digest the specification does not pair (Table 3) is refused as a rule violation, though the signature
    /// is arithmetically good: P-256 with SHA-512, P-384 with SHA-256 and brainpoolP512r1 with SHA-256.
    /// </summary>
    /// <param name="name">The signature file.</param>
    /// <param name="certificate">The signer's certificate file.</param>
    /// <param name="digest">The digest the signature uses.</param>
    /// <param name="curve">The Brainpool curve it needs from the platform, or an empty string.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("p256-sha512.p7s", "p256.crt", PdfDigestMethod.Sha512, "")]
    [Arguments("p256-sha3-512.p7s", "p256.crt", PdfDigestMethod.Sha3_512, "")]
    [Arguments("p384-sha256.p7s", "p384.crt", PdfDigestMethod.Sha256, "")]
    [Arguments("bp512-sha256.p7s", "bp512.crt", PdfDigestMethod.Sha256, Brainpool512)]
    public async Task DisallowedCurveAndDigestPairIsRefused(string name, string certificate, PdfDigestMethod digest, string curve)
    {
        Require(digest, curve);
        using var root = EcSignatures.Certificate(certificate);
        var file = EcSignatures.Sign(EcSignatures.Signature(name));
        var report = SignatureFixtures.Validate(file, root)[0];
        var viewer = SignatureFixtures.ViewerCheck(file, root);

        using (Assert.Multiple())
        {
            await Assert.That(report.Cms.Status).IsEqualTo(PdfCmsStatus.CurveDigestNotAllowed);
            await Assert.That(report.SignatureValid).IsFalse();
            await Assert.That(report.Cms.Detail).Contains("does not allow");
            await Assert.That(viewer.Integrity).IsEqualTo(SignatureIntegrity.CurveDigestNotAllowed);
            await Assert.That(viewer.Summary).Contains("not valid");
        }
    }

    /// <summary>A certificate that gives its curve as explicit parameters is refused (ISO/TS 32002:2022, 5.1.3).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExplicitCurveParametersAreRefused()
    {
        using var root = EcSignatures.Certificate("explicit.crt");
        var file = EcSignatures.Sign(EcSignatures.Signature("explicit-sha256.p7s"));
        var report = SignatureFixtures.Validate(file, root)[0];
        var viewer = SignatureFixtures.ViewerCheck(file, root);

        using (Assert.Multiple())
        {
            await Assert.That(report.Cms.Status).IsEqualTo(PdfCmsStatus.UnnamedCurve);
            await Assert.That(report.SignatureValid).IsFalse();
            await Assert.That(report.Cms.Detail).Contains("named curve");
            await Assert.That(viewer.Integrity).IsEqualTo(SignatureIntegrity.UnnamedCurve);
        }
    }

    /// <summary>
    /// A signature algorithm that names a different digest from the signer's digest algorithm means the signed attributes
    /// and the content are hashed differently, which is refused (ISO/TS 32002:2022, 5.1.4).
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignedAttributeDigestMismatchIsRefused()
    {
        using var root = EcSignatures.Certificate("p256.crt");
        var signature = EcSignatures.Replace(EcSignatures.Signature("p256-sha256.p7s"), EcdsaSha256, EcdsaSha384);
        var file = EcSignatures.Sign(signature);
        var report = SignatureFixtures.Validate(file, root)[0];
        var viewer = SignatureFixtures.ViewerCheck(file, root);

        using (Assert.Multiple())
        {
            await Assert.That(report.Cms.Status).IsEqualTo(PdfCmsStatus.DigestMismatch);
            await Assert.That(report.SignatureValid).IsFalse();
            await Assert.That(viewer.Integrity).IsEqualTo(SignatureIntegrity.DigestMismatch);
            await Assert.That(viewer.Summary).Contains("different digest");
        }
    }

    /// <summary>Ed25519 must use SHA-512 and Ed448 the fixed-length id-shake256 (Table 2 and Table 4).</summary>
    /// <param name="name">The signature file.</param>
    /// <param name="certificate">The signer's certificate file.</param>
    /// <param name="wrongDigest">Whether to change the digest to SHA-256, rather than keep Ed448's id-shake256-len form.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("ed25519.p7s", "ed25519.crt", true)]
    [Arguments("ed448-len.p7s", "ed448.crt", false)]
    public async Task EdwardsSignatureWithOtherDigestIsRefused(string name, string certificate, bool wrongDigest)
    {
        Sha3SignatureValidationTests.RequireDigest(PdfDigestMethod.Shake256);
        using var root = EcSignatures.Certificate(certificate);
        var signature = EcSignatures.Signature(name);
        var file = EcSignatures.Sign(wrongDigest ? EcSignatures.Replace(signature, Sha512, Sha256) : signature);
        var report = SignatureFixtures.Validate(file, root)[0];

        using (Assert.Multiple())
        {
            await Assert.That(report.Cms.Status).IsEqualTo(PdfCmsStatus.CurveDigestNotAllowed);
            await Assert.That(report.SignatureValid).IsFalse();
            await Assert.That(SignatureFixtures.ViewerCheck(file, root).Integrity).IsEqualTo(SignatureIntegrity.CurveDigestNotAllowed);
        }
    }

    /// <summary>
    /// Where the platform's cryptography library lacks a Brainpool curve, such as macOS, the signature is unsupported:
    /// neither valid nor invalid. On platforms with the curves this skips; <see cref="IndependentSignatureValidates"/> runs there.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BrainpoolWithoutPlatformSupportIsUnsupported()
    {
        if (EcSignatures.PlatformHasCurve(Brainpool256))
        {
            Skip.Test("This platform implements brainpoolP256r1, so the unsupported result cannot occur here.");
        }

        using var root = EcSignatures.Certificate("bp256.crt");
        var file = EcSignatures.Sign(EcSignatures.Signature("bp256-sha256.p7s"));
        var report = SignatureFixtures.Validate(file, root)[0];

        using (Assert.Multiple())
        {
            await Assert.That(report.Cms.Status).IsEqualTo(PdfCmsStatus.Unsupported);
            await Assert.That(report.SignatureValid).IsFalse();
            await Assert.That(SignatureFixtures.ViewerCheck(file, root).Integrity).IsEqualTo(SignatureIntegrity.Unsupported);
        }
    }

    /// <summary>Skips a test when the platform cannot compute the digest or implement the curve, saying why.</summary>
    /// <param name="digest">The digest.</param>
    /// <param name="curve">The Brainpool curve, or an empty string.</param>
    private static void Require(PdfDigestMethod digest, string curve)
    {
        Sha3SignatureValidationTests.RequireDigest(digest);
        if (curve.Length > 0 && !EcSignatures.PlatformHasCurve(curve))
        {
            Skip.Test($"This platform's cryptography library lacks {curve}; such signatures are reported as unsupported there.");
        }
    }
}
