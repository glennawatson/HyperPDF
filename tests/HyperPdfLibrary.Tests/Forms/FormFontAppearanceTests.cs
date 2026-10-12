// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Tests field appearances in fonts that are not WinAnsi, in Type0 fonts, and with beveled and inset borders.</summary>
public sealed class FormFontAppearanceTests
{
    /// <summary>The character typed first: the Japanese sun character.</summary>
    private const string Nichi = "日";

    /// <summary>Text that WinAnsi would write as a single byte, with e acute.</summary>
    private const string Accented = "café";

    /// <summary>The widget a font test form holds.</summary>
    private const int Widget = 0;

    /// <summary>A font with the MacRoman encoding writes e acute as code 142, not as its WinAnsi code 233.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MacRomanTextUsesTheFontsCodes()
    {
        using var document = Open("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /MacRomanEncoding >>", string.Empty);

        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, Widget, Accented);
        var content = Appearance(document);

        await Assert.That(Encoding.Latin1.GetString(content)).Contains("(caf\\216) Tj");
    }

    /// <summary>A /Differences array decides the code of a character.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DifferencesDecideTheCode()
    {
        using var document = Open("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding << /Type /Encoding /BaseEncoding /WinAnsiEncoding /Differences [200 /eacute] >> >>", string.Empty);

        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, Widget, Accented);

        await Assert.That(Encoding.Latin1.GetString(Appearance(document))).Contains("(caf\\310) Tj");
    }

    /// <summary>A Type0 font with a Unicode CMap is written as two-byte codes, and its font goes in the appearance's resources.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Type0UnicodeCMapWritesTwoByteCodes()
    {
        using var document = Open(
            "<< /Type /Font /Subtype /Type0 /BaseFont /HeiseiMin-W3 /Encoding /UniJIS-UCS2-H /DescendantFonts [<< /Type /Font /Subtype /CIDFontType0 /BaseFont /HeiseiMin-W3 "
            + "/CIDSystemInfo << /Registry (Adobe) /Ordering (Japan1) /Supplement 2 >> /DW 1000 /FontDescriptor << /Type /FontDescriptor /FontName /HeiseiMin-W3 /Flags 6 "
            + "/FontBBox [-123 -257 1001 910] /ItalicAngle 0 /Ascent 723 /Descent -241 /CapHeight 709 /StemV 69 >> >>] >>",
            string.Empty);

        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, Widget, Nichi);
        var content = Appearance(document);
        var resources = Annotation(document).GetDictionary(KnownName.AP)!.GetStream(KnownName.N)!.Dictionary.GetDictionary(KnownName.Resources)!;

        await Assert.That(Encoding.Latin1.GetString(content)).Contains("(e\\345) Tj");
        await Assert.That(resources.GetDictionary(KnownName.Font)!.Count).IsEqualTo(1);
    }

    /// <summary>A Type0 Identity-H font is written with the codes its /ToUnicode map gives the characters.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Type0IdentityUsesToUnicode()
    {
        using var document = Open(
            "<< /Type /Font /Subtype /Type0 /BaseFont /Arial /Encoding /Identity-H /ToUnicode 6 0 R /DescendantFonts [<< /Type /Font /Subtype /CIDFontType2 /BaseFont /Arial "
            + "/CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >> /DW 600 /CIDToGIDMap /Identity /FontDescriptor << /Type /FontDescriptor /FontName /Arial /Flags 32 "
            + "/FontBBox [0 -200 1000 900] /ItalicAngle 0 /Ascent 900 /Descent -200 /CapHeight 700 /StemV 80 >> >>] >>",
            MiniPdf.Stream(
                string.Empty,
                "/CIDInit /ProcSet findresource begin 12 dict begin begincmap /CMapName /Adobe-Identity-UCS def /CMapType 2 def 1 begincodespacerange <0000> <FFFF> endcodespacerange "
                + "2 beginbfchar <0007> <0048> <0008> <0069> endbfchar endcmap CMapName currentdict /CMap defineresource pop end end"));

        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, Widget, "Hi");

        await Assert.That(Encoding.Latin1.GetString(Appearance(document))).Contains("(\\000\\007\\000\\b) Tj");
    }

    /// <summary>A beveled border is drawn as a lit and a shaded L inside a ring, shaded with half the background.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BeveledBorderIsShaded()
    {
        using var document = Open(
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            string.Empty,
            "/BS << /W 2 /S /B >> /MK << /BC [0 0 0] /BG [0.8 0.8 0.8] >>");

        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, Widget, "x");
        var text = Encoding.Latin1.GetString(Appearance(document));

        await Assert.That(text).Contains("1 g");
        await Assert.That(text).Contains("0.4 0.4 0.4 rg");
        await Assert.That(text).Contains("f*");
    }

    /// <summary>An inset border uses the fixed greys.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InsetBorderUsesGreys()
    {
        using var document = Open(
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
            string.Empty,
            "/BS << /W 1 /S /I >> /MK << /BC [0 0 0] >>");

        _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, Widget, "x");
        var text = Encoding.Latin1.GetString(Appearance(document));

        await Assert.That(text).Contains("0.5 g");
        await Assert.That(text).Contains("0.75 g");
    }

    /// <summary>Opens a one-field form whose /Helv font is the given dictionary.</summary>
    /// <param name="font">The font dictionary text.</param>
    /// <param name="extra">The body of object 6, used by fonts that point at a stream.</param>
    /// <returns>The document.</returns>
    private static PdfDocument Open(string font, string extra) => Open(font, extra, "/MK << /BC [0 0 0] >>");

    /// <summary>Opens a one-field form whose /Helv font is the given dictionary.</summary>
    /// <param name="font">The font dictionary text.</param>
    /// <param name="extra">The body of object 6, used by fonts that point at a stream.</param>
    /// <param name="widgetEntries">More widget entries.</param>
    /// <returns>The document.</returns>
    private static PdfDocument Open(string font, string extra, string widgetEntries) => PdfDocumentReader.Open(
        MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [5 0 R] /DA (/Helv 12 Tf 0 g) /DR << /Font << /Helv 4 0 R >> >> >> >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Annots [5 0 R] >>",
            font,
            $"<< /Type /Annot /Subtype /Widget /Rect [20 200 220 230] /P 3 0 R /F 4 /FT /Tx /T (Name) /DA (/Helv 12 Tf 0 g) {widgetEntries} >>",
            extra.Length == 0 ? "<< >>" : extra), null);

    /// <summary>Reads the field's widget annotation.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The widget dictionary.</returns>
    private static PdfDictionary Annotation(PdfDocument document) =>
        PdfDocumentPages.GetPage(document, 0).Dictionary.GetArray(KnownName.Annots)!.GetDictionary(Widget)!;

    /// <summary>Reads the decoded content of the field's appearance.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The content bytes.</returns>
    private static byte[] Appearance(PdfDocument document) =>
        Annotation(document).GetDictionary(KnownName.AP)!.GetStream(KnownName.N)!.DecodeToArray();
}
