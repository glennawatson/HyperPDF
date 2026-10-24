// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Forms;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Tests that form design on a certified document is reported as a change its permissions do not allow.</summary>
public sealed class FormFieldSignedDocumentTests
{
    /// <summary>DocMDP level 1: no changes.</summary>
    private const int NoChanges = 1;

    /// <summary>DocMDP level 2: form filling and signing.</summary>
    private const int FormFilling = 2;

    /// <summary>DocMDP level 3: also annotations.</summary>
    private const int Annotating = 3;

    /// <summary>The name of the field the tests add.</summary>
    private const string AddedName = "Added";

    /// <summary>Adding a field to a certified document is a change of the form's structure that no DocMDP level allows.</summary>
    /// <param name="permission">The DocMDP level.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(NoChanges)]
    [Arguments(FormFilling)]
    [Arguments(Annotating)]
    public async Task AddingAFieldIsReportedAsDisallowed(int permission)
    {
        using var document = PdfDocumentReader.Open(CreateCertified(permission), null);
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(AddedName, 1));

        var report = PdfDocumentSignatureEffects.GetSignatureEffects(document, true);
        await Assert.That((report.Kinds & PdfChangeKinds.Other) != 0).IsTrue();
        await Assert.That((report.Signatures[0].DisallowedKinds & PdfChangeKinds.Other) != 0).IsTrue();
        await Assert.That(report.Signatures[0].RemainsValid).IsFalse();
    }

    /// <summary>Creates a one page document certified at a DocMDP level, with one text field.</summary>
    /// <param name="permission">The DocMDP level.</param>
    /// <returns>The file bytes.</returns>
    private static byte[] CreateCertified(int permission) =>
        MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R 6 0 R] /SigFlags 3 >> /Perms << /DocMDP 5 0 R >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Annots [4 0 R 6 0 R] >>",
            "<< /FT /Sig /T (Cert) /V 5 0 R /Type /Annot /Subtype /Widget /Rect [0 0 0 0] /P 3 0 R >>",
            string.Create(
                CultureInfo.InvariantCulture,
                $"<< /Type /Sig /ByteRange [0 0 0 0] /Contents <00> /Reference [<< /Type /SigRef /TransformMethod /DocMDP /TransformParams << /P {permission} >> >>] >>"),
            "<< /FT /Tx /T (Name) /V (Old) /Type /Annot /Subtype /Widget /Rect [10 10 100 30] /P 3 0 R >>");
}
