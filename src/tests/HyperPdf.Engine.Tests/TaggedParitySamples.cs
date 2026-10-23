// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Tagged PDFs drawn with Helvetica that both engines read, for the structure parity tests.</summary>
internal static class TaggedParitySamples
{
    /// <summary>The first structure element's object number; objects 1 to 6 are the catalog, pages, page, font, content and tree root.</summary>
    private const int FirstElement = 7;

    /// <summary>The tree root's object number.</summary>
    private const int RootNumber = 6;

    /// <summary>The document element's object number.</summary>
    private const int DocumentNumber = FirstElement;

    /// <summary>The page's object number.</summary>
    private const int PageNumber = 3;

    /// <summary>The first line's baseline.</summary>
    private const int Top = 720;

    /// <summary>The distance between lines.</summary>
    private const int Leading = 18;

    /// <summary>
    /// Builds a page with a heading, a table, a list, a figure, replacement text, a note, an unknown block type, an
    /// unknown grouping type, and an artifact footer, drawn in an order different from the tags.
    /// </summary>
    /// <returns>The PDF.</returns>
    internal static byte[] Mixed()
    {
        string[] texts = ["Results", "Name", "Score", "Ann", "9", "1.", "First item", string.Empty, "abc", "See note", "Custom text", "Inside wrapper"];
        var content = new StringBuilder("/Artifact BMC BT /F1 8 Tf 72 40 Td (Page 1) Tj ET EMC\n");
        for (var i = texts.Length - 1; i >= 0; i--)
        {
            _ = content.Append(texts[i].Length == 0
                ? Format($"/Figure <</MCID {i}>> BDC 0 0 1 rg 300 300 80 40 re f EMC\n")
                : Format($"/Span <</MCID {i}>> BDC BT /F1 12 Tf 72 {Top - (i * Leading)} Td ({texts[i]}) Tj ET EMC\n"));
        }

        var elements = new List<string>();
        var document = Element(elements, "Document", RootNumber, string.Empty);
        var heading = Element(elements, "H2", document, "/K 0");
        var table = Element(elements, "Table", document, string.Empty);
        var head = Element(elements, "TR", table, string.Empty);
        var name = Element(elements, "TH", head, "/K 1");
        var score = Element(elements, "TH", head, "/K 2");
        var row = Element(elements, "TR", table, string.Empty);
        var ann = Element(elements, "TD", row, "/K 3");
        var nine = Element(elements, "TD", row, "/K 4");
        var list = Element(elements, "L", document, string.Empty);
        var item = Element(elements, "LI", list, string.Empty);
        var label = Element(elements, "Lbl", item, "/K 5");
        var body = Element(elements, "LBody", item, "/K 6");
        var figure = Element(elements, "Figure", document, "/K 7 /Alt (Chart)");
        var replaced = Element(elements, "P", document, "/K 8 /ActualText (Alphabet)");
        var note = Element(elements, "Note", document, "/K 9");
        var custom = Element(elements, "Custom", document, "/K 10");
        var wrapper = Element(elements, "Wrapper", document, string.Empty);
        var inner = Element(elements, "P", wrapper, "/K 11");
        SetKids(elements, document, heading, table, list, figure, replaced, note, custom, wrapper);
        SetKids(elements, table, head, row);
        SetKids(elements, head, name, score);
        SetKids(elements, row, ann, nine);
        SetKids(elements, list, item);
        SetKids(elements, item, label, body);
        SetKids(elements, wrapper, inner);
        var parents = Format($"<< /Nums [0 [{heading} 0 R {name} 0 R {score} 0 R {ann} 0 R {nine} 0 R {label} 0 R {body} 0 R {figure} 0 R {replaced} 0 R {note} 0 R {custom} 0 R {inner} 0 R]] >>");
        var parentTree = FirstElement + elements.Count;
        List<string> objects =
        [
            Format($"<< /Type /Catalog /Pages 2 0 R /MarkInfo << /Marked true >> /StructTreeRoot {RootNumber} 0 R >>"),
            Format($"<< /Type /Pages /Kids [{PageNumber} 0 R] /Count 1 >>"),
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R /StructParents 0 >>",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            MiniPdf.Stream(string.Empty, content.ToString()),
            Format($"<< /Type /StructTreeRoot /K [{document} 0 R] /ParentTree {parentTree} 0 R >>"),
        ];
        objects.AddRange(elements);
        objects.Add(parents);
        return MiniPdf.Build([.. objects]);
    }

    /// <summary>Adds an element with no kids yet.</summary>
    /// <param name="elements">The element bodies, numbered from <see cref="FirstElement"/>.</param>
    /// <param name="type">The type.</param>
    /// <param name="parent">The parent's object number.</param>
    /// <param name="entries">Other entries.</param>
    /// <returns>The object number.</returns>
    private static int Element(List<string> elements, string type, int parent, string entries)
    {
        elements.Add(Format($"<< /Type /StructElem /S /{type} /P {parent} 0 R /Pg {PageNumber} 0 R {entries} >>"));
        return FirstElement + elements.Count - 1;
    }

    /// <summary>Gives an element its child elements.</summary>
    /// <param name="elements">The element bodies.</param>
    /// <param name="element">The element's object number.</param>
    /// <param name="kids">The children's object numbers.</param>
    private static void SetKids(List<string> elements, int element, params ReadOnlySpan<int> kids)
    {
        var list = new StringBuilder();
        foreach (var kid in kids)
        {
            _ = list.Append(Format($"{kid} 0 R "));
        }

        var index = element - FirstElement;
        elements[index] = elements[index].Replace(" >>", Format($" /K [{list}] >>"), StringComparison.Ordinal);
    }

    /// <summary>Formats text with the invariant culture.</summary>
    /// <param name="text">The interpolated text.</param>
    /// <returns>The text.</returns>
    private static string Format(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);
}
