// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Requirements;
using HyperPdfLibrary.Tests.Document;
using HyperPdfLibrary.Writing;
using static HyperPdfLibrary.Tests.Requirements.RequirementDocuments;

namespace HyperPdfLibrary.Tests.Requirements;

/// <summary>
/// Tests that saving keeps the catalog's <c>/Requirements</c> as it was read unless an edit took out the feature a
/// requirement names, in which case the requirement is removed and the save report says so (ISO 32000-2 12.11.3).
/// </summary>
public sealed class RequirementSaveTests
{
    /// <summary>The requirements of the sample: a supported one, an unsupported one and a type nobody knows.</summary>
    private const string KeptRequirements = "<< /S /Navigation /Penalty 0 >> << /S /Transitions /Penalty 20 >> << /S /ADBE_Vendor /Penalty 5 >>";

    /// <summary>The sample's signature field and signature value, objects 4 and 5.</summary>
    private const string SignatureField = "<< /FT /Sig /T (Sig1) /V 5 0 R >>";

    /// <summary>The sample's signature value dictionary.</summary>
    private const string SignatureValue = "<< /Type /Sig /Filter /Adobe.PPKLite /SubFilter /adbe.pkcs7.detached /ByteRange [0 0 0 0] /Contents <00> >>";

    /// <summary>An incremental save of an edited file keeps every requirement, including unknown ones.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IncrementalSaveKeepsRequirements()
    {
        using var document = OpenWith(KeptRequirements, string.Empty);
        PdfDocumentMetadataEditing.SetMetadata(document, TitleEdit);
        var report = new PdfSaveReport();

        var types = SaveIncremental(document, report);

        await Assert.That(types).IsEquivalentTo((string[])[NavigationType, "Transitions", "ADBE_Vendor"]);
        await Assert.That(report.Requirements.Count).IsEqualTo(0);
    }

    /// <summary>The asynchronous incremental save removes a stale requirement and reports it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsyncIncrementalSaveRemovesStaleRequirement()
    {
        using var document = OpenWith("<< /S /Collection >> << /S /Navigation >>", string.Empty);
        PdfDocumentMetadataEditing.SetMetadata(document, TitleEdit);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        await PdfIncrementalWriter.SaveAsync(document.Objects, output, report, CancellationToken.None);

        await Assert.That(TypesOf(output.ToArray())).IsEquivalentTo((string[])[NavigationType]);
        await Assert.That(report.Requirements.Count).IsEqualTo(1);
    }

    /// <summary>A compact save of a file with no edits writes the requirements as they are, even a stale one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompactSaveWithoutEditsChangesNothing()
    {
        using var document = OpenWith("<< /S /Collection >> << /S /Navigation >>", string.Empty);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default, output, report);

        await Assert.That(TypesOf(output.ToArray())).IsEquivalentTo((string[])["Collection", NavigationType]);
        await Assert.That(report.Requirements.Count).IsEqualTo(0);
    }

    /// <summary>A requirement for a portfolio is removed, with a notice, when the edited file has no collection; the others stay.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditedFileLosesRequirementForMissingCollection()
    {
        using var document = OpenWith("<< /S /Navigation >> << /S /Collection >> << /S /CollectionEditing >> << /S /ADBE_Vendor >>", string.Empty);
        PdfDocumentMetadataEditing.SetMetadata(document, TitleEdit);
        var report = new PdfSaveReport();

        var types = SaveIncremental(document, report);

        await Assert.That(types).IsEquivalentTo((string[])[NavigationType, "ADBE_Vendor"]);
        await Assert.That(report.Requirements.Select(static change => change.Type)).IsEquivalentTo((string[])["Collection", "CollectionEditing"]);
        await Assert.That(report.Requirements[0].Message).Contains("Showing a portfolio of files");
    }

    /// <summary>A compact save of an edited file drops the stale requirement and removes the entry when none are left.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompactSaveRemovesEntryWhenNothingStays()
    {
        using var document = OpenWith("<< /S /Collection >>", string.Empty);
        PdfDocumentMetadataEditing.SetMetadata(document, TitleEdit);
        var report = new PdfSaveReport();
        await using var output = new MemoryStream();

        PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default, output, report);

        using var reopened = PdfDocumentReader.Open(output.ToArray(), null);
        await Assert.That(PdfDocumentRequirements.GetRequirements(reopened).IsEmpty).IsTrue();
        await Assert.That(report.Requirements.Count).IsEqualTo(1);
    }

    /// <summary>A requirement kept from an indirect array survives, and the array is rewritten into the catalog.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IndirectArrayIsReconciled()
    {
        using var document = StructureDocuments.Open("/Version /2.0 /Requirements 4 0 R", "[<< /S /Collection >> << /S /Navigation >>]");
        PdfDocumentMetadataEditing.SetMetadata(document, TitleEdit);

        var types = SaveIncremental(document, new());

        await Assert.That(types).IsEquivalentTo((string[])[NavigationType]);
    }

    /// <summary>Attachments, from the name tree or a file attachment annotation, keep their requirements; without either they go.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AttachmentRequirementFollowsAttachments()
    {
        using var withTree = OpenWith("<< /S /Attachment >>", "/Names << /EmbeddedFiles << /Names [(a.txt) 4 0 R] >> >>", "<< /Type /Filespec /F (a.txt) >>");
        using var withAnnotation = StructureDocuments.OpenPage(
            "/Version /2.0 /Requirements [<< /S /Attachment >>]",
            "/Annots [4 0 R]",
            "<< /Type /Annot /Subtype /FileAttachment /Rect [0 0 10 10] >>");
        using var without = OpenWith("<< /S /Attachment >> << /S /AttachmentEditing >>", string.Empty);
        PdfDocumentMetadataEditing.SetMetadata(withTree, TitleEdit);
        PdfDocumentMetadataEditing.SetMetadata(withAnnotation, TitleEdit);
        PdfDocumentMetadataEditing.SetMetadata(without, TitleEdit);

        await Assert.That(SaveIncremental(withTree, new())).IsEquivalentTo((string[])["Attachment"]);
        await Assert.That(SaveIncremental(withAnnotation, new())).IsEquivalentTo((string[])["Attachment"]);
        await Assert.That(SaveIncremental(without, new())).IsEmpty();
    }

    /// <summary>Removing the signatures drops the validation requirements but keeps the signing one, as the empty field remains.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovingSignaturesReconcilesSignatureRequirements()
    {
        const string requirements = "<< /S /DigSigValidation >> << /S /DigSig >> << /S /DigSigMDP >> << /S /Navigation >>";
        const string form = "/AcroForm << /Fields [4 0 R] /SigFlags 3 >>";
        using var signed = OpenWith(requirements, form, SignatureField, SignatureValue);
        using var unsigned = OpenWith(requirements, form, SignatureField, SignatureValue);
        PdfDocumentMetadataEditing.SetMetadata(signed, TitleEdit);
        var untouched = new PdfSaveReport();

        var whileSigned = SaveIncremental(signed, untouched);
        _ = PdfDocumentSignatureEditing.RemoveSignatures(unsigned);
        var report = new PdfSaveReport();
        var afterRemoval = SaveIncremental(unsigned, report);

        await Assert.That(whileSigned).IsEquivalentTo((string[])["DigSigValidation", "DigSig", "DigSigMDP", NavigationType]);
        await Assert.That(untouched.Requirements.Count).IsEqualTo(0);
        await Assert.That(afterRemoval).IsEquivalentTo((string[])["DigSig", NavigationType]);
        await Assert.That(report.Requirements.Select(static change => change.Type)).IsEquivalentTo((string[])["DigSigValidation", "DigSigMDP"]);
    }

    /// <summary>Without any signature field, even the signing requirement goes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SigningRequirementGoesWithoutSignatureFields()
    {
        using var document = OpenWith("<< /S /DigSig >> << /S /Navigation >>", string.Empty);
        PdfDocumentMetadataEditing.SetMetadata(document, TitleEdit);

        await Assert.That(SaveIncremental(document, new())).IsEquivalentTo((string[])[NavigationType]);
    }
}
