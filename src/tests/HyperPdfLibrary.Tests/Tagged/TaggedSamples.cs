// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.TestAssets;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>Hand-built tagged PDFs for the structure tests.</summary>
internal static class TaggedSamples
{
    /// <summary>The running header, marked as an artifact.</summary>
    internal const string Header = "Header";

    /// <summary>The heading text.</summary>
    internal const string Title = "Title";

    /// <summary>The first paragraph, drawn below the second.</summary>
    internal const string First = "First";

    /// <summary>The second paragraph, drawn above the first.</summary>
    internal const string Second = "Second";

    /// <summary>The figure's description.</summary>
    internal const string FigureAlt = "A bar";

    /// <summary>The document language.</summary>
    internal const string DocumentLanguage = "en-AU";

    /// <summary>The first paragraph's language.</summary>
    internal const string FrenchLanguage = "fr";

    /// <summary>The link text.</summary>
    internal const string LinkText = "Visit";

    /// <summary>The link address.</summary>
    internal const string LinkUri = "https://example.com";

    /// <summary>The form field's label.</summary>
    internal const string FieldLabel = "Your name";

    /// <summary>The form field's value.</summary>
    internal const string FieldValue = "Ann";

    /// <summary>The index of the text field in the page's annotations.</summary>
    internal const int FieldIndex = 1;

    /// <summary>The length of the chain of nested elements in <see cref="Cycles"/>.</summary>
    internal const int ChainLength = 100;

    /// <summary>The left column's x position.</summary>
    private const int LeftColumn = 72;

    /// <summary>The right column's x position.</summary>
    private const int RightColumn = 320;

    /// <summary>The first body line's baseline.</summary>
    private const int BodyTop = 680;

    /// <summary>The distance between body lines.</summary>
    private const int Leading = 14;

    /// <summary>The lines in each column.</summary>
    private const int ColumnLines = 3;

    /// <summary>The third line's offset in lines, which is also the marked content id drawn there.</summary>
    private const int SecondLine = 2;

    /// <summary>The document element's type.</summary>
    private const string DocumentType = "Document";

    /// <summary>Builds a document with a heading, two paragraphs drawn out of order, a figure and an artifact header.</summary>
    /// <returns>The PDF.</returns>
    internal static byte[] Basic()
    {
        var pdf = new TaggedPdfBuilder
        {
            Content = """
                /Artifact BMC BT /F1 9 Tf 72 760 Td (Header) Tj ET EMC
                /P <</MCID 2>> BDC BT /F1 12 Tf 72 650 Td (Second) Tj ET EMC
                /Heading1 <</MCID 0>> BDC BT /F1 20 Tf 72 700 Td (Title) Tj ET EMC
                /P <</MCID 1>> BDC BT /F1 12 Tf 72 600 Td (First) Tj ET EMC
                /Figure <</MCID 3>> BDC 0 0 0 rg 72 400 100 50 re f EMC

                """,
        };
        var root = pdf.Reserve();
        var document = pdf.Reserve();
        var heading = pdf.Element("Heading1", document, "/K 0");
        var first = pdf.Element("MyPara", document, "/K 1 /Lang (fr)");
        var second = pdf.Element("P", document, "/K [<< /Type /MCR /MCID 2 >>]");
        var figure = pdf.Element("Figure", document, "/K 3 /Alt (A bar)");
        var parents = pdf.Add(Format($"<< /Nums [0 [{heading} 0 R {first} 0 R {second} 0 R {figure} 0 R]] >>"));
        pdf.Set(document, Format($"<< /Type /StructElem /S /Document /P {root} 0 R /K [{heading} 0 R {first} 0 R {second} 0 R {figure} 0 R] >>"));
        pdf.Set(root, Format($"<< /Type /StructTreeRoot /K {document} 0 R /ParentTree {parents} 0 R /RoleMap << /Heading1 /H1 /MyPara /P >> >>"));
        pdf.Catalog = Format($"/MarkInfo << /Marked true >> /Lang ({DocumentLanguage}) /StructTreeRoot {root} 0 R");
        return pdf.Build();
    }

    /// <summary>Builds a document with a table (head, body, spans and explicit headers) and a numbered list.</summary>
    /// <returns>The PDF.</returns>
    internal static byte[] TableAndList()
    {
        var pdf = new TaggedPdfBuilder { Content = TableContent() };
        var root = pdf.Reserve();
        var table = pdf.Reserve();
        var head = pdf.Reserve();
        var body = pdf.Reserve();
        var list = pdf.Reserve();
        var nameRow = Row(pdf, head, [Cell("TH", "/ID (hname) /K 0"), Cell("TH", "/K 1")]);
        var annRow = Row(pdf, body, [Cell("TH", "/K 2"), Cell("TD", "/K 3")]);
        var bobRow = Row(pdf, body, [Cell("TD", "/A << /O /Table /Headers [(hname)] >> /K 4"), Cell("TD", "/K 5")]);
        var totalRow = Row(pdf, body, [Cell("TD", "/C /wide /K 8"), Cell("TD", "/K 9")]);
        pdf.SetElement(head, "THead", table, Format($"/K [{nameRow} 0 R]"));
        pdf.SetElement(body, "TBody", table, Format($"/K [{annRow} 0 R {bobRow} 0 R {totalRow} 0 R]"));
        pdf.SetElement(table, "Table", root, Format($"/A << /O /Table /Summary (Ages) >> /K [{head} 0 R {body} 0 R]"));
        var item = pdf.Reserve();
        var label = pdf.Element("Lbl", item, "/K 6");
        var itemBody = pdf.Element("LBody", item, "/K 7");
        pdf.SetElement(item, "LI", list, Format($"/K [{label} 0 R {itemBody} 0 R]"));
        pdf.SetElement(list, "L", root, Format($"/A << /O /List /ListNumbering /Decimal >> /K [{item} 0 R]"));
        pdf.Set(root, Format($"<< /Type /StructTreeRoot /K [{table} 0 R {list} 0 R] /ClassMap << /wide << /O /Table /ColSpan 2 >> >> >>"));
        pdf.Catalog = Format($"/MarkInfo << /Marked true >> /StructTreeRoot {root} 0 R");
        return pdf.Build();
    }

    /// <summary>Builds a document whose types use PDF 2.0 namespaces, a custom namespace with a role map, and an ID tree.</summary>
    /// <returns>The PDF.</returns>
    internal static byte[] Namespaces()
    {
        var pdf = new TaggedPdfBuilder
        {
            Content = TaggedPdfBuilder.Text("Title", 0, LeftColumn, BodyTop, "Intro")
                + TaggedPdfBuilder.Text("P", 1, LeftColumn, BodyTop - Leading, "Body")
                + TaggedPdfBuilder.Text("Note", SecondLine, LeftColumn, BodyTop - (Leading * SecondLine), "Aside"),
        };
        var pdf2 = pdf.Add("<< /Type /Namespace /NS (http://iso.org/pdf2/ssn) >>");
        var custom = pdf.Add(Format($"<< /Type /Namespace /NS (http://example.com/custom) /RoleMapNS << /Chapter [/Sect {pdf2} 0 R] /Para /P >> >>"));
        var root = pdf.Reserve();
        var document = pdf.Reserve();
        var chapter = pdf.Reserve();
        var title = pdf.Element("Title", chapter, Format($"/NS {pdf2} 0 R /K 0"));
        var para = pdf.Element("Para", chapter, Format($"/NS {custom} 0 R /K 1 /ID (intro)"));
        var note = pdf.Element("Note", chapter, Format($"/NS {pdf2} 0 R /K 2"));
        pdf.SetElement(chapter, "Chapter", document, Format($"/NS {custom} 0 R /K [{title} 0 R {para} 0 R {note} 0 R]"));
        pdf.SetElement(document, DocumentType, root, Format($"/NS {pdf2} 0 R /K [{chapter} 0 R]"));
        pdf.Set(root, Format($"<< /Type /StructTreeRoot /K [{document} 0 R] /Namespaces [{pdf2} 0 R {custom} 0 R] /IDTree << /Names [(intro) {para} 0 R] >> >>"));
        pdf.Catalog = Format($"/MarkInfo << /Marked true >> /StructTreeRoot {root} 0 R");
        return pdf.Build();
    }

    /// <summary>Builds a document whose tree loops back on itself, nests very deeply and has a role map loop.</summary>
    /// <returns>The PDF.</returns>
    internal static byte[] Cycles()
    {
        var pdf = new TaggedPdfBuilder { Content = TaggedPdfBuilder.Text("P", 0, LeftColumn, BodyTop, "Loop") };
        var root = pdf.Reserve();
        var document = pdf.Reserve();
        var a = pdf.Reserve();
        var b = pdf.Reserve();
        pdf.SetElement(a, "Sect", document, Format($"/K [{b} 0 R 0]"));
        pdf.SetElement(b, "X", a, Format($"/K [{a} 0 R {b} 0 R {document} 0 R 1]"));
        var chain = new int[ChainLength];
        for (var i = 0; i < chain.Length; i++)
        {
            chain[i] = pdf.Reserve();
        }

        for (var i = 0; i < chain.Length; i++)
        {
            var kid = i + 1 < chain.Length ? Format($"/K [{chain[i + 1]} 0 R]") : "/K 2";
            pdf.SetElement(chain[i], "Div", i == 0 ? document : chain[i - 1], kid);
        }

        pdf.SetElement(document, DocumentType, root, Format($"/K [{a} 0 R {chain[0]} 0 R {document} 0 R]"));
        pdf.Set(root, Format($"<< /Type /StructTreeRoot /K [{document} 0 R {document} 0 R] /RoleMap << /X /Y /Y /X >> >>"));
        pdf.Catalog = Format($"/MarkInfo << /Marked true >> /StructTreeRoot {root} 0 R");
        return pdf.Build();
    }

    /// <summary>Builds a document with a tagged link annotation and a tagged text field, each with a /StructParent.</summary>
    /// <returns>The PDF.</returns>
    internal static byte[] LinksAndForms()
    {
        var content = TaggedPdfBuilder.Text("Link", 0, LeftColumn, BodyTop, LinkText) + TaggedPdfBuilder.Text("P", 1, LeftColumn, BodyTop - (Leading * SecondLine), "Name");
        var pdf = new TaggedPdfBuilder { Content = content };
        var link = pdf.Add(Format($"<< /Type /Annot /Subtype /Link /Rect [72 676 140 692] /A << /S /URI /URI ({LinkUri}) >> /StructParent 1 /P {TaggedPdfBuilder.PageNumber} 0 R >>"));
        var field = pdf.Add(
            Format($"<< /Type /Annot /Subtype /Widget /FT /Tx /T (name) /TU ({FieldLabel}) /V ({FieldValue}) /DA (/Helv 12 Tf 0 g) ")
            + Format($"/Rect [150 645 300 665] /StructParent 2 /P {TaggedPdfBuilder.PageNumber} 0 R >>"));
        var root = pdf.Reserve();
        var document = pdf.Reserve();
        var linkElement = pdf.Element("Link", document, Format($"/K [0 << /Type /OBJR /Obj {link} 0 R >>]"));
        var label = pdf.Element("P", document, "/K 1");
        var form = pdf.Element("Form", document, Format($"/K << /Type /OBJR /Obj {field} 0 R /Pg {TaggedPdfBuilder.PageNumber} 0 R >>"));
        var parents = pdf.Add(Format($"<< /Nums [0 [{linkElement} 0 R {label} 0 R] 1 {linkElement} 0 R 2 {form} 0 R] >>"));
        pdf.SetElement(document, DocumentType, root, Format($"/K [{linkElement} 0 R {label} 0 R {form} 0 R]"));
        pdf.Set(root, Format($"<< /Type /StructTreeRoot /K [{document} 0 R] /ParentTree {parents} 0 R /ParentTreeNextKey 3 >>"));
        pdf.Catalog = Format($"/MarkInfo << /Marked true >> /StructTreeRoot {root} 0 R /AcroForm << /Fields [{field} 0 R] /DA (/Helv 0 Tf 0 g) ")
            + "/DR << /Font << /Helv << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> >>";
        pdf.Page = Format($"/StructParents 0 /Annots [{link} 0 R {field} 0 R]");
        return pdf.Build();
    }

    /// <summary>Builds an untagged page with a large heading over two columns of three lines each.</summary>
    /// <returns>The PDF.</returns>
    internal static byte[] TwoColumns()
    {
        var content = new StringBuilder("BT /F1 28 Tf 72 740 Td (Big heading) Tj ET\n");
        for (var i = 0; i < ColumnLines; i++)
        {
            var y = BodyTop - (i * Leading);
            _ = content.Append(Format($"BT /F1 10 Tf {LeftColumn} {y} Td (left{i}) Tj ET\n"));
            _ = content.Append(Format($"BT /F1 10 Tf {RightColumn} {y} Td (right{i}) Tj ET\n"));
        }

        var pdf = new TaggedPdfBuilder { Catalog = string.Empty, Page = string.Empty, Content = content.ToString() };
        return pdf.Build();
    }

    /// <summary>Builds an untagged page that only draws an image.</summary>
    /// <returns>The PDF.</returns>
    internal static byte[] ImageOnly()
    {
        var pdf = new TaggedPdfBuilder { Catalog = string.Empty, Page = string.Empty, Content = "q 200 0 0 100 72 500 cm /Im0 Do Q" };
        var image = pdf.Add(MiniPdf.Stream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /ASCIIHexDecode", "80>"));
        pdf.Resources = Format($"/XObject << /Im0 {image} 0 R >>");
        return pdf.Build();
    }

    /// <summary>Builds a page whose marked content comes from a named property list and from inside a form XObject.</summary>
    /// <returns>The PDF.</returns>
    internal static byte[] NamedAndFormContent()
    {
        var pdf = new TaggedPdfBuilder { Content = "/P /MC0 BDC BT /F1 12 Tf 72 700 Td (Named) Tj ET EMC\n/Fm0 Do\n" };
        var form = pdf.Add(TaggedPdfBuilder.FormStream("/Span <</MCID 5>> BDC BT /F1 12 Tf 72 650 Td (Inside) Tj ET EMC"));
        var root = pdf.Reserve();
        var first = pdf.Element("P", root, "/K 4");
        var second = pdf.Element("Span", root, "/K 5");
        pdf.Set(root, Format($"<< /Type /StructTreeRoot /K [{first} 0 R {second} 0 R] >>"));
        pdf.Resources = Format($"/Properties << /MC0 << /MCID 4 >> >> /XObject << /Fm0 {form} 0 R >>");
        pdf.Catalog = Format($"/MarkInfo << /Marked true >> /StructTreeRoot {root} 0 R");
        return pdf.Build();
    }

    /// <summary>Writes the table and list content: one line per marked content id.</summary>
    /// <returns>The content.</returns>
    private static string TableContent()
    {
        string[] texts = ["Name", "Age", "Ann", "30", "Bob", "40", "1.", "Milk", "Total", "70"];
        var content = new StringBuilder();
        for (var i = 0; i < texts.Length; i++)
        {
            _ = content.Append(TaggedPdfBuilder.Text("Span", i, LeftColumn, BodyTop - (i * Leading), texts[i]));
        }

        return content.ToString();
    }

    /// <summary>Writes a cell's type and entries for <see cref="Row"/>.</summary>
    /// <param name="type">TH or TD.</param>
    /// <param name="entries">The cell's entries.</param>
    /// <returns>The pair, joined by a bar.</returns>
    private static string Cell(string type, string entries) => $"{type}|{entries}";

    /// <summary>Adds a table row and its cells.</summary>
    /// <param name="pdf">The builder.</param>
    /// <param name="parent">The row group.</param>
    /// <param name="cells">The cells from <see cref="Cell"/>.</param>
    /// <returns>The row's object number.</returns>
    private static int Row(TaggedPdfBuilder pdf, int parent, string[] cells)
    {
        var row = pdf.Reserve();
        var kids = new StringBuilder();
        foreach (var cell in cells)
        {
            var parts = cell.Split('|');
            _ = kids.Append(Format($"{pdf.Element(parts[0], row, parts[1])} 0 R "));
        }

        pdf.SetElement(row, "TR", parent, Format($"/K [{kids}]"));
        return row;
    }

    /// <summary>Formats text with the invariant culture.</summary>
    /// <param name="text">The interpolated text.</param>
    /// <returns>The text.</returns>
    private static string Format(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
