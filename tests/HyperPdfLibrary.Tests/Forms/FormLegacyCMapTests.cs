// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Tests field text in fonts written with legacy multi-byte CMaps (ISO 32000-2 9.7.5), which mix one-byte and two-byte codes.</summary>
public sealed class FormLegacyCMapTests
{
    /// <summary>The Shift-JIS CMap.</summary>
    private const string ShiftJis = "90ms-RKSJ-H";

    /// <summary>The Adobe-Japan1 character collection.</summary>
    private const string Japan1 = "Japan1";

    /// <summary>A text field widget.</summary>
    private const string TextField = "/FT /Tx /T (Name)";

    /// <summary>The number of lines of the multi-line test.</summary>
    private const int TwoLines = 2;

    /// <summary>The bytes of the Japanese sun, book and Latin A characters in Shift-JIS.</summary>
    private static readonly byte[] SunBookA = [0x93, 0xFA, 0x96, 0x7B, 0x41];

    /// <summary>The bytes of halfwidth katakana A and the Japanese sun character in Shift-JIS.</summary>
    private static readonly byte[] KatakanaSun = [0xB1, 0x93, 0xFA];

    /// <summary>The bytes of the Chinese characters for middle and writing, and Latin A, in GBK.</summary>
    private static readonly byte[] MiddleWritingA = [0xD6, 0xD0, 0xCE, 0xC4, 0x41];

    /// <summary>The bytes of the Japanese sun and book characters in Shift-JIS.</summary>
    private static readonly byte[] SunBook = [0x93, 0xFA, 0x96, 0x7B];

    /// <summary>Japanese text in Shift-JIS (90ms-RKSJ-H) is written as one-byte and two-byte codes, in the field's own Type0 font.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShiftJisTextUsesTheCMapsCodes()
    {
        using var document = await OpenAsync(ShiftJis, Japan1, TextField);

        _ = PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget, "日本A");
        var shown = FormTestPdf.FirstShownString(FormTestPdf.Content(document));

        await Assert.That(shown).IsEquivalentTo(SunBookA);
    }

    /// <summary>Halfwidth katakana, which Shift-JIS writes as a single byte above 0xA0, keeps its one-byte code.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShiftJisHalfwidthKatakanaIsOneByte()
    {
        using var document = await OpenAsync(ShiftJis, Japan1, TextField);

        _ = PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget, "ｱ日");
        var shown = FormTestPdf.FirstShownString(FormTestPdf.Content(document));

        await Assert.That(shown).IsEquivalentTo(KatakanaSun);
    }

    /// <summary>Simplified Chinese text in GBK (GBK-EUC-H) is written as the two-byte GBK codes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GbkTextUsesTheCMapsCodes()
    {
        using var document = await OpenAsync("GBK-EUC-H", "GB1", TextField);

        _ = PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget, "中文A");
        var shown = FormTestPdf.FirstShownString(FormTestPdf.Content(document));

        await Assert.That(shown).IsEquivalentTo(MiddleWritingA);
    }

    /// <summary>The appearance names the field's Type0 font, not the built-in Helvetica the field fell back to before.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AppearanceKeepsTheFieldsType0Font()
    {
        using var document = await OpenAsync(ShiftJis, Japan1, TextField);

        _ = PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget, "日本");
        var fonts = FormTestPdf.Stream(document).Dictionary.GetDictionary(KnownName.Resources)!.GetDictionary(KnownName.Font)!;

        await Assert.That(fonts.Count).IsEqualTo(1);
        await Assert.That(fonts.GetDictionary(document.Objects.Names.Intern("Helv"))!.IsName(KnownName.Subtype, KnownName.Type0)).IsTrue();
    }

    /// <summary>A multi-line value breaks between mixed-length codes without splitting a two-byte code.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MultilineShiftJisKeepsCodesWhole()
    {
        using var document = await OpenAsync(ShiftJis, Japan1, $"{TextField} /Ff 4096");

        _ = PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), 0, FormTestPdf.Widget, "日本\n日");
        var content = FormTestPdf.Content(document);

        await Assert.That(content.Split(" Tj").Length - 1).IsEqualTo(TwoLines);
        await Assert.That(FormTestPdf.FirstShownString(content)).IsEquivalentTo(SunBook);
    }

    /// <summary>
    /// Opens a form whose field uses a Type0 font with a predefined CMap, after loading the font the async way so the CMap, the
    /// character collection's Unicode table and the fallback face are generated and cached. That data is fetched on demand, so a
    /// test cannot count on another test having fetched it first.
    /// </summary>
    /// <param name="encoding">The CMap name.</param>
    /// <param name="ordering">The character collection name in the font's CIDSystemInfo.</param>
    /// <param name="widgetEntries">The widget and field entries.</param>
    /// <returns>The document.</returns>
    private static async Task<PdfDocument> OpenAsync(string encoding, string ordering, string widgetEntries)
    {
        var document = FormTestPdf.Open(Font(encoding, ordering), widgetEntries);
        var font = document.Catalog.GetDictionary(KnownName.AcroForm)!.GetDictionary(KnownName.DR)!.GetDictionary(KnownName.Font)!.GetDictionary(document.Objects.Names.Intern("Helv"))!;
        _ = await PdfFontLoader.LoadAsync(font, CancellationToken.None);
        return document;
    }

    /// <summary>Gets the dictionary of a Type0 font with a predefined CMap and no embedded program.</summary>
    /// <param name="encoding">The CMap name.</param>
    /// <param name="ordering">The character collection: <c>Japan1</c>, <c>GB1</c>, <c>CNS1</c> or <c>Korea1</c>.</param>
    /// <returns>The font dictionary text.</returns>
    private static string Font(string encoding, string ordering) =>
        $"<< /Type /Font /Subtype /Type0 /BaseFont /TestFont /Encoding /{encoding} /DescendantFonts [<< /Type /Font /Subtype /CIDFontType2 /BaseFont /TestFont "
        + $"/CIDSystemInfo << /Registry (Adobe) /Ordering ({ordering}) /Supplement 2 >> /DW 1000 /FontDescriptor << /Type /FontDescriptor /FontName /TestFont /Flags 6 "
        + "/FontBBox [0 -137 1000 859] /ItalicAngle 0 /Ascent 859 /Descent -137 /CapHeight 709 /StemV 69 >> >>] >>";
}
