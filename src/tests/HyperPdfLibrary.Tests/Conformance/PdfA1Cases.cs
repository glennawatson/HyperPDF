// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>
/// Hand-built PDF/A-1b files for the font rules of ISO 19005-1 Cor.1 and Cor.2 (§6.3.3, §6.3.5 to 6.3.7) and the
/// transparency, annotation and action rules of Cor.2 (§6.4, §6.5.3, §6.6.1). Each passing case has a failing twin.
/// </summary>
internal static class PdfA1Cases
{
    /// <summary>The program width of 'H' in the test font, in thousandths of an em.</summary>
    private const int HWidth = 500;

    /// <summary>A width that does not match the program.</summary>
    private const int WrongWidth = 900;

    /// <summary>The font flags of a non-symbolic font.</summary>
    private const int NonSymbolic = 32;

    /// <summary>The font flags of a symbolic font.</summary>
    private const int Symbolic = 4;

    /// <summary>The bits in a byte of a CIDSet.</summary>
    private const int BitsPerByte = 8;

    /// <summary>The highest bit of a byte.</summary>
    private const int HighBit = 0x80;

    /// <summary>The text that shows 'H' in <c>/F1</c>.</summary>
    private const string ShowH = "BT /F1 12 Tf (H) Tj ET";

    /// <summary>A square drawn without naming a resource.</summary>
    private const string Fill = "0 0 10 10 re f";

    /// <summary>The PDF/A part every case claims.</summary>
    private const int PartOne = 1;

    /// <summary>The WinAnsi encoding entry.</summary>
    private const string WinAnsi = "/Encoding /WinAnsiEncoding";

    /// <summary>The identity CID to glyph map entry.</summary>
    private const string IdentityMap = "/CIDToGIDMap /Identity";

    /// <summary>A printable square annotation's entries.</summary>
    private const string Square = "/Subtype /Square /Rect [0 0 10 10] /F 4";

    /// <summary>A normal appearance that is a stream; <c>{0}</c> stands for its number.</summary>
    private const string NormalStream = "/N {0} 0 R";

    /// <summary>A normal appearance that is a subdictionary with one state; <c>{0}</c> stands for its stream's number.</summary>
    private const string NormalStates = "/N << /On {0} 0 R >>";

    /// <summary>An unembedded Helvetica font resource.</summary>
    private const string Helvetica = "/Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >>";

    /// <summary>A one-pixel grey image's entries.</summary>
    private const string GrayPixel = "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8";

    /// <summary>Gets every case.</summary>
    /// <returns>The cases.</returns>
    internal static PdfA1Case[] All() =>
    [
        new("width-matches", TrueType(HWidth, NonSymbolic, WinAnsi), PdfARule.GlyphWidth, false),
        new("width-mismatch", TrueType(WrongWidth, NonSymbolic, WinAnsi), PdfARule.GlyphWidth, true),
        new("nonsymbolic-winansi", TrueType(HWidth, NonSymbolic, WinAnsi), PdfARule.TrueTypeEncodingPdfA1, false),
        new("nonsymbolic-no-encoding", TrueType(HWidth, NonSymbolic, string.Empty), PdfARule.TrueTypeEncodingPdfA1, true),
        new("symbolic-no-encoding", TrueType(HWidth, Symbolic, string.Empty), PdfARule.TrueTypeSymbolicEncoding, false),
        new("symbolic-encoding", TrueType(HWidth, Symbolic, WinAnsi), PdfARule.TrueTypeSymbolicEncoding, true),
        new("symbolic-one-cmap", TrueType(HWidth, Symbolic, string.Empty), PdfARule.TrueTypeSymbolicCmapPdfA1, false),
        new("cidfont-gid-map", CidFont(IdentityMap, false), PdfARule.CidFontToGidMapPdfA1, false),
        new("cidfont-no-gid-map", CidFont(string.Empty, false), PdfARule.CidFontToGidMapPdfA1, true),
        new("cid-subset-cidset", CidFont(IdentityMap, true), PdfARule.CidSetCompletePdfA1, false),
        new("cid-subset-no-cidset", CidFont(IdentityMap, false, "ABCDEF+"), PdfARule.CidSetCompletePdfA1, true),
        new("unused-font", Page(Helvetica, Fill).ToBytes(), PdfARule.FontEmbedded, false),
        new("used-unembedded-font", Page(Helvetica, ShowH).ToBytes(), PdfARule.FontEmbedded, true),
        new("extgstate-smask-none", State("/SMask /None"), PdfARule.ExtGStateSoftMask, false),
        new("extgstate-smask", State("/SMask << /S /Luminosity /G 4 0 R >>"), PdfARule.ExtGStateSoftMask, true),
        new("image-smask", Image(true), PdfARule.XObjectSoftMaskPdfA1, true),
        new("image-plain", Image(false), PdfARule.XObjectSoftMaskPdfA1, false),
        new("page-group", Grouped(true, false), PdfARule.TransparencyGroupPdfA1, true),
        new("form-group", Grouped(false, true), PdfARule.TransparencyGroupPdfA1, true),
        new("no-group", Grouped(false, false), PdfARule.TransparencyGroupPdfA1, false),
        new("hide-action", Annotated("/Subtype /Link /Rect [0 0 10 10] /F 4 /A << /S /Hide /T (x) >>", null), PdfARule.ActionTypePdfA1, true),
        new("goto-action", Annotated("/Subtype /Link /Rect [0 0 10 10] /F 4 /A << /S /GoTo /D [3 0 R /Fit] >>", null), PdfARule.ActionTypePdfA1, false),
        new("ap-normal-only", Annotated(Square, NormalStream), PdfARule.AnnotationOnlyNormalAppearance, false),
        new("ap-extra-down", Annotated(Square, $"{NormalStream} /D {{0}} 0 R"), PdfARule.AnnotationOnlyNormalAppearance, true),
        new("button-subdictionary", Annotated("/Subtype /Widget /FT /Btn /T (b) /Rect [0 0 10 10] /F 4 /AS /On", NormalStates), PdfARule.AnnotationButtonAppearanceDictionary, false),
        new("button-stream", Annotated("/Subtype /Widget /FT /Btn /T (b) /Rect [0 0 10 10] /F 4", NormalStream), PdfARule.AnnotationButtonAppearanceDictionary, true),
        new("square-stream", Annotated(Square, NormalStream), PdfARule.AnnotationNormalAppearanceStream, false),
        new("square-subdictionary", Annotated(Square, NormalStates), PdfARule.AnnotationNormalAppearanceStream, true),
    ];

    /// <summary>Builds a page that claims PDF/A-1b.</summary>
    /// <param name="resources">The resource entries.</param>
    /// <param name="content">The content.</param>
    /// <returns>The builder.</returns>
    internal static RenderTestPdf Page(string resources, string content)
    {
        var pdf = ConformancePdf.Page();
        ConformancePdf.Claim(pdf, PartOne, "B", null);
        pdf.Resources = resources;
        pdf.Content = content;
        return pdf;
    }

    /// <summary>Builds a page that shows 'H' in an embedded TrueType font.</summary>
    /// <param name="width">The width of 'H' in the font dictionary.</param>
    /// <param name="flags">The font descriptor flags.</param>
    /// <param name="encoding">The encoding entry, or empty.</param>
    /// <returns>The file.</returns>
    private static byte[] TrueType(int width, int flags, string encoding)
    {
        var pdf = Page(string.Empty, ShowH);
        var program = pdf.AddStream(string.Create(CultureInfo.InvariantCulture, $"/Length1 {TestFont.Create().Length}"), TestFont.Create());
        var descriptor = pdf.AddObject(Descriptor("PVLTestSans", flags, string.Create(CultureInfo.InvariantCulture, $"/FontFile2 {program} 0 R")));
        pdf.Resources = string.Create(
            CultureInfo.InvariantCulture,
            $"/Font << /F1 << /Type /Font /Subtype /TrueType /BaseFont /PVLTestSans /FirstChar 72 /LastChar 72 /Widths [{width}] {encoding} /FontDescriptor {descriptor} 0 R >> >>");
        return pdf.ToBytes();
    }

    /// <summary>Builds a page that shows 'H' in an embedded Type 2 CIDFont through Identity-H.</summary>
    /// <param name="mapEntry">The CIDToGIDMap entry, or empty.</param>
    /// <param name="withCidSet">Whether the descriptor holds a complete CIDSet; the font is then named as a subset.</param>
    /// <returns>The file.</returns>
    private static byte[] CidFont(string mapEntry, bool withCidSet) => CidFont(mapEntry, withCidSet, withCidSet ? "ABCDEF+" : string.Empty);

    /// <summary>Builds a page that shows 'H' in an embedded Type 2 CIDFont through Identity-H.</summary>
    /// <param name="mapEntry">The CIDToGIDMap entry, or empty.</param>
    /// <param name="withCidSet">Whether the descriptor holds a complete CIDSet.</param>
    /// <param name="subsetTag">The subset tag before the font name, or empty.</param>
    /// <returns>The file.</returns>
    private static byte[] CidFont(string mapEntry, bool withCidSet, string subsetTag)
    {
        var glyph = TestFont.GlyphOf('H');
        var pdf = Page(string.Empty, string.Create(CultureInfo.InvariantCulture, $"BT /F1 12 Tf <{glyph:X4}> Tj ET"));
        var font = TestFont.Create();
        var program = pdf.AddStream(string.Create(CultureInfo.InvariantCulture, $"/Length1 {font.Length}"), font);
        var extra = string.Create(CultureInfo.InvariantCulture, $"/FontFile2 {program} 0 R");
        if (withCidSet)
        {
            extra += string.Create(CultureInfo.InvariantCulture, $" /CIDSet {pdf.AddStream(string.Empty, CidSet(TestFont.Characters.Length + 1))} 0 R");
        }

        var name = $"{subsetTag}PVLTestSans";
        var descriptor = pdf.AddObject(Descriptor(name, Symbolic, extra));
        var descendant = pdf.AddObject(string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Type /Font /Subtype /CIDFontType2 /BaseFont /{name} /CIDSystemInfo << /Registry (Adobe) /Ordering (Identity) /Supplement 0 >>"
            + $" /DW 500 {mapEntry} /FontDescriptor {descriptor} 0 R >>"));
        pdf.Resources = string.Create(
            CultureInfo.InvariantCulture,
            $"/Font << /F1 << /Type /Font /Subtype /Type0 /BaseFont /{name} /Encoding /Identity-H /DescendantFonts [{descendant} 0 R] >> >>");
        return pdf.ToBytes();
    }

    /// <summary>Writes a font descriptor.</summary>
    /// <param name="name">The font name.</param>
    /// <param name="flags">The flags.</param>
    /// <param name="extra">The font file and other entries.</param>
    /// <returns>The dictionary.</returns>
    private static string Descriptor(string name, int flags, string extra) => string.Create(
        CultureInfo.InvariantCulture,
        $"<< /Type /FontDescriptor /FontName /{name} /Flags {flags} /FontBBox [0 -200 1000 800] /ItalicAngle 0 /Ascent 800 /Descent -200 /CapHeight 700 /StemV 80 {extra} >>");

    /// <summary>Writes a CIDSet that marks every CID below a count.</summary>
    /// <param name="count">The number of CIDs.</param>
    /// <returns>The bit string.</returns>
    private static byte[] CidSet(int count)
    {
        var bits = new byte[(count + BitsPerByte - 1) / BitsPerByte];
        for (var cid = 0; cid < count; cid++)
        {
            bits[cid / BitsPerByte] |= (byte)(HighBit >> (cid % BitsPerByte));
        }

        return bits;
    }

    /// <summary>Builds a page that sets a graphics state and fills.</summary>
    /// <param name="entries">The graphics state's entries.</param>
    /// <returns>The file.</returns>
    private static byte[] State(string entries)
    {
        var pdf = Page(string.Empty, $"/GS gs {Fill}");
        var state = pdf.AddObject($"<< /Type /ExtGState {entries} >>");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/ExtGState << /GS {state} 0 R >>");
        return pdf.ToBytes();
    }

    /// <summary>Builds a page that draws a one-pixel image, with or without a soft mask.</summary>
    /// <param name="masked">Whether the image has a soft mask.</param>
    /// <returns>The file.</returns>
    private static byte[] Image(bool masked)
    {
        var pdf = Page(string.Empty, "q 10 0 0 10 0 0 cm /Im Do Q");
        var mask = masked ? string.Create(CultureInfo.InvariantCulture, $" /SMask {pdf.AddStream(GrayPixel, [byte.MaxValue])} 0 R") : string.Empty;
        var image = pdf.AddStream($"{GrayPixel}{mask}", [0]);
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/XObject << /Im {image} 0 R >>");
        return pdf.ToBytes();
    }

    /// <summary>Builds a page that draws a form, with a transparency group on the page or on the form.</summary>
    /// <param name="onPage">Whether the page has a transparency group.</param>
    /// <param name="onForm">Whether the form has a transparency group.</param>
    /// <returns>The file.</returns>
    private static byte[] Grouped(bool onPage, bool onForm)
    {
        const string Group = " /Group << /S /Transparency >>";
        var pdf = Page(string.Empty, "/Fm Do");
        var form = pdf.AddStream($"/Type /XObject /Subtype /Form /BBox [0 0 10 10]{(onForm ? Group : string.Empty)}", Fill);
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/XObject << /Fm {form} 0 R >>");
        pdf.PageEntries = onPage ? Group : string.Empty;
        return pdf.ToBytes();
    }

    /// <summary>Builds a page with one annotation, optionally with an appearance dictionary.</summary>
    /// <param name="entries">The annotation's entries besides /Type and /AP.</param>
    /// <param name="appearance">The appearance dictionary's entries, with <c>{0}</c> standing for the appearance stream's number, or null for none.</param>
    /// <returns>The file.</returns>
    private static byte[] Annotated(string entries, string? appearance)
    {
        var pdf = Page(string.Empty, Fill);
        var ap = string.Empty;
        if (appearance is not null)
        {
            var stream = pdf.AddStream("/Type /XObject /Subtype /Form /BBox [0 0 10 10]", Fill);
            ap = $" /AP << {string.Format(CultureInfo.InvariantCulture, appearance, stream)} >>";
        }

        var annotation = pdf.AddObject($"<< /Type /Annot {entries}{ap} >>");
        pdf.PageEntries = string.Create(CultureInfo.InvariantCulture, $"/Annots [{annotation} 0 R]");
        if (entries.Contains("/Widget", StringComparison.Ordinal))
        {
            pdf.CatalogEntries += string.Create(CultureInfo.InvariantCulture, $" /AcroForm << /Fields [{annotation} 0 R] >>");
        }

        return pdf.ToBytes();
    }
}
