// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Signatures;

namespace HyperPdfLibrary.Tests.Signatures;

/// <summary>Tests signed page shadowing across raw incremental revisions.</summary>
public sealed class PdfSignatureValidatorTests
{
    /// <summary>The number of genuine cross-reference revisions.</summary>
    private const int RevisionCount = 3;

    /// <summary>The number of genuine signatures after reusing a signature field.</summary>
    private const int ResignedCount = 2;

    /// <summary>The unreferenced object appended after two unsigned signature fields.</summary>
    private const int UnsignedUpdateObject = 5;

    /// <summary>Junk before the header and extra EOF markers do not manufacture cross-reference revisions.</summary>
    /// <param name="prefix">Junk before the PDF header.</param>
    /// <param name="cancellationToken">The test's cancellation token.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("")]
    [Arguments("junk bytes\n%%EOF\n")]
    public async Task ValidateSignatures_PrefixedHeaderAndExtraEof_PreservesSignedRevision(string prefix, CancellationToken cancellationToken)
    {
        var signed = SignedPageRevisions.Signed(SignatureFixtures.Signer, prefix);
        byte[] file = [.. signed, .. "%%EOF\n%%EOF\n"u8];
        using var document = await PdfDocumentReader.OpenAsync(file, null, cancellationToken);
        var revisions = PdfDocumentSignatureValidation.GetRevisions(document);
        var report = PdfDocumentSignatureValidation.ValidateSignatures(document, new() { TrustedRoots = [SignatureFixtures.Signer] })[0];

        await Assert.That(document.Objects.HeaderOffset).IsEqualTo(prefix.Length);
        await Assert.That(document.Objects.Version).IsEqualTo("1.7");
        await Assert.That(revisions.Count).IsEqualTo(1);
        await Assert.That(revisions[0].EndOffset).IsEqualTo(signed.LongLength);
        await Assert.That(report.SignatureValid).IsTrue();
        await Assert.That(report.DigestValid).IsTrue();
        await Assert.That(report.ByteRange.RevisionIndex).IsEqualTo(0);
        await Assert.That(report.ModifiedAfterSigning).IsTrue();
        await Assert.That(report.Changes.Length).IsEqualTo(0);
    }

    /// <summary>Changing a signed page's type cannot turn its replacement into a permitted change.</summary>
    /// <param name="replacementIndex">The unsigned replacement's index.</param>
    /// <param name="prefix">Junk before the PDF header.</param>
    /// <param name="cancellationToken">The test's cancellation token.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0, "")]
    [Arguments(1, "")]
    [Arguments(2, "")]
    [Arguments(0, "junk bytes\n%%EOF\n")]
    public async Task ValidateSignatures_PageShadowedAfterSigning_RejectsOverride(int replacementIndex, string prefix, CancellationToken cancellationToken)
    {
        string[] replacements = [SignedPageRevisions.Annotation, SignedPageRevisions.Metadata, SignedPageRevisions.ObjectStream];
        var signed = SignedPageRevisions.Signed(SignatureFixtures.Signer, prefix);
        var file = SignedPageRevisions.Shadow(signed, replacements[replacementIndex]);
        using var document = await PdfDocumentReader.OpenAsync(file, null, cancellationToken);
        var report = PdfDocumentSignatureValidation.ValidateSignatures(document, new() { TrustedRoots = [SignatureFixtures.Signer] })[0];
        var pageChange = Array.Find(report.Changes, static change => change.ObjectNumber == SignedPageRevisions.PageNumber);

        await Assert.That(report.ByteRange.IsValid).IsTrue();
        await Assert.That(report.SignatureValid).IsTrue();
        await Assert.That(report.DigestValid).IsTrue();
        await Assert.That(report.ModifiedAfterSigning).IsTrue();
        await Assert.That(report.CoversWholeDocument).IsFalse();
        await Assert.That(pageChange.Kind).IsEqualTo(PdfModificationKinds.Pages);
        await Assert.That(pageChange.IsPermitted).IsFalse();
        await Assert.That(report.PermittedByMdp).IsFalse();
        await Assert.That(PdfDocumentSignatureValidation.GetRevisions(document).Count).IsEqualTo(RevisionCount);
    }

    /// <summary>An unsigned cross-reference table cannot expose a hidden signed page without a reported page change.</summary>
    /// <param name="cancellationToken">The test's cancellation token.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidateSignatures_XrefRepointsToHiddenPage_RejectsOverride(CancellationToken cancellationToken)
    {
        var signed = SignedPageRevisions.SignedWithHiddenPage(SignatureFixtures.Signer);
        var file = SignedPageRevisions.RepointPage(signed);
        using var document = await PdfDocumentReader.OpenAsync(file, null, cancellationToken);
        var report = PdfDocumentSignatureValidation.ValidateSignatures(document, new() { TrustedRoots = [SignatureFixtures.Signer] })[0];

        await Assert.That(report.SignatureValid).IsTrue();
        await Assert.That(report.DigestValid).IsTrue();
        await Assert.That(report.ModifiedAfterSigning).IsTrue();
        await Assert.That(report.ChangeKinds).IsEqualTo(PdfModificationKinds.Pages);
        await Assert.That(report.PermittedByMdp).IsFalse();
    }

    /// <summary>Unsigned catalog and field overrides cannot remove the signed certification's authority.</summary>
    /// <param name="replacementIndex">The current catalog or signature field override.</param>
    /// <param name="cancellationToken">The test's cancellation token.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(0)]
    [Arguments(1)]
    [Arguments(2)]
    [Arguments(3)]
    [Arguments(4)]
    public async Task ValidateSignatures_SignedAuthorityRemoved_RejectsPageOverride(int replacementIndex, CancellationToken cancellationToken)
    {
        var signed = SignedPageRevisions.Signed(SignatureFixtures.Signer, string.Empty);
        var file = SignedPageRevisions.RemoveSignatureAuthority(signed, replacementIndex);
        using var document = await PdfDocumentReader.OpenAsync(file, null, cancellationToken);
        var reports = PdfDocumentSignatureValidation.ValidateSignatures(document, new() { TrustedRoots = [SignatureFixtures.Signer] });

        await Assert.That(reports.Count).IsEqualTo(1);
        var report = reports[0];
        await Assert.That(report.Signature.IsCertification).IsTrue();
        await Assert.That(report.Signature.DocMdp).IsEqualTo(PdfMdpPermission.AnnotateFormFillAndSign);
        await Assert.That(report.DigestValid).IsTrue();
        await Assert.That(report.SignatureValid).IsTrue();
        await Assert.That(report.ModifiedAfterSigning).IsTrue();
        await Assert.That((report.ChangeKinds & PdfModificationKinds.Pages) != 0).IsTrue();
        await Assert.That(report.PermittedByMdp).IsFalse();
    }

    /// <summary>Different signed ranges in one reused field retain both signatures and the first signature's authority.</summary>
    /// <param name="cancellationToken">The test's cancellation token.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidateSignatures_FieldSignedAgain_RetainsBothSignatures(CancellationToken cancellationToken)
    {
        var signed = SignedPageRevisions.Signed(SignatureFixtures.Signer, string.Empty);
        var update = IncrementalPdf.Append(signed, new Dictionary<int, string>
        {
            [1] = "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] >> >>",
            [SignedPageRevisions.PageNumber] = "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Rotate 90 >>",
            [SignedPageRevisions.PageNumber + ResignedCount] = PdfSigning.SignatureDictionary("adbe.pkcs7.detached", string.Empty),
        });
        var file = PdfSigning.SignDetached(update, SignatureFixtures.Signer);
        using var document = await PdfDocumentReader.OpenAsync(file, null, cancellationToken);
        var reports = PdfDocumentSignatureValidation.ValidateSignatures(document, new() { TrustedRoots = [SignatureFixtures.Signer] });

        await Assert.That(reports.Count).IsEqualTo(ResignedCount);
        await Assert.That(reports[0].Signature.Field.Index).IsEqualTo(0);
        await Assert.That(reports[1].Signature.Field.Index).IsEqualTo(1);
        await Assert.That(reports[0].Signature.FieldName).IsEqualTo(reports[1].Signature.FieldName);
        await Assert.That(reports[0].ByteRange.SignedEnd).IsEqualTo(signed.LongLength);
        await Assert.That(reports[1].ByteRange.SignedEnd).IsEqualTo(file.LongLength);
        await Assert.That(reports[0].DigestValid).IsTrue();
        await Assert.That(reports[1].DigestValid).IsTrue();
        await Assert.That(reports[0].Signature.IsCertification).IsTrue();
        await Assert.That(reports[1].Signature.IsCertification).IsFalse();
        await Assert.That(reports[0].PermittedByMdp).IsFalse();
    }

    /// <summary>Removing a signed field lock cannot authorize changing the locked form field.</summary>
    /// <param name="cancellationToken">The test's cancellation token.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidateSignatures_SignedFieldLockRemoved_RejectsLockedFormFill(CancellationToken cancellationToken)
    {
        var signed = SignatureSamples.Signed(SignatureFixtures.Signer, 0, "<< /Type /SigFieldLock /Action /Include /Fields [(Name)] >>");
        var unlocked = IncrementalPdf.Append(signed, new Dictionary<int, string>
        {
            [SignatureSamples.SignatureField] = "<< /Type /Annot /Subtype /Widget /FT /Sig /T (Signature1) /Rect [0 0 0 0] /P 3 0 R /V 7 0 R >>",
        });
        var file = SignatureSamples.FillForm(unlocked);
        using var document = await PdfDocumentReader.OpenAsync(file, null, cancellationToken);
        var reports = PdfDocumentSignatureValidation.ValidateSignatures(document, new() { TrustedRoots = [SignatureFixtures.Signer] });

        await Assert.That(reports.Count).IsEqualTo(1);
        await Assert.That(reports[0].DigestValid).IsTrue();
        await Assert.That(reports[0].Signature.Lock).IsNotNull();
        await Assert.That(reports[0].Signature.Lock!.Fields).IsEquivalentTo(["Name"]);
        await Assert.That(reports[0].PermittedByMdp).IsFalse();
    }

    /// <summary>Historical deduplication does not collapse distinct current unsigned signature fields.</summary>
    /// <param name="cancellationToken">The test's cancellation token.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task GetSignatureDetails_UnsignedFieldsAfterUpdate_RetainsEveryField(CancellationToken cancellationToken)
    {
        var unsigned = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [3 0 R 4 0 R] >> >>",
            "<< /Type /Pages /Kids [] /Count 0 >>",
            "<< /FT /Sig /T (First) >>",
            "<< /FT /Sig /T (Second) >>");
        var file = IncrementalPdf.Append(unsigned, new Dictionary<int, string> { [UnsignedUpdateObject] = "(unreferenced update)" });
        using var document = await PdfDocumentReader.OpenAsync(file, null, cancellationToken);
        var details = PdfDocumentSignatureValidation.GetSignatureDetails(document);

        await Assert.That(details.Count).IsEqualTo(ResignedCount);
        await Assert.That(details[0].FieldName).IsEqualTo("First");
        await Assert.That(details[1].FieldName).IsEqualTo("Second");
        await Assert.That(details[0].IsSigned).IsFalse();
        await Assert.That(details[1].IsSigned).IsFalse();
    }
}
