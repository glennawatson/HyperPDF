// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Fonts;

/// <summary>Builds one-page PDFs whose /F1 is a non-embedded CJK /Type0 font, so both engines substitute a system font.</summary>
internal static class CjkFontPdf
{
    /// <summary>The Shift-JIS page content: A, then hiragana a, i and u.</summary>
    internal const string ShiftJisContent = "BT /F1 24 Tf 10 60 Td <4182A082A282A4> Tj ET";

    /// <summary>The GB-EUC-H page content: two hanzi, then two more on a second line.</summary>
    internal const string GbEucContent = "BT /F1 24 Tf 10 60 Td <B0A1D6D0> Tj 0 -30 Td <B9FAC8CB> Tj ET";

    /// <summary>The UniKS-UCS2-H page content: three hangul syllables.</summary>
    internal const string UniKsContent = "BT /F1 24 Tf 10 60 Td <AC00D55CAE00> Tj ET";

    /// <summary>The Identity-H page content: Adobe-Japan1 CIDs 843, 845 and 847, hiragana a, i and u.</summary>
    internal const string IdentityJapan1Content = "BT /F1 24 Tf 10 60 Td <034B034D034F> Tj ET";

    /// <summary>The /W entry giving CID 843 a width of 500.</summary>
    internal const string HiraganaAWidth = "/W [843 [500]]";

    /// <summary>The page width in points.</summary>
    private const int PageWidth = 300;

    /// <summary>The page height in points.</summary>
    private const int PageHeight = 100;

    /// <summary>Builds the PDF.</summary>
    /// <param name="encoding">The predefined CMap name, without the slash.</param>
    /// <param name="ordering">The /CIDSystemInfo /Ordering.</param>
    /// <param name="content">The page content.</param>
    /// <param name="cidEntries">More descendant entries, such as /W.</param>
    /// <returns>The PDF bytes.</returns>
    internal static byte[] Build(string encoding, string ordering, string content, string cidEntries)
    {
        var pdf = new RenderTestPdf(PageWidth, PageHeight) { Content = content };
        var descriptor = pdf.AddObject(
            "<< /Type /FontDescriptor /FontName /CjkTest /Flags 4 /FontBBox [0 -141 1000 859] /ItalicAngle 0 /Ascent 859 /Descent -141 /CapHeight 700 /StemV 80 >>");
        var systemInfo = $"/CIDSystemInfo << /Registry (Adobe) /Ordering ({ordering}) /Supplement 2 >>";
        var descendant = pdf.AddObject(Invariant(
            $"<< /Type /Font /Subtype /CIDFontType0 /BaseFont /CjkTest {systemInfo} /FontDescriptor {descriptor} 0 R /DW 1000 {cidEntries} >>"));
        var font = pdf.AddObject(Invariant($"<< /Type /Font /Subtype /Type0 /BaseFont /CjkTest /Encoding /{encoding} /DescendantFonts [{descendant} 0 R] >>"));
        pdf.Resources = Invariant($"/Font << /F1 {font} 0 R >>");
        return pdf.ToBytes();
    }

    /// <summary>Loads /F1 of a PDF built by <see cref="Build"/>.</summary>
    /// <param name="document">The open document.</param>
    /// <returns>The font.</returns>
    /// <exception cref="InvalidOperationException">The font did not load.</exception>
    internal static PdfFont LoadFont(PdfDocument document)
    {
        var fonts = PdfDocumentPages.GetPage(document, 0).Resources?.GetDictionary(KnownName.Font) ?? throw new InvalidOperationException("The page has no fonts.");
        var dictionary = fonts.Get(fonts.GetKeyAt(0)).AsDictionary() ?? throw new InvalidOperationException("The font is not a dictionary.");
        return PdfFontLoader.Load(dictionary) ?? throw new InvalidOperationException("The font did not load.");
    }

    /// <summary>Formats with the invariant culture.</summary>
    /// <param name="text">The interpolated text.</param>
    /// <returns>The text.</returns>
    private static string Invariant(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
