// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets;

/// <summary>Writes small forms whose widgets need the form appearance features: rich text, push button icons, round bevels and legacy multi-byte fonts.</summary>
public static class FormAppearanceSamples
{
    /// <summary>The index of the rich text field in <see cref="CreateWidgetsForm"/>.</summary>
    public static readonly int RichTextIndex;

    /// <summary>The index of the push button with an icon and a caption in <see cref="CreateWidgetsForm"/>.</summary>
    public static readonly int PushButtonIndex = 1;

    /// <summary>The index of the radio button with a beveled border in <see cref="CreateWidgetsForm"/>.</summary>
    public static readonly int BeveledRadioIndex = 2;

    /// <summary>The index of the radio button with an inset border in <see cref="CreateWidgetsForm"/>.</summary>
    public static readonly int InsetRadioIndex = 3;

    /// <summary>The rich text of the first field: bold, italic, coloured, underlined and struck-through runs, and a right aligned paragraph.</summary>
    private const string RichXhtml =
        "<body xmlns=\"http://www.w3.org/1999/xhtml\" style=\"font: Helvetica 12pt\"><p>Plain <b>bold</b> <i>italic</i> <span style=\"color:#CC0000\">red</span> <u>underlined</u></p>"
        + "<p style=\"text-align:right\">Second <s>struck</s> paragraph</p></body>";

    /// <summary>Creates a 300 by 300 point page with a rich text field, a push button with an icon above its caption, and beveled and inset radio buttons.</summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateWidgetsForm() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [6 0 R 7 0 R 8 0 R 9 0 R] /DA (/Helv 12 Tf 0 g) /DR << /Font << /Helv 4 0 R >> >> >> >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << >> /Annots [6 0 R 7 0 R 8 0 R 9 0 R] >>",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
        MiniPdf.Stream("/Type /XObject /Subtype /Form /BBox [0 0 100 50]", "1 0 0 rg 0 0 100 50 re f 0 0 1 rg 0 0 20 10 re f"),
        $"<< /Type /Annot /Subtype /Widget /Rect [10 240 290 290] /P 3 0 R /F 4 /FT /Tx /T (Rich) /Ff 33558528 /V (Plain) /RV ({RichXhtml}) /MK << /BC [0 0 0] /BG [1 1 0.8] >> >>",
        "<< /Type /Annot /Subtype /Widget /Rect [10 130 290 230] /P 3 0 R /F 4 /FT /Btn /Ff 65536 /T (Go) /BS << /W 2 /S /B >> /MK << /CA (Push) /TP 2 /I 5 0 R /BC [0 0 0] /BG [0.9 0.9 0.9] >> >>",
        "<< /Type /Annot /Subtype /Widget /Rect [10 80 50 120] /P 3 0 R /F 4 /FT /Btn /Ff 49152 /T (R1) /BS << /W 2 /S /B >> /MK << /BC [0 0 1] /BG [0.8 0.8 0.8] >> >>",
        "<< /Type /Annot /Subtype /Widget /Rect [70 80 110 120] /P 3 0 R /F 4 /FT /Btn /Ff 49152 /T (R2) /BS << /W 1 /S /I >> /MK << /BC [0 0 0] /BG [1 1 1] >> >>");

    /// <summary>Creates a one page form with a single text field whose font is a Shift-JIS Type0 font (<c>90ms-RKSJ-H</c>) with no embedded program.</summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateShiftJisForm() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [5 0 R] /DA (/Helv 12 Tf 0 g) /DR << /Font << /Helv 4 0 R >> >> >> >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Resources << >> /Annots [5 0 R] >>",
        "<< /Type /Font /Subtype /Type0 /BaseFont /MS-Mincho /Encoding /90ms-RKSJ-H /DescendantFonts [<< /Type /Font /Subtype /CIDFontType2 /BaseFont /MS-Mincho "
        + "/CIDSystemInfo << /Registry (Adobe) /Ordering (Japan1) /Supplement 2 >> /DW 1000 /FontDescriptor << /Type /FontDescriptor /FontName /MS-Mincho /Flags 6 "
        + "/FontBBox [0 -137 1000 859] /ItalicAngle 0 /Ascent 859 /Descent -137 /CapHeight 709 /StemV 69 >> >>] >>",
        "<< /Type /Annot /Subtype /Widget /Rect [10 240 290 290] /P 3 0 R /F 4 /FT /Tx /T (Name) /MK << /BC [0 0 0] >> >>");
}
