// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets;

/// <summary>
/// A one page form for the form runtime tests. Annotation indexes: 0 Total (calculated), 1 Price, 2 Qty, 3 Clear (reset),
/// 4 Hider (hides Qty, then the named action NextPage), 5 Layer (toggles a layer), 6 Send (submit), 7 Script (JavaScript).
/// </summary>
public static class FormRuntimeSamples
{
    /// <summary>The index of the calculated field "Total".</summary>
    public static readonly int TotalIndex;

    /// <summary>The index of the field "Price", whose default value is 1.</summary>
    public static readonly int PriceIndex = 1;

    /// <summary>The index of the field "Qty", which has no default value.</summary>
    public static readonly int QtyIndex = 2;

    /// <summary>The index of the "Clear" button.</summary>
    public static readonly int ClearIndex = 3;

    /// <summary>The index of the "Hider" button.</summary>
    public static readonly int HiderIndex = 4;

    /// <summary>The index of the "Layer" button.</summary>
    public static readonly int LayerIndex = 5;

    /// <summary>The index of the "Send" button.</summary>
    public static readonly int SendIndex = 6;

    /// <summary>The index of the "Script" button.</summary>
    public static readonly int ScriptIndex = 7;

    /// <summary>The number of widgets on the form.</summary>
    public static readonly int WidgetCount = 8;

    /// <summary>Creates the form.</summary>
    /// <param name="pageEntries">Extra entries of the page dictionary, such as <c>/Tabs /C</c>.</param>
    /// <returns>The PDF bytes.</returns>
    public static byte[] Create(string pageEntries) => Create(pageEntries, string.Empty);

    /// <summary>Creates the form with extra catalog entries.</summary>
    /// <param name="pageEntries">Extra entries of the page dictionary, such as <c>/Tabs /C</c>.</param>
    /// <param name="catalogEntries">Extra entries of the catalog, such as <c>/Requirements [...]</c>.</param>
    /// <returns>The PDF bytes.</returns>
    public static byte[] Create(string pageEntries, string catalogEntries) => MiniPdf.Build(
        $"<< /Type /Catalog /Pages 2 0 R {catalogEntries} "
        + "/AcroForm << /Fields [5 0 R 6 0 R 7 0 R 8 0 R 9 0 R 10 0 R 11 0 R 12 0 R] /CO [6 0 R 5 0 R] /DA (/Helv 12 Tf 0 g) /DR << /Font << /Helv 4 0 R >> >> >> "
        + "/OCProperties << /OCGs [13 0 R] /D << /Order [13 0 R] >> >> /AA << /WC << /S /Named /N /Close >> >> >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /Helv 4 0 R >> >> /Annots [5 0 R 6 0 R 7 0 R 8 0 R 9 0 R 10 0 R 11 0 R 12 0 R] "
        + "/AA << /O << /S /Named /N /FirstPage >> >> " + pageEntries + " >>",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
        Widget("/FT /Tx /T (Total) /V (0) /DV (0) /AA << /C << /S /JavaScript /JS (AFSimple_Calculate\\(\"SUM\", new Array \\(\"Price\", \"Qty\"\\)\\);) >> >>", "300 700 400 720"),
        Widget("/FT /Tx /T (Price) /V (5) /DV (1)", "72 700 172 720"),
        Widget("/FT /Tx /T (Qty) /V (3)", "72 650 172 670"),
        Widget("/FT /Btn /Ff 65536 /T (Clear) /A << /S /ResetForm >>", "72 600 172 620"),
        Widget("/FT /Btn /Ff 65536 /T (Hider) /A << /S /Hide /T (Qty) /H true /Next << /S /Named /N /NextPage >> >>", "300 600 400 620"),
        Widget("/FT /Btn /Ff 65536 /T (Layer) /A << /S /SetOCGState /State [/Toggle 13 0 R] >>", "72 550 172 570"),
        Widget("/FT /Btn /Ff 65536 /T (Send) /A << /S /SubmitForm /F (https://example.com/form) /Flags 0 >>", "300 550 400 570"),
        Widget("/FT /Btn /Ff 65536 /T (Script) /A << /S /JavaScript /JS (app.alert\\(1\\);) >>", "72 500 172 520"),
        "<< /Type /OCG /Name (Notes) >>");

    /// <summary>Writes a widget annotation.</summary>
    /// <param name="entries">The widget's entries after the common ones.</param>
    /// <param name="rect">The four numbers of the rectangle.</param>
    /// <returns>The object body.</returns>
    private static string Widget(string entries, string rect) =>
        $"<< /Type /Annot /Subtype /Widget /Rect [{rect}] /P 3 0 R /F 4 {entries} >>";
}
