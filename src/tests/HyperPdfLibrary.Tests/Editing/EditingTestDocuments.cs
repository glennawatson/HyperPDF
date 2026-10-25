// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Builds and inspects documents for the editing tests.</summary>
internal static class EditingTestDocuments
{
    /// <summary>The pages in the structured document.</summary>
    internal const int PageCount = 6;

    /// <summary>The number of the first page object.</summary>
    internal const int FirstPageNumber = 11;

    /// <summary>The number of the first intermediate page tree node.</summary>
    internal const int FirstNodeNumber = 8;

    /// <summary>The number of the second intermediate page tree node.</summary>
    internal const int SecondNodeNumber = 9;

    /// <summary>The number of the first structure element.</summary>
    internal const int FirstElementNumber = 25;

    /// <summary>The number of the second structure element.</summary>
    internal const int SecondElementNumber = 26;

    /// <summary>The number of the first outline entry.</summary>
    internal const int FirstOutlineNumber = 23;

    /// <summary>The rotation inherited by the first three pages.</summary>
    internal const int InheritedRotation = 90;

    /// <summary>The width of the first three pages' inherited media box.</summary>
    internal const int TallWidth = 612;

    /// <summary>The width of the last three pages' inherited media box.</summary>
    internal const int WideWidth = 400;

    /// <summary>The pages under each intermediate node.</summary>
    private const int PagesPerNode = 3;

    /// <summary>The number of the flat document's first page.</summary>
    private const int FirstFlatPageNumber = 4;

    /// <summary>The objects each page of the flat document takes: the page and its content.</summary>
    private const int ObjectsPerFlatPage = 2;

    /// <summary>The number of the first content stream.</summary>
    private const int FirstContentNumber = 17;

    /// <summary>Gets the producer in the XMP packet, written as an attribute.</summary>
    internal static string XmpProducer => "Old Producer";

    /// <summary>Gets the title in the XMP packet, written as an element.</summary>
    internal static string XmpTitle => "Old Title";

    /// <summary>Gets an XMP property that no edit touches.</summary>
    internal static string XmpFormat => "<dc:format>application/pdf</dc:format>";

    /// <summary>Gets the XMP packet of the structured document.</summary>
    private static string Xmp =>
        """
        <?xpacket begin="" id="W5M0MpCehiHzreSzNTczkc9d"?>
        <x:xmpmeta xmlns:x="adobe:ns:meta/"><rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
        <rdf:Description rdf:about="" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:pdf="http://ns.adobe.com/pdf/1.3/" pdf:Producer="Old Producer">
        <dc:title><rdf:Alt><rdf:li xml:lang="x-default">Old Title</rdf:li></rdf:Alt></dc:title>
        <dc:format>application/pdf</dc:format>
        </rdf:Description></rdf:RDF></x:xmpmeta>
        <?xpacket end="w"?>
        """;

    /// <summary>
    /// Creates a six page document with two intermediate page tree nodes carrying inherited boxes and rotation, root
    /// resources, page labels (i, ii, A-1 to A-4), an outline and named destinations to pages 1 to 3, a structure tree
    /// whose elements name pages 1 and 2, and an XMP packet. Each page draws "Page n".
    /// </summary>
    /// <returns>The file bytes.</returns>
    internal static byte[] CreateStructured()
    {
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R /Outlines 3 0 R /PageLabels << /Nums [0 << /S /r >> 2 << /S /D /P (A-) >>] >> /Names << /Dests 4 0 R >> "
                + "/Dests 5 0 R /StructTreeRoot 6 0 R /MarkInfo << /Marked true >> /Metadata 7 0 R >>",
            "<< /Type /Pages /Kids [8 0 R 9 0 R] /Count 6 /Resources << /Font << /F1 10 0 R >> >> >>",
            "<< /Type /Outlines /First 23 0 R /Last 24 0 R /Count 2 >>",
            "<< /Names [(d1) [11 0 R /Fit] (d2) [12 0 R /Fit]] >>",
            "<< /n1 [11 0 R /Fit] /n3 [13 0 R /Fit] >>",
            "<< /Type /StructTreeRoot /K [25 0 R 26 0 R] /ParentTree << /Nums [0 [25 0 R] 1 [26 0 R]] >> /ParentTreeNextKey 2 >>",
            MiniPdf.Stream("/Type /Metadata /Subtype /XML", Xmp),
            "<< /Type /Pages /Parent 2 0 R /Kids [11 0 R 12 0 R 13 0 R] /Count 3 /MediaBox [0 0 612 792] /Rotate 90 >>",
            "<< /Type /Pages /Parent 2 0 R /Kids [14 0 R 15 0 R 16 0 R] /Count 3 /MediaBox [0 0 400 300] >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
        };

        for (var i = 0; i < PageCount; i++)
        {
            var parent = i < PagesPerNode ? FirstNodeNumber : SecondNodeNumber;
            objects.Add(string.Create(CultureInfo.InvariantCulture, $"<< /Type /Page /Parent {parent} 0 R /Contents {FirstContentNumber + i} 0 R /StructParents {i} >>"));
        }

        for (var i = 0; i < PageCount; i++)
        {
            objects.Add(MiniPdf.Stream(string.Empty, string.Create(CultureInfo.InvariantCulture, $"BT /F1 24 Tf 72 100 Td (Page {i + 1}) Tj ET")));
        }

        objects.Add("<< /Title (One) /Parent 3 0 R /Next 24 0 R /Dest [11 0 R /Fit] >>");
        objects.Add("<< /Title (Two) /Parent 3 0 R /Prev 23 0 R /Dest [12 0 R /Fit] >>");
        objects.Add("<< /Type /StructElem /S /P /P 6 0 R /Pg 11 0 R /K 0 >>");
        objects.Add("<< /Type /StructElem /S /P /P 6 0 R /Pg 12 0 R /K 0 >>");
        return MiniPdf.Build([.. objects]);
    }

    /// <summary>Creates a document whose pages draw "Page n" and inherit nothing.</summary>
    /// <param name="pageCount">The page count.</param>
    /// <returns>The file bytes.</returns>
    internal static byte[] CreateFlat(int pageCount)
    {
        var kids = new StringBuilder();
        for (var i = 0; i < pageCount; i++)
        {
            _ = kids.Append(CultureInfo.InvariantCulture, $"{FlatPageNumber(i)} 0 R ");
        }

        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{kids.ToString().TrimEnd()}] /Count {pageCount} >>"),
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
        };

        for (var i = 0; i < pageCount; i++)
        {
            var content = FlatPageNumber(i) + 1;
            objects.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> >> /Contents {content} 0 R >>"));
            objects.Add(MiniPdf.Stream(string.Empty, string.Create(CultureInfo.InvariantCulture, $"BT /F1 24 Tf 72 100 Td (Page {i + 1}) Tj ET")));
        }

        return MiniPdf.Build([.. objects]);
    }

    /// <summary>Gets the text each page draws, in page order.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The texts, such as "Page 3".</returns>
    internal static string[] PageTexts(PdfDocument document)
    {
        var texts = new string[document.PageCount];
        for (var i = 0; i < texts.Length; i++)
        {
            texts[i] = PageText(PdfDocumentPages.GetPage(document, i));
        }

        return texts;
    }

    /// <summary>Gets the text a page draws.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The text between the first parentheses of its content, or empty.</returns>
    internal static string PageText(PdfPage page)
    {
        var content = Encoding.Latin1.GetString(page.Dictionary.GetStream(KnownName.Contents)?.DecodeToArray() ?? []);
        var start = content.IndexOf('(', StringComparison.Ordinal);
        var end = content.IndexOf(')', StringComparison.Ordinal);
        return start < 0 || end < start ? string.Empty : content[(start + 1)..end];
    }

    /// <summary>Gets the expected page texts for page numbers.</summary>
    /// <param name="pages">The one based page numbers, separated by spaces, such as "3 1 2".</param>
    /// <returns>The texts.</returns>
    internal static string[] Expected(string pages)
    {
        var numbers = pages.Split(' ');
        var texts = new string[numbers.Length];
        for (var i = 0; i < numbers.Length; i++)
        {
            texts[i] = $"Page {numbers[i]}";
        }

        return texts;
    }

    /// <summary>Gets the object number of a page of the flat document.</summary>
    /// <param name="index">The page index.</param>
    /// <returns>The object number.</returns>
    private static int FlatPageNumber(int index) => FirstFlatPageNumber + (index * ObjectsPerFlatPage);
}
