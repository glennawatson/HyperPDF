// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets;

/// <summary>
/// A one page form for the Import Data tests. Annotation indexes: 0 "Name" (text field, value "old"), 1 "Load" (a push button
/// whose action imports form data from <see cref="NamedFile"/>), 2 "Note" (text field with no value).
/// </summary>
public static class FormImportSamples
{
    /// <summary>The index of the text field "Name".</summary>
    public static readonly int NameIndex;

    /// <summary>The index of the "Load" button.</summary>
    public static readonly int LoadIndex = 1;

    /// <summary>The index of the text field "Note".</summary>
    public static readonly int NoteIndex = 2;

    /// <summary>The file the button's action names.</summary>
    public static readonly string NamedFile = "answers.xfdf";

    /// <summary>The value "Name" holds before an import.</summary>
    public static readonly string OriginalName = "old";

    /// <summary>Creates the form.</summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] Create() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [5 0 R 6 0 R 7 0 R] /DA (/Helv 12 Tf 0 g) /DR << /Font << /Helv 4 0 R >> >> /NeedAppearances true >> >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /Helv 4 0 R >> >> /Annots [5 0 R 6 0 R 7 0 R] >>",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
        Widget($"/FT /Tx /T (Name) /V ({OriginalName})", "72 700 272 720"),
        Widget($"/FT /Btn /Ff 65536 /T (Load) /A << /S /ImportData /F ({NamedFile}) >>", "72 650 172 670"),
        Widget("/FT /Tx /T (Note)", "72 600 272 620"));

    /// <summary>Writes a widget annotation.</summary>
    /// <param name="entries">The widget's entries after the common ones.</param>
    /// <param name="rect">The four numbers of the rectangle.</param>
    /// <returns>The object body.</returns>
    private static string Widget(string entries, string rect) =>
        $"<< /Type /Annot /Subtype /Widget /Rect [{rect}] /P 3 0 R /F 4 {entries} >>";
}
