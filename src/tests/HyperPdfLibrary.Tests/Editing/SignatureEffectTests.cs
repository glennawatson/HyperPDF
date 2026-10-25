// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for <see cref="PdfDocumentSignatureEffects.GetSignatureEffects"/>.</summary>
public sealed class SignatureEffectTests
{
    /// <summary>DocMDP level 2: form filling and signing.</summary>
    private const int FormFilling = 2;

    /// <summary>DocMDP level 3: also annotations.</summary>
    private const int Annotating = 3;

    /// <summary>The text field's object number.</summary>
    private const int TextFieldNumber = 8;

    /// <summary>The page's object number.</summary>
    private const int PageNumber = 3;

    /// <summary>The signature fields in the document.</summary>
    private const int SignatureCount = 2;

    /// <summary>A quarter turn.</summary>
    private const int QuarterTurn = 90;

    /// <summary>With no edits every signature keeps its bytes and permissions.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoEditsKeepEverySignature()
    {
        using var document = PdfDocumentReader.Open(CreateSigned(FormFilling), null);
        var report = PdfDocumentSignatureEffects.GetSignatureEffects(document, true);
        await Assert.That(report.Kinds).IsEqualTo(PdfChangeKinds.None);
        await Assert.That(report.Signatures.Count).IsEqualTo(SignatureCount);
        await Assert.That(report.Signatures[0].FieldName).IsEqualTo("Cert");
        await Assert.That(report.Signatures[0].IsCertification).IsTrue();
        await Assert.That(report.Signatures[0].Permission).IsEqualTo(FormFilling);
        await Assert.That(report.Signatures[0].RemainsValid).IsTrue();
        await Assert.That(report.Signatures[1].IsCertification).IsFalse();
        await Assert.That(report.Signatures[1].RemainsValid).IsTrue();
    }

    /// <summary>Filling a field is allowed at level 2 but breaks the approval signature that locks it; a rewrite breaks both.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormFillIsAllowedUnlessLocked()
    {
        using var document = PdfDocumentReader.Open(CreateSigned(FormFilling), null);
        var field = StoreReading.GetDictionary(document.Objects, new(TextFieldNumber, 0))!.Clone();
        field.Set(KnownName.V, PdfValue.FromString("New"u8.ToArray()));
        StoreEditing.Replace(document.Objects, new(TextFieldNumber, 0), PdfValue.FromDictionary(field));
        var report = PdfDocumentSignatureEffects.GetSignatureEffects(document, true);
        await Assert.That(report.Kinds).IsEqualTo(PdfChangeKinds.FormFill);
        await Assert.That(report.ChangedFields).IsEquivalentTo((string[])["Name"]);
        await Assert.That(report.Signatures[0].RemainsValid).IsTrue();
        await Assert.That(report.Signatures[1].LockedFieldsChanged).IsEquivalentTo((string[])["Name"]);
        await Assert.That(report.Signatures[1].RemainsValid).IsFalse();
        var rewrite = PdfDocumentSignatureEffects.GetSignatureEffects(document, false);
        await Assert.That(rewrite.Signatures[0].KeepsSignedBytes).IsFalse();
        await Assert.That(rewrite.Signatures[0].RemainsValid).IsFalse();
    }

    /// <summary>Adding an annotation is disallowed at level 2 and allowed at level 3.</summary>
    /// <param name="permission">The DocMDP level.</param>
    /// <param name="allowed">Whether the change is allowed.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(FormFilling, false)]
    [Arguments(Annotating, true)]
    public async Task AnnotationsNeedLevelThree(int permission, bool allowed)
    {
        using var document = PdfDocumentReader.Open(CreateSigned(permission), null);
        var note = new PdfDictionary(document.Objects);
        note.Set(KnownName.Type, PdfValue.FromName(KnownName.Annot));
        note.Set(KnownName.Subtype, PdfValue.FromName(KnownName.Square));
        note.Set(KnownName.Rect, PdfValue.FromArray(new PdfRectangle(0, 0, 1, 1).ToArray(document.Objects)));
        var noteId = StoreEditing.Add(document.Objects, PdfValue.FromDictionary(note));
        var page = PdfDocumentPages.GetPage(document, 0).Dictionary.Clone();
        var annots = page.GetArray(KnownName.Annots)!.Clone();
        annots.Add(PdfValue.FromReference(noteId));
        page.Set(KnownName.Annots, PdfValue.FromArray(annots));
        StoreEditing.Replace(document.Objects, new(PageNumber, 0), PdfValue.FromDictionary(page));
        var report = PdfDocumentSignatureEffects.GetSignatureEffects(document, true);
        await Assert.That(report.Kinds).IsEqualTo(PdfChangeKinds.Annotations);
        await Assert.That(report.Signatures[0].RemainsValid).IsEqualTo(allowed);
        await Assert.That(report.Signatures[1].RemainsValid).IsTrue();
    }

    /// <summary>Page and metadata changes are never allowed by a certification.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageAndMetadataChangesAreDisallowed()
    {
        using var document = PdfDocumentReader.Open(CreateSigned(Annotating), null);
        PdfDocumentPageOperations.SetRotation(document, 0, QuarterTurn);
        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = "Changed" });
        var report = PdfDocumentSignatureEffects.GetSignatureEffects(document, true);
        await Assert.That(report.Kinds).IsEqualTo(PdfChangeKinds.PageChanges | PdfChangeKinds.Metadata);
        await Assert.That(report.Signatures[0].DisallowedKinds).IsEqualTo(PdfChangeKinds.PageChanges | PdfChangeKinds.Metadata);
        await Assert.That(report.Signatures[1].DisallowedKinds).IsEqualTo(PdfChangeKinds.None);
    }

    /// <summary>
    /// Creates a one page document certified at a DocMDP level, with an approval signature whose /Lock includes the
    /// text field "Name".
    /// </summary>
    /// <param name="permission">The DocMDP level.</param>
    /// <returns>The file bytes.</returns>
    private static byte[] CreateSigned(int permission) =>
        MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 6 0 R 8 0 R] /SigFlags 3 >> /Perms << /DocMDP 5 0 R >> >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R 6 0 R 8 0 R] >>",
        "<< /FT /Sig /T (Cert) /V 5 0 R /Type /Annot /Subtype /Widget /Rect [0 0 0 0] /P 3 0 R >>",
        string.Create(
        CultureInfo.InvariantCulture,
        $"<< /Type /Sig /ByteRange [0 0 0 0] /Contents <00> /Reference [<< /Type /SigRef /TransformMethod /DocMDP /TransformParams << /P {permission} >> >>] >>"),
        "<< /FT /Sig /T (Approval) /V 7 0 R /Lock << /Type /SigFieldLock /Action /Include /Fields [(Name)] >> /Type /Annot /Subtype /Widget /Rect [0 0 0 0] /P 3 0 R >>",
        "<< /Type /Sig /ByteRange [0 0 0 0] /Contents <00> >>",
        "<< /FT /Tx /T (Name) /V (Old) /Type /Annot /Subtype /Widget /Rect [10 10 100 30] /P 3 0 R >>");
}
