// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Rendering;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.Tests.Text;

/// <summary>Builds one-page documents whose fonts are <see cref="TextTestFont"/> and extracts their text.</summary>
internal static class TextTestDocument
{
    /// <summary>The page width in points.</summary>
    internal const int PageWidth = 300;

    /// <summary>The page height in points.</summary>
    internal const int PageHeight = 300;

    /// <summary>The base font name of the horizontal test font, /F1.</summary>
    private const string HorizontalName = "TextTestHorizontal";

    /// <summary>The base font name of the vertical test font, /F2.</summary>
    private const string VerticalName = "TextTestVertical";

    /// <summary>The base font name of the bold test font, /F3.</summary>
    private const string BoldName = "TextTestBold";

    /// <summary>Creates a page with the test fonts as /F1 (horizontal), /F2 (vertical) and /F3 (bold).</summary>
    /// <param name="content">The page content.</param>
    /// <returns>The PDF builder, for more resources or page entries.</returns>
    internal static RenderTestPdf Create(string content)
    {
        var pdf = new RenderTestPdf(PageWidth, PageHeight) { Content = content };
        var f1 = pdf.AddObject(FontDictionary(HorizontalName));
        var f2 = pdf.AddObject(FontDictionary(VerticalName));
        var f3 = pdf.AddObject(FontDictionary(BoldName));
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/Font << /F1 {f1} 0 R /F2 {f2} 0 R /F3 {f3} 0 R >>");
        return pdf;
    }

    /// <summary>Extracts the text of a page with the given content.</summary>
    /// <param name="content">The page content.</param>
    /// <returns>The text page.</returns>
    internal static PdfTextPage Extract(string content) => Extract(Create(content));

    /// <summary>Extracts the text of a test document's page.</summary>
    /// <param name="pdf">The document.</param>
    /// <returns>The text page.</returns>
    internal static PdfTextPage Extract(RenderTestPdf pdf) => PdfDocumentText.GetTextPage(Open(pdf.ToBytes()), 0);

    /// <summary>Opens a document with its own test font loader.</summary>
    /// <param name="bytes">The PDF bytes.</param>
    /// <returns>The document with its text pages warmed.</returns>
    internal static PdfDocument Open(byte[] bytes)
    {
        var document = PdfDocumentReader.OpenWith(bytes, new PdfOpenOptions { FontFactory = static dictionary => Load(dictionary) ?? PdfFontLoader.Load(dictionary) });
        for (var i = 0; i < document.PageCount; i++)
        {
            _ = PdfDocumentText.GetTextPage(document, i);
        }

        return document;
    }

    /// <summary>Gets the dictionary of a test font.</summary>
    /// <param name="name">The base font name.</param>
    /// <returns>The dictionary text.</returns>
    private static string FontDictionary(string name) => $"<< /Type /Font /Subtype /Type1 /BaseFont /{name} >>";

    /// <summary>Loads a test font by its base font name.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <returns>The font, or <see langword="null"/> for other fonts.</returns>
    private static TextTestFont? Load(PdfDictionary dictionary)
    {
        var name = dictionary.Owner!.Names.GetString(dictionary.GetName(KnownName.BaseFont));
        return name switch
        {
            HorizontalName => new(dictionary, false, false),
            VerticalName => new(dictionary, true, false),
            BoldName => new(dictionary, false, true),
            _ => null,
        };
    }
}
