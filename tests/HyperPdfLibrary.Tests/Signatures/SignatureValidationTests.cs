// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Tests.Signatures;

/// <summary>Tests for byte ranges, CMS checks and revisions of signed documents.</summary>
public sealed class SignatureValidationTests
{
    /// <summary>The revision a signature appended to the original closes.</summary>
    private const int SignedRevision = 1;

    /// <summary>The revisions after signing once and updating once.</summary>
    private const int RevisionsAfterUpdate = 3;

    /// <summary>The SHA-256 OID.</summary>
    private const string Sha256Oid = "2.16.840.1.101.3.4.2.1";

    /// <summary>Gets a DER SEQUENCE holding INTEGER 1: well formed, but not a CMS container.</summary>
    private static ReadOnlySpan<byte> NotSignedData => [0x30, 0x03, 0x02, 0x01, 0x01];

    /// <summary>A valid signature reports a good digest and signature, the signer, both signing times and a trusted chain.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidSignatureReportsSigner()
    {
        var report = SignatureFixtures.ValidateSingle(SignatureSamples.Signed(SignatureFixtures.Signer, 0, string.Empty));

        using (Assert.Multiple())
        {
            await Assert.That(report.Cms.Status).IsEqualTo(PdfCmsStatus.Checked);
            await Assert.That(report.DigestValid).IsTrue();
            await Assert.That(report.SignatureValid).IsTrue();
            await Assert.That(report.Signer!.Subject).IsEqualTo("CN=Tester Signer");
            await Assert.That(report.CoversWholeDocument).IsTrue();
            await Assert.That(report.ModifiedAfterSigning).IsFalse();
            await Assert.That(report.Changes.Length).IsEqualTo(0);
            await Assert.That(report.ByteRange.RevisionIndex).IsEqualTo(SignedRevision);
            await Assert.That(report.SignedAttributeTime).IsEqualTo(PdfSigning.SigningTime);
            await Assert.That(report.RecordedTime).IsEqualTo(PdfSigning.SigningTime);
            await Assert.That(report.Chain!.IsTrusted).IsTrue();
            await Assert.That(report.Chain.Revocation).IsEqualTo(PdfRevocationStatus.Unknown);
            await Assert.That(report.Cms.DigestAlgorithm).IsEqualTo(Sha256Oid);
            await Assert.That(report.Signature.FieldName).IsEqualTo("Signature1");
            await Assert.That(report.Signature.SignerName).IsEqualTo("Tester");
            await Assert.That(report.Signature.Format).IsEqualTo(PdfSignatureFormat.Pkcs7Detached);
        }
    }

    /// <summary>Changing one byte inside the signed range fails the digest and the signature.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TamperedByteFailsDigest()
    {
        var signed = SignatureSamples.Signed(SignatureFixtures.Signer, 0, string.Empty);
        var report = SignatureFixtures.ValidateSingle(SignatureFixtures.Patch(signed, "(Alice)", "(Alicf)"));

        await Assert.That(report.ByteRange.IsValid).IsTrue();
        await Assert.That(report.DigestValid).IsFalse();
        await Assert.That(report.SignatureValid).IsFalse();
    }

    /// <summary>A self-signed signer the options do not trust gives an untrusted chain but a good signature.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UntrustedSignerKeepsGoodSignature()
    {
        using var other = PdfSigning.CreateCertificate("Other Root", null, true, TimeProvider.System);
        var report = SignatureFixtures.Validate(SignatureSamples.Signed(SignatureFixtures.Signer, 0, string.Empty), other)[0];

        await Assert.That(report.SignatureValid).IsTrue();
        await Assert.That(report.Chain!.IsTrusted).IsFalse();
    }

    /// <summary>The revisions are listed oldest first, and the signature closes the second.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RevisionsAreListed()
    {
        var file = SignatureSamples.AddAnnotation(SignatureSamples.Signed(SignatureFixtures.Signer, 0, string.Empty));
        using var document = PdfDocumentReader.Open(file, null);
        var revisions = PdfDocumentSignatureValidation.GetRevisions(document);

        await Assert.That(revisions.Count).IsEqualTo(RevisionsAfterUpdate);
        await Assert.That(revisions[^1].EndOffset).IsEqualTo(file.Length);
        await Assert.That(revisions[0].EndOffset).IsLessThan(revisions[1].EndOffset);
    }

    /// <summary>A /Contents that is not a CMS container is reported as malformed, not thrown.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MalformedContentsIsReported()
    {
        var prepared = PdfSigning.Prepare(IncrementalPdf.Append(SignatureSamples.Unsigned(), SignedUpdate()));
        var file = PdfSigning.WriteContents(prepared, NotSignedData.ToArray());
        var report = SignatureFixtures.ValidateSingle(file);

        await Assert.That(report.Cms.Status).IsEqualTo(PdfCmsStatus.Malformed);
        await Assert.That(report.SignatureValid).IsFalse();
    }

    /// <summary>An unsigned signature field is reported as unsigned.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsignedFieldIsReported()
    {
        var report = SignatureFixtures.ValidateSingle(SignatureSamples.Unsigned());

        await Assert.That(report.Cms.Status).IsEqualTo(PdfCmsStatus.Unsigned);
        await Assert.That(report.ByteRange.Status).IsEqualTo(PdfByteRangeStatus.Missing);
    }

    /// <summary>The signature update for a field, without the signature's bytes.</summary>
    /// <returns>The objects.</returns>
    private static Dictionary<int, string> SignedUpdate() => new()
    {
        [SignatureSamples.SignatureField] = "<< /Type /Annot /Subtype /Widget /FT /Sig /T (Signature1) /Rect [0 0 0 0] /P 3 0 R /V 7 0 R >>",
        [SignatureSamples.SignatureValue] = PdfSigning.SignatureDictionary("adbe.pkcs7.detached", string.Empty),
    };
}
