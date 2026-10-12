// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using HyperPdf.Core.Signatures;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Tests.Signatures;

/// <summary>Tests for timestamps, the SHA-1 enveloping format, usage rights, and agreement with the app's own verifier.</summary>
public sealed class SignatureFormatTests
{
    /// <summary>The unsigned attribute holding a signature's timestamp token.</summary>
    private const string TimestampTokenOid = "1.2.840.113549.1.9.16.2.14";

    /// <summary>The detached CMS sub-filter.</summary>
    private const string Detached = "adbe.pkcs7.detached";

    /// <summary>The pages in the app's signed sample.</summary>
    private const int Pages = 2;

    /// <summary>DocMDP P 1.</summary>
    private const int NoChanges = 1;

    /// <summary>The number of signatures after a document timestamp is added.</summary>
    private const int SignaturesWithTimestamp = 2;

    /// <summary>Changing one default trust collection cannot change a later default validation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultTrustRootsAreIndependent()
    {
        _ = PdfSignatureValidationOptions.Default.TrustedRoots.Add(SignatureFixtures.Signer);
        await Assert.That(PdfSignatureValidationOptions.Default.TrustedRoots.Count).IsEqualTo(0);
    }

    /// <summary>A document timestamp is checked, and adding it is allowed even when the certification allows no changes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentTimestampIsChecked()
    {
        using var authority = new TestTimestampAuthority(PdfSigning.SigningTime);
        var update = new Dictionary<int, string>
        {
            [3] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Annots [5 0 R 6 0 R 10 0 R] >>",
            [4] = "<< /Fields [5 0 R 6 0 R 10 0 R] /SigFlags 3 >>",
            [9] = PdfSigning.SignatureDictionary("ETSI.RFC3161", string.Empty).Replace("/Type /Sig ", "/Type /DocTimeStamp ", StringComparison.Ordinal),
            [10] = "<< /Type /Annot /Subtype /Widget /FT /Sig /T (Timestamp1) /Rect [0 0 0 0] /F 132 /P 3 0 R /V 9 0 R >>",
        };
        var prepared = PdfSigning.Prepare(IncrementalPdf.Append(SignatureSamples.Signed(SignatureFixtures.Signer, NoChanges, string.Empty), update));
        var file = PdfSigning.WriteContents(prepared, await authority.TimestampAsync(prepared.SignedBytes, CancellationToken.None));
        using var document = PdfDocumentReader.Open(file, null);
        var reports = PdfDocumentSignatureValidation.ValidateSignatures(document, new() { TrustedRoots = [SignatureFixtures.Signer, authority.Certificate] });

        await Assert.That(reports.Count).IsEqualTo(SignaturesWithTimestamp);
        await Assert.That(reports[1].Signature.IsDocumentTimestamp).IsTrue();
        await Assert.That(reports[1].DigestValid).IsTrue();
        await Assert.That(reports[1].TimestampValid).IsTrue();
        await Assert.That(reports[1].Chain!.IsTrusted).IsTrue();
        await Assert.That(reports[1].SignedAttributeTime).IsEqualTo(PdfSigning.SigningTime);
        await Assert.That(reports[0].ChangeKinds).IsEqualTo(PdfModificationKinds.SecurityStore);
        await Assert.That(reports[0].PermittedByMdp).IsTrue();
    }

    /// <summary>A timestamp token in the signer's unsigned attributes is read and checked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignatureTimestampIsChecked()
    {
        using var authority = new TestTimestampAuthority(PdfSigning.SigningTime);
        var prepared = PdfSigning.Prepare(IncrementalPdf.Append(SignatureSamples.Unsigned(), SignedUpdate(Detached)));
        var cms = new SignedCms(new ContentInfo(prepared.SignedBytes), true);
        cms.ComputeSignature(new(SignatureFixtures.Signer), true);
        var token = await authority.TimestampAsync(cms.SignerInfos[0].GetSignature(), CancellationToken.None);
        cms.SignerInfos[0].AddUnsignedAttribute(new(TimestampTokenOid, token));
        var file = PdfSigning.WriteContents(prepared, cms.Encode());
        using var document = PdfDocumentReader.Open(file, null);
        var report = PdfDocumentSignatureValidation.ValidateSignatures(document, new() { TrustedRoots = [SignatureFixtures.Signer, authority.Certificate] })[0];

        await Assert.That(report.SignatureValid).IsTrue();
        await Assert.That(report.HasTimestamp).IsTrue();
        await Assert.That(report.TimestampValid).IsTrue();
        await Assert.That(report.Cms.Timestamp!.Time).IsEqualTo(PdfSigning.SigningTime);
    }

    /// <summary>An <c>adbe.pkcs7.sha1</c> signature, which signs the SHA-1 hash of the bytes, is checked.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Sha1EnvelopingSignatureIsChecked()
    {
        var prepared = PdfSigning.Prepare(IncrementalPdf.Append(SignatureSamples.Unsigned(), SignedUpdate("adbe.pkcs7.sha1")));
        var cms = new SignedCms(new ContentInfo(CryptographicOperations.HashData(HashAlgorithmName.SHA1, prepared.SignedBytes)), false);
        cms.ComputeSignature(new(SignatureFixtures.Signer), true);
        var report = SignatureFixtures.ValidateSingle(PdfSigning.WriteContents(prepared, cms.Encode()));

        await Assert.That(report.Signature.Format).IsEqualTo(PdfSignatureFormat.Pkcs7Sha1);
        await Assert.That(report.DigestValid).IsTrue();
        await Assert.That(report.SignatureValid).IsTrue();
    }

    /// <summary>UR3 usage rights are read as data from the catalog's /Perms.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UsageRightsAreRead()
    {
        var file = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /Perms << /UR3 3 0 R >> >>",
            "<< /Type /Pages /Kids [] /Count 0 >>",
            "<< /Type /Sig /Reference [<< /Type /SigRef /TransformMethod /UR3 /TransformParams << /Document [/FullSave] /Annots [/Create /Delete] /Form [/FillIn] /Msg (Rights) /P true >> >>] >>");
        using var document = PdfDocumentReader.Open(file, null);
        var rights = PdfDocumentSignatureValidation.GetUsageRights(document);

        await Assert.That(rights).IsNotNull();
        await Assert.That(rights!.Document).IsEquivalentTo(["FullSave"]);
        await Assert.That(rights.Annotations).IsEquivalentTo(["Create", "Delete"]);
        await Assert.That(rights.Form).IsEquivalentTo(["FillIn"]);
        await Assert.That(rights.Message).IsEqualTo("Rights");
        await Assert.That(rights.RestrictsOtherRights).IsTrue();
    }

    /// <summary>A FieldMDP transform in /Reference is read as a lock.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FieldMdpReferenceIsRead()
    {
        const string Reference = "/Reference [<< /Type /SigRef /TransformMethod /FieldMDP /TransformParams << /Action /All /V /1.2 >> >>]";
        var update = SignedUpdate(Detached);
        update[SignatureSamples.SignatureValue] = PdfSigning.SignatureDictionary(Detached, Reference);
        var file = PdfSigning.SignDetached(IncrementalPdf.Append(SignatureSamples.Unsigned(), update), SignatureFixtures.Signer);
        var report = SignatureFixtures.ValidateSingle(SignatureSamples.FillForm(file));

        await Assert.That(report.Signature.FieldMdp!.Action).IsEqualTo(PdfFieldLockAction.All);
        await Assert.That(report.PermittedByMdp).IsFalse();
    }

    /// <summary>The app's verifier and the library agree on an intact and a tampered file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AgreesWithAppVerifier()
    {
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var intact = TestSignedPdf.Create(Pages, certificate);
        var tampered = SignatureFixtures.Patch(intact, TestPdf.Title, TestPdf.Title.ToUpperInvariant());

        await Assert.That(AppIntegrity(intact, certificate)).IsEqualTo(SignatureIntegrity.Intact);
        await Assert.That(LibraryIntact(intact, certificate)).IsTrue();
        await Assert.That(AppIntegrity(tampered, certificate)).IsEqualTo(SignatureIntegrity.Invalid);
        await Assert.That(LibraryIntact(tampered, certificate)).IsFalse();
    }

    /// <summary>Checks a file with the app's verifier.</summary>
    /// <param name="file">The file.</param>
    /// <param name="certificate">The trusted signer.</param>
    /// <returns>The app's integrity result.</returns>
    private static SignatureIntegrity AppIntegrity(byte[] file, System.Security.Cryptography.X509Certificates.X509Certificate2 certificate)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-signature-{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, file);
        try
        {
            using var document = PdfDocumentReader.Open(file, null);
            var field = PdfDocumentAttachments.GetSignatures(document)[0];
            var raw = new RawSignature(field.Index, field.Contents, field.ByteRange, field.SubFilter, field.Reason, field.SigningTime);
            return SignatureVerifier.Verify(raw, path, [certificate]).Integrity;
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Checks a file with the library.</summary>
    /// <param name="file">The file.</param>
    /// <param name="certificate">The trusted signer.</param>
    /// <returns>Whether the library finds it intact.</returns>
    private static bool LibraryIntact(byte[] file, System.Security.Cryptography.X509Certificates.X509Certificate2 certificate)
    {
        var report = SignatureFixtures.Validate(file, certificate)[0];
        return report.DigestValid && report.SignatureValid && report.CoversWholeDocument;
    }

    /// <summary>The signature update for the sample's field, with a sub-filter.</summary>
    /// <param name="subFilter">The sub-filter.</param>
    /// <returns>The objects.</returns>
    private static Dictionary<int, string> SignedUpdate(string subFilter) => new()
    {
        [SignatureSamples.SignatureField] = "<< /Type /Annot /Subtype /Widget /FT /Sig /T (Signature1) /Rect [0 0 0 0] /P 3 0 R /V 7 0 R >>",
        [SignatureSamples.SignatureValue] = PdfSigning.SignatureDictionary(subFilter, string.Empty),
    };
}
