// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Builds one-widget form documents for the form appearance tests and reads back what the library drew.</summary>
internal static class FormTestPdf
{
    /// <summary>The widget's index in the page's annotations.</summary>
    internal const int Widget = 0;

    /// <summary>The default appearance every test widget has, naming the field font in the form's resources.</summary>
    internal const string DefaultAppearance = "/Helv 12 Tf 0 g";

    /// <summary>The most digits of an octal escape in a PDF string.</summary>
    private const int OctalDigits = 3;

    /// <summary>The base of an octal escape.</summary>
    private const int OctalBase = 8;

    /// <summary>Gets the Helvetica font dictionary tests use for the field font.</summary>
    internal static string Helvetica => "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>";

    /// <summary>Opens a form with one widget on a 300 by 300 point page.</summary>
    /// <param name="font">The dictionary of the font the form's <c>/DR</c> names <c>Helv</c>.</param>
    /// <param name="widgetEntries">The widget and field entries after the usual ones, such as <c>/FT /Tx /T (Name)</c>.</param>
    /// <param name="extraObjects">More objects, numbered from 7.</param>
    /// <returns>The document.</returns>
    internal static PdfDocument Open(string font, string widgetEntries, params string[] extraObjects) =>
        OpenWithForm(font, widgetEntries, string.Empty, extraObjects);

    /// <summary>Opens a form with one widget on a 300 by 300 point page.</summary>
    /// <param name="font">The dictionary of the font the form's <c>/DR</c> names <c>Helv</c>.</param>
    /// <param name="widgetEntries">The widget and field entries after the usual ones.</param>
    /// <param name="formEntries">More entries of the AcroForm dictionary, such as <c>/NeedAppearances true</c>.</param>
    /// <param name="extraObjects">More objects, numbered from 7.</param>
    /// <returns>The document.</returns>
    internal static PdfDocument OpenWithForm(string font, string widgetEntries, string formEntries, params string[] extraObjects) => PdfDocumentReader.Open(
        MiniPdf.Build(
            [
                $"<< /Type /Catalog /Pages 2 0 R /AcroForm << /Fields [5 0 R] /DA ({DefaultAppearance}) /DR << /Font << /Helv 4 0 R >> >> {formEntries} >> >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] /Annots [5 0 R] >>",
                font,
                $"<< /Type /Annot /Subtype /Widget /Rect [20 200 220 230] /P 3 0 R /F 4 /DA ({DefaultAppearance}) {widgetEntries} >>",
                "<< >>",
                .. extraObjects,
            ]),
        null);

    /// <summary>Reads the field's widget annotation.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The widget dictionary.</returns>
    internal static PdfDictionary Annotation(PdfDocument document) =>
        PdfDocumentPages.GetPage(document, 0).Dictionary.GetArray(KnownName.Annots)!.GetDictionary(Widget)!;

    /// <summary>Reads the normal appearance stream of the field's widget.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The stream.</returns>
    internal static PdfStream Stream(PdfDocument document) => Annotation(document).GetDictionary(KnownName.AP)!.GetStream(KnownName.N)!;

    /// <summary>Reads the decoded content of the field's normal appearance.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The content, one character per byte.</returns>
    internal static string Content(PdfDocument document) => Encoding.Latin1.GetString(Stream(document).DecodeToArray());

    /// <summary>Reads the bytes of the first string a <c>Tj</c> operator shows in some content.</summary>
    /// <param name="content">The content, one character per byte.</param>
    /// <returns>The string's bytes with escapes resolved.</returns>
    internal static byte[] FirstShownString(string content)
    {
        var end = content.IndexOf(") Tj", StringComparison.Ordinal);
        var start = content.LastIndexOf('(', end);
        var bytes = new List<byte>();
        var i = start + 1;
        while (i < end)
        {
            if (content[i] != '\\')
            {
                bytes.Add((byte)content[i]);
                i++;
                continue;
            }

            i++;
            if (!char.IsAsciiDigit(content[i]))
            {
                bytes.Add(Unescape(content[i]));
                i++;
                continue;
            }

            var value = 0;
            var digits = 0;
            while (digits < OctalDigits && i < end && char.IsAsciiDigit(content[i]))
            {
                value = (value * OctalBase) + (content[i] - '0');
                digits++;
                i++;
            }

            bytes.Add((byte)value);
        }

        return [.. bytes];
    }

    /// <summary>Resolves a single character escape of a PDF string.</summary>
    /// <param name="escaped">The character after the backslash.</param>
    /// <returns>The byte it stands for.</returns>
    private static byte Unescape(char escaped) => escaped switch
    {
        'n' => (byte)'\n',
        'r' => (byte)'\r',
        't' => (byte)'\t',
        'b' => (byte)'\b',
        'f' => (byte)'\f',
        _ => (byte)escaped,
    };
}
