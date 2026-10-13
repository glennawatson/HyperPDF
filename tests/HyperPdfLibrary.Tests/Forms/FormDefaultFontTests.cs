// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Tests.Conformance;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Tests adding an embedded font to the form's default resources.</summary>
public sealed class FormDefaultFontTests
{
    /// <summary>The second part of ISO 19005.</summary>
    private const int PdfA2 = 2;

    /// <summary>The resource name of the test font.</summary>
    private const string FontName = "Body";

    /// <summary>The default appearance that names the test font.</summary>
    private const string Appearance = "/Body 12 Tf 0 g";

    /// <summary>The advance of the only glyph.</summary>
    private const float GlyphWidth = 500;

    /// <summary>The size of the font's box.</summary>
    private const float EmSize = 1000;

    /// <summary>Stand-in font file bytes; the form only checks that the program is present.</summary>
    private static readonly byte[] FontBytes = [0, 1, 0, 0];

    /// <summary>An added font lands in the form's resources, can be named by a default appearance, and a name cannot be used twice.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddedFontIsEmbeddedInTheForm()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        PdfFormDefaultFonts.AddFont(document, FontName, TestFont());

        await Assert.That(FormFieldFonts.IsEmbeddedInForm(document, Appearance)).IsTrue();
        await Assert.That(() => PdfFormDefaultFonts.AddFont(document, FontName, TestFont())).Throws<ArgumentException>();
        await Assert.That(PdfDocumentEditing.Undo(document)).IsTrue();
        await Assert.That(FormFieldFonts.IsEmbeddedInForm(document, Appearance)).IsFalse();
    }

    /// <summary>A PDF/A document takes a text field once the form has an embedded font to name.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfADocumentTakesATextFieldWithAnEmbeddedFont()
    {
        var pdf = ConformancePdf.Page();
        pdf.Content = "0 g 0 0 1 1 re f 1 0 0 rg 0 0 1 1 re f";
        ConformancePdf.Claim(pdf, PdfA2, "B", null);
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        PdfFormDefaultFonts.AddFont(document, FontName, TestFont());

        var created = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text("Name", 0) with { DefaultAppearance = Appearance });

        await Assert.That(created.Name).IsEqualTo("Name");
        await Assert.That(PdfFormFieldEditing.GetField(document, "Name")!.DefaultAppearance).IsEqualTo(Appearance);
    }

    /// <summary>Makes a font with the parts embedding needs.</summary>
    /// <returns>The font.</returns>
    private static PdfTrueTypeFontInfo TestFont() => new(FontName, FontBytes, [GlyphWidth], [], new(0, 0, EmSize, EmSize));
}
