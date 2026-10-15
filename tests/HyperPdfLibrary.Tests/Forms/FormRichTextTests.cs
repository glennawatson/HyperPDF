// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Xml;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Tests the rich text a text field draws from <c>/RV</c> and <c>/DS</c>, and the XHTML it stores (ISO 32000-2 12.7.4.3).</summary>
public sealed class FormRichTextTests
{
    /// <summary>The XHTML namespace.</summary>
    private const string XhtmlNamespace = "http://www.w3.org/1999/xhtml";

    /// <summary>The flags of a multiline rich text field: bit 13 and bit 26.</summary>
    private const string RichMultiline = "/Ff 33558528";

    /// <summary>The rich value tests draw: bold, red 14 point and underlined runs.</summary>
    private const string StyledValue =
        "(<body xmlns=\"http://www.w3.org/1999/xhtml\"><p>Hello <b>bold</b> <span style=\"color:#FF0000;font-size:14pt\">red</span> <u>under</u></p></body>)";

    /// <summary>Repeated appearances observe changes to both rich XML and its default style.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepeatedRichAppearanceObservesSourceChanges()
    {
        using var document = FormTestPdf.Open(FormTestPdf.Helvetica, $"/FT /Tx /T (Rich) {RichMultiline} /DS (font-size:12pt) /RV (<body xmlns=\"{XhtmlNamespace}\"><p>First</p></body>)");
        var form = PdfDocumentForms.GetForm(document);
        _ = PdfFormEditing.RegenerateAppearance(form, 0, FormTestPdf.Widget);
        var first = FormTestPdf.Content(document);
        _ = PdfFormEditing.RegenerateAppearance(form, 0, FormTestPdf.Widget);
        await Assert.That(FormTestPdf.Content(document)).IsEqualTo(first);

        FormTestPdf.Annotation(document).Set(KnownName.DS, PdfValue.FromString("font-size:18pt"u8.ToArray()));
        _ = PdfFormEditing.RegenerateAppearance(form, 0, FormTestPdf.Widget);
        await Assert.That(FormTestPdf.Content(document)).Contains("18 Tf");

        FormTestPdf.Annotation(document).Set(document.Objects.Names.Intern("RV"u8), PdfValue.FromString(PdfText.Encode($"<body xmlns=\"{XhtmlNamespace}\"><p>Next</p></body>")));
        _ = PdfFormEditing.RegenerateAppearance(form, 0, FormTestPdf.Widget);
        await Assert.That(FormTestPdf.Content(document)).Contains("(Next) Tj");
        await Assert.That(FormTestPdf.Content(document)).DoesNotContain("(First) Tj");
    }

    /// <summary>Chunked XML text keeps characters and collapsed spaces across buffer boundaries.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LongTextKeepsWhitespaceAcrossReadChunks()
    {
        const int prefixLength = 511;
        var prefix = new string('a', prefixLength);
        var xml = $"<body xmlns=\"{XhtmlNamespace}\"><p>{prefix}   tail</p><p>next</p></body>";

        await Assert.That(PdfRichText.GetPlainText(xml)).IsEqualTo($"{prefix} tail\nnext");
    }

    /// <summary>An annotation's XML stream is read using its declared encoding.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PopupStreamHonorsItsXmlEncoding()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        var annotation = new PdfDictionary(document.Objects);
        var xml = System.Text.Encoding.Unicode.GetBytes($"<?xml version=\"1.0\" encoding=\"utf-16\"?><body xmlns=\"{XhtmlNamespace}\"><p>café</p></body>");
        annotation.Set(KnownName.RC, PdfValue.FromStream(new(new(document.Objects), xml)));

        await Assert.That(PdfRichText.GetPopupText(annotation)).IsEqualTo("café");
    }

    /// <summary>Runs in other fonts, sizes and colours are drawn in them, and the fonts go in the appearance's resources and the form's defaults.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task StyledRunsAreDrawnInTheirOwnFontsSizesAndColors()
    {
        using var document = FormTestPdf.Open(FormTestPdf.Helvetica, $"/FT /Tx /T (Rich) {RichMultiline} /V (plain) /RV {StyledValue}");

        var drawn = PdfFormEditing.RegenerateAppearance(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget);
        var content = FormTestPdf.Content(document);
        var fonts = FormTestPdf.Stream(document).Dictionary.GetDictionary(KnownName.Resources)!.GetDictionary(KnownName.Font)!;
        var defaults = document.Catalog.GetDictionary(KnownName.AcroForm)!.GetDictionary(KnownName.DR)!.GetDictionary(KnownName.Font)!;

        await Assert.That(drawn).IsTrue();
        await Assert.That(content).Contains("/RtHelvB 12 Tf");
        await Assert.That(content).Contains("/Helv 14 Tf");
        await Assert.That(content).Contains("1 0 0 rg");
        await Assert.That(content).Contains(" re\nf");
        await Assert.That(fonts.ContainsKey(document.Objects.Names.Intern("RtHelvB"))).IsTrue();
        await Assert.That(defaults.ContainsKey(document.Objects.Names.Intern("RtHelvB"))).IsTrue();
    }

    /// <summary>The rich value is drawn instead of the plain value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RichValueIsDrawnInsteadOfThePlainValue()
    {
        using var document = FormTestPdf.Open(FormTestPdf.Helvetica, $"/FT /Tx /T (Rich) {RichMultiline} /V (PLAIN) /RV (<body xmlns=\"{XhtmlNamespace}\"><p>Rich</p></body>)");

        _ = PdfFormEditing.RegenerateAppearance(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget);
        var content = FormTestPdf.Content(document);

        await Assert.That(content).Contains("(Rich) Tj");
        await Assert.That(content).DoesNotContain("PLAIN");
    }

    /// <summary>A rich value that is not well formed XML leaves the plain value to be drawn.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MalformedRichValueFallsBackToThePlainValue()
    {
        using var document = FormTestPdf.Open(FormTestPdf.Helvetica, $"/FT /Tx /T (Rich) {RichMultiline} /V (PLAIN) /RV (<body xmlns=\"{XhtmlNamespace}\"><p>unclosed)");

        _ = PdfFormEditing.RegenerateAppearance(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget);

        await Assert.That(FormTestPdf.Content(document)).Contains("(PLAIN) Tj");
    }

    /// <summary>A document type declaration is refused, so an external entity cannot be read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RichValueWithADocumentTypeDeclarationIsRefused()
    {
        using var document = FormTestPdf.Open(
            FormTestPdf.Helvetica,
            $"/FT /Tx /T (Rich) {RichMultiline} /V (PLAIN) /RV (<!DOCTYPE body [<!ENTITY x \"boom\">]><body xmlns=\"{XhtmlNamespace}\"><p>&x;</p></body>)");

        _ = PdfFormEditing.RegenerateAppearance(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget);

        await Assert.That(FormTestPdf.Content(document)).Contains("(PLAIN) Tj");
    }

    /// <summary>The default style (<c>/DS</c>) sets the size of text that gives none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultStyleSetsTheSize()
    {
        using var document = FormTestPdf.Open(
            FormTestPdf.Helvetica,
            $"/FT /Tx /T (Rich) {RichMultiline} /DS (font: 20pt Helvetica; color:#0000FF) /RV (<body xmlns=\"{XhtmlNamespace}\"><p>Big</p></body>)");

        _ = PdfFormEditing.RegenerateAppearance(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget);
        var content = FormTestPdf.Content(document);

        await Assert.That(content).Contains("/RtHelv 20 Tf");
        await Assert.That(content).Contains("0 0 1 rg");
    }

    /// <summary>Setting the text of a rich text field stores a valid XHTML body with a paragraph for each line, and escapes markup characters.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavedRichValueIsValidXhtml()
    {
        using var document = FormTestPdf.Open(FormTestPdf.Helvetica, $"/FT /Tx /T (Rich) {RichMultiline}");
        var names = document.Objects.Names;

        var set = PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget, "Line one\nA < B & \"C\"");
        var rich = FormTestPdf.Annotation(document).GetText(names.Intern("RV"))!;

        await Assert.That(set).IsTrue();
        await Assert.That(ReadStrictly(rich)).IsEqualTo("body|p|p");
        await Assert.That(PdfRichText.GetPlainText(rich)).IsEqualTo("Line one\nA < B & \"C\"");
    }

    /// <summary>The stored rich value uses the field's default style.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavedRichValueKeepsTheDefaultStyle()
    {
        using var document = FormTestPdf.Open(FormTestPdf.Helvetica, $"/FT /Tx /T (Rich) {RichMultiline} /DS (font: Times 18pt; color:#336699)");

        _ = PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget, "x");
        var rich = FormTestPdf.Annotation(document).GetText(document.Objects.Names.Intern("RV"))!;

        await Assert.That(rich).Contains("font: Times 18pt; color:#336699");
    }

    /// <summary>A pop-up shows the plain text of the annotation's rich text, and falls back to its contents.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PopupTextComesFromTheRichContents()
    {
        using var rich = FormTestPdf.Open(
            FormTestPdf.Helvetica,
            $"/FT /Tx /T (Rich) /Contents (plain contents) /RC (<body xmlns=\"{XhtmlNamespace}\"><p>Rich <b>text</b></p><p>second</p></body>)");
        using var plain = FormTestPdf.Open(FormTestPdf.Helvetica, "/FT /Tx /T (Rich) /Contents (plain contents) /RC (<body><p>broken)");

        await Assert.That(PdfRichText.GetPopupText(FormTestPdf.Annotation(rich))).IsEqualTo("Rich text\nsecond");
        await Assert.That(PdfRichText.GetPopupText(FormTestPdf.Annotation(plain))).IsEqualTo("plain contents");
    }

    /// <summary>A token that is already cancelled stops the drawing before it starts.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelledTokenStopsDrawing()
    {
        using var document = FormTestPdf.Open(FormTestPdf.Helvetica, $"/FT /Tx /T (Rich) {RichMultiline} /V (x) /RV {StyledValue}");
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        var form = PdfDocumentForms.GetForm(document);

        await Assert.That(() => PdfFormEditing.RegenerateAppearance(form, 0, FormTestPdf.Widget, cancelled.Token)).Throws<OperationCanceledException>();
        await Assert.That(async () => await PdfFormAsync.RegenerateAppearanceAsync(form, 0, FormTestPdf.Widget, cancelled.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>The fallback Helvetica goes in the form's default resources when the field's font is missing from the document.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FallbackFontIsAddedToTheDefaultResources()
    {
        var bytes = HyperPdf.TestAssets.MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [4 0 R] /DA (/Missing 12 Tf 0 g) >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Annots [4 0 R] >>",
            "<< /Type /Annot /Subtype /Widget /Rect [20 200 220 230] /P 3 0 R /F 4 /FT /Tx /T (Name) /DA (/Missing 12 Tf 0 g) >>");
        using var document = PdfDocumentReader.Open(bytes, null);

        _ = PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, 0, "x");
        var defaults = document.Catalog.GetDictionary(KnownName.AcroForm)!.GetDictionary(KnownName.DR)!.GetDictionary(KnownName.Font)!;

        await Assert.That(defaults.ContainsKey(document.Objects.Names.Intern("Helv"))).IsTrue();
    }

    /// <summary>Reads XML the strict way and lists the elements in order, joined with bars.</summary>
    /// <param name="xml">The XML text.</param>
    /// <returns>The element names; an element is also checked to be in the XHTML namespace.</returns>
    private static string ReadStrictly(string xml)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, ConformanceLevel = ConformanceLevel.Document };
        var names = new List<string>();
        using var text = new StringReader(xml);
        using var reader = XmlReader.Create(text, settings);
        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
            {
                continue;
            }

            names.Add(reader.NamespaceURI == XhtmlNamespace ? reader.LocalName : $"!{reader.LocalName}");
        }

        return string.Join('|', names);
    }
}
