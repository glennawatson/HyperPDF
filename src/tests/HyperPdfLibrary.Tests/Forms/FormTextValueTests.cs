// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Checks appearance decoding of stored field values.</summary>
public sealed class FormTextValueTests
{
    /// <summary>String and array values retain their PDF text decoding.</summary>
    /// <param name="value">The stored PDF value.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("(Stored)")]
    [Arguments("<FEFF00530074006F007200650064>")]
    [Arguments("[<EFBBBF53746F726564> (Ignored)]")]
    public async Task DrawsEncodedStoredValues(string value)
    {
        using var document = FormTestPdf.Open(FormTestPdf.Helvetica, $"/FT /Tx /T (Text) /V {value}");

        _ = PdfFormEditing.RegenerateAppearance(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget);

        await Assert.That(FormTestPdf.Content(document)).Contains("(Stored) Tj");
    }

    /// <summary>A tolerated stream value is decoded as PDF text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawsEncodedStreamValue()
    {
        using var document = FormTestPdf.Open(FormTestPdf.Helvetica, "/FT /Tx /T (Text)");
        var stream = new PdfStream(new(document.Objects), PdfText.EncodeUtf8("Stored"));
        FormTestPdf.Annotation(document).Set(KnownName.V, PdfValue.FromStream(stream));

        _ = PdfFormEditing.RegenerateAppearance(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget);

        await Assert.That(FormTestPdf.Content(document)).Contains("(Stored) Tj");
    }
}
