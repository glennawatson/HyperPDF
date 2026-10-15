// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Tests.Tagged;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Tests for the font findings: embedded programs and Unicode mappings (ISO 14289-1 7.21; ISO 14289-2 8.4.5).</summary>
public sealed class AccessibilityFontTests
{
    /// <summary>The first character code the test font defines.</summary>
    private const int FirstCode = 32;

    /// <summary>The number of character codes the test font defines, from the space to the letter z.</summary>
    private const int CodeCount = 91;

    /// <summary>The last character code the test font defines.</summary>
    private const int LastCode = FirstCode + CodeCount - 1;

    /// <summary>A ToUnicode map that gives the letter L the value U+0000 and every other code its own character.</summary>
    private const string InvalidMap = """
        /CIDInit /ProcSet findresource begin 12 dict begin begincmap
        /CMapName /Adobe-Identity-UCS def /CMapType 2 def
        1 begincodespacerange <00> <FF> endcodespacerange
        2 beginbfrange <20> <4B> <0020> <4D> <7A> <004D> endbfrange
        1 beginbfchar <4C> <0000> endbfchar
        endcmap CMapName currentdict /CMap defineresource pop end end
        """;

    /// <summary>A font whose program is not stored in the file is flagged; a font with no program to store is not.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FontsWithoutEmbeddedProgramsAreFlagged()
    {
        var notEmbedded = await AccessibilityPdfs.ReportAsync(new() { Font = static _ => "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>" });
        var type3 = await AccessibilityPdfs.ReportAsync(new());

        await Assert.That(notEmbedded.GetCount(PdfAccessibilityCode.FontNotEmbedded)).IsGreaterThan(0);
        await Assert.That(type3.GetCount(PdfAccessibilityCode.FontNotEmbedded)).IsEqualTo(0);
    }

    /// <summary>A font that gives its shown character codes no Unicode value is flagged once.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FontsWithoutUnicodeMapsAreFlagged()
    {
        var report = await AccessibilityPdfs.ReportAsync(new() { Font = static pdf => UnnamedType3(pdf, string.Empty) });

        await Assert.That(report.GetCount(PdfAccessibilityCode.FontNoUnicodeMap)).IsEqualTo(1);
        await Assert.That(report.GetCount(PdfAccessibilityCode.FontUnicodeMapInvalid)).IsEqualTo(0);
        await Assert.That(report.GetCount(PdfAccessibilityCode.FontNotEmbedded)).IsEqualTo(0);
    }

    /// <summary>A Unicode map that gives a shown code the value U+0000 is flagged as invalid, not as missing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvalidUnicodeValuesAreFlagged()
    {
        var report = await AccessibilityPdfs.ReportAsync(new()
        {
            Font = static pdf => UnnamedType3(pdf, string.Create(CultureInfo.InvariantCulture, $"/ToUnicode {pdf.Add(MiniPdf.Stream(string.Empty, InvalidMap))} 0 R")),
        });

        await Assert.That(report.GetCount(PdfAccessibilityCode.FontUnicodeMapInvalid)).IsEqualTo(1);
        await Assert.That(report.GetCount(PdfAccessibilityCode.FontNoUnicodeMap)).IsEqualTo(0);
    }

    /// <summary>Writes a Type 3 font whose glyph names have no standard Unicode value.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="extra">Extra font entries, such as <c>/ToUnicode</c>.</param>
    /// <returns>The font dictionary.</returns>
    private static string UnnamedType3(TaggedPdfBuilder pdf, string extra)
    {
        var glyph = pdf.Add(MiniPdf.Stream(string.Empty, "1000 0 0 0 750 750 d1 0 0 750 750 re f"));
        var names = string.Concat(Enumerable.Repeat("/zz ", CodeCount));
        var widths = string.Concat(Enumerable.Repeat("1000 ", CodeCount));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Type /Font /Subtype /Type3 /FontBBox [0 0 750 750] /FontMatrix [0.001 0 0 0.001 0 0] /CharProcs << /zz {glyph} 0 R >> "
            + $"/Encoding << /Type /Encoding /Differences [{FirstCode} {names}] >> /FirstChar {FirstCode} /LastChar {LastCode} /Widths [{widths}] {extra} >>");
    }
}
