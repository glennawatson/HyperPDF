// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.TestAssets;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>Hand-built untagged pages for the layout inference tests: tables, lists, running text and figures. The content streams are written out as page operators.</summary>
internal static class InferredSamples
{
    /// <summary>The page numbers printed on the pages of the running header sample.</summary>
    private static readonly string[] PageLabels = ["1", "2", "3"];

    /// <summary>Gets the resource entry of the sample image.</summary>
    private static string Image => "/XObject << /Im0 5 0 R >>";

    /// <summary>Builds a page with a ruled table of four rows and three columns: a header row, two data rows and a total row whose first cell spans two columns.</summary>
    /// <returns>The PDF.</returns>
    internal static byte[] RuledTable() => Build(
        [
            """
            0.5 w
            72 700 m 372 700 l S
            72 680 m 372 680 l S
            72 660 m 372 660 l S
            72 640 m 372 640 l S
            72 620 m 372 620 l S
            72 700 m 72 620 l S
            372 700 m 372 620 l S
            172 700 m 172 640 l S
            272 700 m 272 620 l S
            BT /F1 10 Tf 78 686 Td (Item) Tj ET
            BT /F1 10 Tf 178 686 Td (Qty) Tj ET
            BT /F1 10 Tf 278 686 Td (Price) Tj ET
            BT /F1 10 Tf 78 666 Td (Apple) Tj ET
            BT /F1 10 Tf 178 666 Td (3) Tj ET
            BT /F1 10 Tf 278 666 Td (1.20) Tj ET
            BT /F1 10 Tf 78 646 Td (Pear) Tj ET
            BT /F1 10 Tf 178 646 Td (5) Tj ET
            BT /F1 10 Tf 278 646 Td (0.80) Tj ET
            BT /F1 10 Tf 78 626 Td (Total) Tj ET
            BT /F1 10 Tf 278 626 Td (2.00) Tj ET
            """,
        ],
        string.Empty);

    /// <summary>Builds a page with a table that has no ruling lines: a header row and three data rows in three columns, far enough apart to be cells.</summary>
    /// <returns>The PDF.</returns>
    internal static byte[] AlignedTable() => Build(
        [
            """
            BT /F1 18 Tf 72 700 Td (Sales) Tj ET
            BT /F1 10 Tf 72 640 Td (Name) Tj ET
            BT /F1 10 Tf 222 640 Td (Region) Tj ET
            BT /F1 10 Tf 372 640 Td (Total) Tj ET
            BT /F1 10 Tf 72 626 Td (Ann) Tj ET
            BT /F1 10 Tf 222 626 Td (North) Tj ET
            BT /F1 10 Tf 372 626 Td (12) Tj ET
            BT /F1 10 Tf 72 612 Td (Bob) Tj ET
            BT /F1 10 Tf 222 612 Td (South) Tj ET
            BT /F1 10 Tf 372 612 Td (7) Tj ET
            BT /F1 10 Tf 72 598 Td (Cat) Tj ET
            BT /F1 10 Tf 222 598 Td (East) Tj ET
            BT /F1 10 Tf 372 598 Td (30) Tj ET
            """,
        ],
        string.Empty);

    /// <summary>Builds a page with a numbered list of three items, the second wrapping onto a hanging line, between a paragraph and a heading.</summary>
    /// <returns>The PDF.</returns>
    internal static byte[] NumberedList() => Build(
        [
            """
            BT /F1 10 Tf 72 700 Td (Steps to follow are listed here.) Tj ET
            BT /F1 10 Tf 72 650 Td (1. Open the box) Tj ET
            BT /F1 10 Tf 72 636 Td (2. Read the card that was inside) Tj ET
            BT /F1 10 Tf 87 622 Td (the box and keep it) Tj ET
            BT /F1 10 Tf 72 608 Td (3. Close the lid) Tj ET
            BT /F1 18 Tf 72 560 Td (Next) Tj ET
            """,
        ],
        string.Empty);

    /// <summary>Builds a page with a list whose items start with a dash.</summary>
    /// <returns>The PDF.</returns>
    internal static byte[] DashList() => Build(
        [
            """
            BT /F1 10 Tf 72 650 Td (- red apples) Tj ET
            BT /F1 10 Tf 72 636 Td (- green pears) Tj ET
            BT /F1 10 Tf 72 622 Td (- yellow plums) Tj ET
            """,
        ],
        string.Empty);

    /// <summary>Builds a document of three pages, each with a running header, a running footer and a page number around different body text.</summary>
    /// <returns>The PDF.</returns>
    internal static byte[] RunningHeadersAndFooters()
    {
        var pages = new List<string>();
        foreach (var label in PageLabels)
        {
            pages.Add(Format($"""
                BT /F1 10 Tf 72 760 Td (Annual Report) Tj ET
                BT /F1 10 Tf 72 600 Td (Body text of page {label} is unique to it.) Tj ET
                BT /F1 10 Tf 72 40 Td (Confidential) Tj ET
                BT /F1 10 Tf 290 28 Td (Page {label}) Tj ET
                """));
        }

        return Build([.. pages], string.Empty);
    }

    /// <summary>Builds a page with a figure, a caption under it, and a paragraph.</summary>
    /// <returns>The PDF.</returns>
    internal static byte[] FigureWithCaption() => Build(
        [
            """
            BT /F1 10 Tf 72 740 Td (A figure follows this introduction.) Tj ET
            q 200 0 0 100 72 600 cm /Im0 Do Q
            BT /F1 10 Tf 72 580 Td (Figure 1. A grey square) Tj ET
            BT /F1 10 Tf 72 500 Td (The text after the figure goes on here.) Tj ET
            """,
        ],
        Image);

    /// <summary>Builds a page with a wide column of text and a narrow sidebar to its right, level with the text.</summary>
    /// <returns>The PDF.</returns>
    internal static byte[] Sidebar() => Build(
        [
            """
            BT /F1 10 Tf 72 700 Td (Main column line one has long running text) Tj ET
            BT /F1 10 Tf 72 686 Td (Main column line two has long running text) Tj ET
            BT /F1 10 Tf 72 672 Td (Main column line three has long running text) Tj ET
            BT /F1 10 Tf 72 658 Td (Main column line four has long running text) Tj ET
            BT /F1 10 Tf 470 700 Td (Aside note) Tj ET
            BT /F1 10 Tf 470 686 Td (for readers) Tj ET
            """,
        ],
        string.Empty);

    /// <summary>Builds a page with a centred title, left aligned lines and right aligned lines.</summary>
    /// <returns>The PDF.</returns>
    internal static byte[] Alignments() => Build(
        [
            """
            BT /F1 10 Tf 157 718 Td (Centred title) Tj ET
            BT /F1 10 Tf 72 700 Td (left aligned one) Tj ET
            BT /F1 10 Tf 72 686 Td (left aligned a considerably longer line of text) Tj ET
            BT /F1 10 Tf 72 672 Td (left two) Tj ET
            BT /F1 10 Tf 430 600 Td (right one) Tj ET
            BT /F1 10 Tf 385 586 Td (right a bit longer) Tj ET
            BT /F1 10 Tf 430 572 Td (right two) Tj ET
            """,
        ],
        string.Empty);

    /// <summary>Builds a page of one English paragraph and one German paragraph.</summary>
    /// <returns>The PDF.</returns>
    internal static byte[] Languages() => Build(
        [
            """
            BT /F1 10 Tf 72 700 Td (The cat is on the mat and it is not in the house.) Tj ET
            BT /F1 10 Tf 72 600 Td (Der Hund und die Katze sind nicht in dem Haus von der Frau.) Tj ET
            """,
        ],
        string.Empty);

    /// <summary>
    /// Builds an untagged page in a standard font that the optimiser can read: a ruled table, a numbered list and a figure
    /// with a caption under it.
    /// </summary>
    /// <returns>The PDF.</returns>
    internal static byte[] TableListAndFigure() => Build(
        [
            """
            0.5 w
            72 700 m 372 700 l S
            72 680 m 372 680 l S
            72 660 m 372 660 l S
            72 640 m 372 640 l S
            72 700 m 72 640 l S
            172 700 m 172 640 l S
            272 700 m 272 640 l S
            372 700 m 372 640 l S
            BT /F1 10 Tf 78 686 Td (Item) Tj ET
            BT /F1 10 Tf 178 686 Td (Qty) Tj ET
            BT /F1 10 Tf 278 686 Td (Price) Tj ET
            BT /F1 10 Tf 78 666 Td (Apple) Tj ET
            BT /F1 10 Tf 178 666 Td (3) Tj ET
            BT /F1 10 Tf 278 666 Td (1.20) Tj ET
            BT /F1 10 Tf 72 590 Td (1.) Tj ET
            BT /F1 10 Tf 90 590 Td (Open the box) Tj ET
            BT /F1 10 Tf 72 576 Td (2.) Tj ET
            BT /F1 10 Tf 90 576 Td (Read the card inside) Tj ET
            BT /F1 10 Tf 72 562 Td (3.) Tj ET
            BT /F1 10 Tf 90 562 Td (Close the lid) Tj ET
            q 200 0 0 100 72 420 cm /Im0 Do Q
            BT /F1 10 Tf 72 400 Td (Figure 1. A grey square) Tj ET
            """,
        ],
        Image,
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>");

    /// <summary>Builds a page that only draws an image.</summary>
    /// <returns>The PDF.</returns>
    internal static byte[] ImageOnlyPage() => Build(["q 400 0 0 600 72 100 cm /Im0 Do Q\n"], Image);

    /// <summary>Writes an interpolated string using the invariant culture.</summary>
    /// <param name="value">The interpolated text.</param>
    /// <returns>The formatted text.</returns>
    private static string Format(FormattableString value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Builds a file of pages that share one test font and one image.</summary>
    /// <param name="contents">The content of each page.</param>
    /// <param name="resources">Extra page resources.</param>
    /// <returns>The PDF.</returns>
    private static byte[] Build(string[] contents, string resources) => Build(contents, resources, "<< /Type /Font /Subtype /Type1 /BaseFont /TaggedTest >>");

    /// <summary>Builds a file of pages that share one font and one image.</summary>
    /// <param name="contents">The content of each page.</param>
    /// <param name="resources">Extra page resources.</param>
    /// <param name="font">The font object's body.</param>
    /// <returns>The PDF.</returns>
    private static byte[] Build(string[] contents, string resources, string font)
    {
        var kids = new StringBuilder();
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            string.Empty,
            font,
            "<< >>",
            MiniPdf.Stream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /ASCIIHexDecode", "80>"),
        };
        foreach (var content in contents)
        {
            var pageNumber = objects.Count + 1;
            _ = kids.Append(Format($"{pageNumber} 0 R "));
            objects.Add(Format($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 3 0 R >> {resources} >> /Contents {pageNumber + 1} 0 R >>"));
            objects.Add(MiniPdf.Stream(string.Empty, content));
        }

        objects[1] = Format($"<< /Type /Pages /Kids [{kids}] /Count {contents.Length} >>");
        return MiniPdf.Build([.. objects]);
    }
}
