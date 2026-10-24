// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets;

/// <summary>Writes small one page PDFs with a table and a numbered list, tagged and untagged, for screen reader tests.</summary>
public static class StructuredTestPdf
{
    /// <summary>The heading text.</summary>
    public static readonly string Heading = "Report";

    /// <summary>The text of the first cell of the table's data row.</summary>
    public static readonly string DataCell = "Apple";

    /// <summary>Creates a tagged page: a level 1 heading, a table with a header row and one data row, and a list of two items.</summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateTagged() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /MarkInfo << /Marked true >> /StructTreeRoot 6 0 R /Lang (en-GB) >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R /StructParents 0 >>",
        MiniPdf.Stream(
            string.Empty,
            """
            /H1 << /MCID 0 >> BDC BT /F1 20 Tf 72 700 Td (Report) Tj ET EMC
            /TH << /MCID 1 >> BDC BT /F1 12 Tf 72 650 Td (Item) Tj ET EMC
            /TH << /MCID 2 >> BDC BT /F1 12 Tf 200 650 Td (Qty) Tj ET EMC
            /TD << /MCID 3 >> BDC BT /F1 12 Tf 72 630 Td (Apple) Tj ET EMC
            /TD << /MCID 4 >> BDC BT /F1 12 Tf 200 630 Td (3) Tj ET EMC
            /Lbl << /MCID 5 >> BDC BT /F1 12 Tf 72 580 Td (1.) Tj ET EMC
            /LBody << /MCID 6 >> BDC BT /F1 12 Tf 90 580 Td (Open the box) Tj ET EMC
            /Lbl << /MCID 7 >> BDC BT /F1 12 Tf 72 560 Td (2.) Tj ET EMC
            /LBody << /MCID 8 >> BDC BT /F1 12 Tf 90 560 Td (Close the lid) Tj ET EMC
            """),
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        "<< /Type /StructTreeRoot /K 7 0 R /ParentTree << /Nums [0 [8 0 R 12 0 R 13 0 R 14 0 R 15 0 R 19 0 R 20 0 R 21 0 R 22 0 R]] >> /ParentTreeNextKey 1 >>",
        "<< /Type /StructElem /S /Document /P 6 0 R /K [8 0 R 9 0 R 16 0 R] >>",
        "<< /Type /StructElem /S /H1 /P 7 0 R /Pg 3 0 R /K 0 >>",
        "<< /Type /StructElem /S /Table /P 7 0 R /K [10 0 R 11 0 R] >>",
        "<< /Type /StructElem /S /TR /P 9 0 R /K [12 0 R 13 0 R] >>",
        "<< /Type /StructElem /S /TR /P 9 0 R /K [14 0 R 15 0 R] >>",
        "<< /Type /StructElem /S /TH /P 10 0 R /Pg 3 0 R /K 1 /A << /O /Table /Scope /Column >> >>",
        "<< /Type /StructElem /S /TH /P 10 0 R /Pg 3 0 R /K 2 /A << /O /Table /Scope /Column >> >>",
        "<< /Type /StructElem /S /TD /P 11 0 R /Pg 3 0 R /K 3 >>",
        "<< /Type /StructElem /S /TD /P 11 0 R /Pg 3 0 R /K 4 >>",
        "<< /Type /StructElem /S /L /P 7 0 R /K [17 0 R 18 0 R] >>",
        "<< /Type /StructElem /S /LI /P 16 0 R /K [19 0 R 20 0 R] >>",
        "<< /Type /StructElem /S /LI /P 16 0 R /K [21 0 R 22 0 R] >>",
        "<< /Type /StructElem /S /Lbl /P 17 0 R /Pg 3 0 R /K 5 >>",
        "<< /Type /StructElem /S /LBody /P 17 0 R /Pg 3 0 R /K 6 >>",
        "<< /Type /StructElem /S /Lbl /P 18 0 R /Pg 3 0 R /K 7 >>",
        "<< /Type /StructElem /S /LBody /P 18 0 R /Pg 3 0 R /K 8 >>");

    /// <summary>Creates an untagged page with the same heading, a table drawn with ruling lines, and a list of two items.</summary>
    /// <returns>The PDF bytes.</returns>
    public static byte[] CreateUntagged() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 5 0 R >> >> /Contents 4 0 R >>",
        MiniPdf.Stream(
            string.Empty,
            """
            BT /F1 20 Tf 72 720 Td (Report) Tj ET
            0.5 w
            72 700 m 272 700 l S
            72 680 m 272 680 l S
            72 660 m 272 660 l S
            72 700 m 72 660 l S
            172 700 m 172 660 l S
            272 700 m 272 660 l S
            BT /F1 12 Tf 78 686 Td (Item) Tj ET
            BT /F1 12 Tf 178 686 Td (Qty) Tj ET
            BT /F1 12 Tf 78 666 Td (Apple) Tj ET
            BT /F1 12 Tf 178 666 Td (3) Tj ET
            BT /F1 12 Tf 72 600 Td (1.) Tj ET
            BT /F1 12 Tf 90 600 Td (Open the box) Tj ET
            BT /F1 12 Tf 72 580 Td (2.) Tj ET
            BT /F1 12 Tf 90 580 Td (Close the lid) Tj ET
            """),
        "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
}
