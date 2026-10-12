// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Navigation;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Builds and reads the documents for the tests of structures that page copies keep.</summary>
internal static class CarryTestDocuments
{
    /// <summary>The pages in the form book.</summary>
    internal const int BookPages = 2;

    /// <summary>The name of the form book's embedded file.</summary>
    internal const string AttachmentName = "note.txt";

    /// <summary>
    /// Creates a two page book with a form, links, an outline, named destinations, page labels, a hidden layer and an embedded
    /// file. Page 1 holds the text field "Name", the field "Address.Street", a link to page 2 and a link to the named
    /// destination "chap2". Page 2 holds "Address.City" and a link back to page 1. The outline lists "One" (page 1) and
    /// "Two" ("chap2").
    /// </summary>
    /// <returns>The file bytes.</returns>
    internal static byte[] CreateBook() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /AcroForm 3 0 R /Outlines 12 0 R /Names << /Dests 13 0 R /EmbeddedFiles << /Names [(note.txt) 19 0 R] >> >> "
            + "/PageLabels << /Nums [0 << /S /r >>] >> /OCProperties 14 0 R >>",
        "<< /Type /Pages /Kids [8 0 R 9 0 R] /Count 2 >>",
        "<< /Fields [5 0 R 6 0 R] /DR << /Font << /Helv 4 0 R >> >> /DA (/Helv 12 Tf 0 g) /Q 1 >>",
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>",
        "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Name) /V (Alice) /Rect [10 700 200 720] /F 4 /P 8 0 R >>",
        "<< /T (Address) /Kids [7 0 R 10 0 R] >>",
        "<< /Type /Annot /Subtype /Widget /FT /Tx /T (Street) /Parent 6 0 R /V (Main) /Rect [10 660 200 680] /F 4 /P 8 0 R >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 15 0 R /Annots [5 0 R 7 0 R 11 0 R 22 0 R] /AF [19 0 R] "
            + "/Resources << /Font << /F1 4 0 R >> /Properties << /MC0 18 0 R >> >> >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 21 0 R /Annots [10 0 R 23 0 R] /Resources << /Font << /F1 4 0 R >> >> >>",
        "<< /Type /Annot /Subtype /Widget /FT /Tx /T (City) /Parent 6 0 R /V (Town) /Rect [10 620 200 640] /F 4 /P 9 0 R >>",
        "<< /Type /Annot /Subtype /Link /Rect [10 600 100 620] /Border [0 0 0] /Dest [9 0 R /Fit] >>",
        "<< /Type /Outlines /First 16 0 R /Last 17 0 R /Count 2 >>",
        "<< /Names [(chap1) [8 0 R /Fit] (chap2) [9 0 R /Fit]] >>",
        "<< /OCGs [18 0 R] /D << /OFF [18 0 R] >> >>",
        MiniPdf.Stream(string.Empty, "/OC /MC0 BDC BT /F1 24 Tf 72 100 Td (Page 1) Tj ET EMC"),
        "<< /Title (One) /Parent 12 0 R /Next 17 0 R /Dest [8 0 R /Fit] >>",
        "<< /Title (Two) /Parent 12 0 R /Prev 16 0 R /Dest (chap2) >>",
        "<< /Type /OCG /Name (Notes) >>",
        "<< /Type /Filespec /F (note.txt) /UF (note.txt) /AFRelationship /Data /EF << /F 20 0 R >> >>",
        MiniPdf.Stream("/Type /EmbeddedFile", "hello"),
        MiniPdf.Stream(string.Empty, "BT /F1 24 Tf 72 100 Td (Page 2) Tj ET"),
        "<< /Type /Annot /Subtype /Link /Rect [10 580 100 600] /Border [0 0 0] /A << /S /GoTo /D (chap2) >> >>",
        "<< /Type /Annot /Subtype /Link /Rect [10 560 100 580] /Border [0 0 0] /Dest [8 0 R /Fit] >>");

    /// <summary>Gets the full field names of a page's widgets.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The names, in annotation order.</returns>
    internal static string[] WidgetNames(PdfDocument document, int pageIndex)
    {
        var widgets = new List<PdfFormWidget>();
        HyperPdfLibrary.Forms.PdfFormReading.GetWidgets(PdfDocumentForms.GetForm(document), pageIndex, widgets);
        return [.. widgets.ConvertAll(static widget => widget.Name)];
    }

    /// <summary>Gets the pages a page's links go to.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The target page of each link that goes to a page in the document.</returns>
    internal static int[] LinkPages(PdfDocument document, int pageIndex)
    {
        var pages = new List<int>();
        foreach (var link in PdfDocumentLinks.GetLinks(document, pageIndex))
        {
            if (link.Action.Value is GoToAction goTo)
            {
                pages.Add(goTo.Destination.PageIndex);
            }
        }

        return [.. pages];
    }

    /// <summary>Gets the pages the top-level outline entries go to.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The target page of each entry, or -1 for an entry that goes nowhere.</returns>
    internal static int[] OutlinePages(PdfDocument document)
    {
        var pages = new List<int>();
        foreach (var item in PdfDocumentNavigation.GetOutline(document))
        {
            pages.Add(item.Action.Value is GoToAction goTo ? goTo.Destination.PageIndex : -1);
        }

        return [.. pages];
    }
}
