// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using HyperPdf.TestAssets;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>
/// Builds a one-page PDF for the structure tests. Objects 1 to 5 are the catalog, the page tree, the page, the test
/// font <c>/F1</c> and the page content; structure objects are added after them.
/// </summary>
[DebuggerDisplay("TaggedPdfBuilder: {_objects.Count} objects")]
internal sealed class TaggedPdfBuilder
{
    /// <summary>The page's object number.</summary>
    internal const int PageNumber = 3;

    /// <summary>The catalog's object number.</summary>
    private const int CatalogNumber = 1;

    /// <summary>The page tree's object number.</summary>
    private const int PagesNumber = 2;

    /// <summary>The font's object number.</summary>
    private const int FontNumber = 4;

    /// <summary>The content stream's object number.</summary>
    private const int ContentNumber = 5;

    /// <summary>The object bodies; object n is at n - 1.</summary>
    private readonly List<string> _objects = [string.Empty, string.Empty, string.Empty, string.Empty, string.Empty];

    /// <summary>Gets or sets extra catalog entries, such as <c>/MarkInfo</c> and <c>/StructTreeRoot</c>.</summary>
    internal string Catalog { get; set; } = "/MarkInfo << /Marked true >>";

    /// <summary>Gets or sets extra page entries, such as <c>/StructParents</c> and <c>/Annots</c>.</summary>
    internal string Page { get; set; } = "/StructParents 0";

    /// <summary>Gets or sets extra resource entries, such as <c>/XObject</c> and <c>/Properties</c>.</summary>
    internal string Resources { get; set; } = string.Empty;

    /// <summary>Gets or sets the page content.</summary>
    internal string Content { get; set; } = string.Empty;

    /// <summary>Writes a structure element's body.</summary>
    /// <param name="type">The <c>/S</c> type.</param>
    /// <param name="parent">The parent's object number.</param>
    /// <param name="entries">The other entries.</param>
    /// <returns>The body.</returns>
    internal static string ElementBody(string type, int parent, string entries) =>
        string.Create(CultureInfo.InvariantCulture, $"<< /Type /StructElem /S /{type} /P {parent} 0 R /Pg {PageNumber} 0 R {entries} >>");

    /// <summary>Writes a line of text in marked content.</summary>
    /// <param name="tag">The marked content tag.</param>
    /// <param name="mcid">The marked content id.</param>
    /// <param name="x">The left edge.</param>
    /// <param name="y">The baseline.</param>
    /// <param name="text">The text, with no characters that need escaping.</param>
    /// <returns>The content.</returns>
    internal static string Text(string tag, int mcid, int x, int y, string text) =>
        string.Create(CultureInfo.InvariantCulture, $"/{tag} <</MCID {mcid}>> BDC BT /F1 12 Tf {x} {y} Td ({text}) Tj ET EMC\n");

    /// <summary>Writes a form XObject that draws with the test font.</summary>
    /// <param name="content">The form's content.</param>
    /// <returns>The stream object's body.</returns>
    internal static string FormStream(string content) =>
        MiniPdf.Stream(string.Create(CultureInfo.InvariantCulture, $"/Type /XObject /Subtype /Form /BBox [0 0 612 792] /Resources << /Font << /F1 {FontNumber} 0 R >> >>"), content);

    /// <summary>Adds an object.</summary>
    /// <param name="body">The object body.</param>
    /// <returns>The object number.</returns>
    internal int Add(string body)
    {
        _objects.Add(body);
        return _objects.Count;
    }

    /// <summary>Reserves an object number, to be set later.</summary>
    /// <returns>The object number.</returns>
    internal int Reserve() => Add(string.Empty);

    /// <summary>Sets an object's body.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="body">The body.</param>
    internal void Set(int number, string body) => _objects[number - 1] = body;

    /// <summary>Adds a structure element.</summary>
    /// <param name="type">The <c>/S</c> type.</param>
    /// <param name="parent">The parent's object number.</param>
    /// <param name="entries">The other entries, such as <c>/K 0</c>.</param>
    /// <returns>The object number.</returns>
    internal int Element(string type, int parent, string entries) => Add(ElementBody(type, parent, entries));

    /// <summary>Sets a reserved object to a structure element.</summary>
    /// <param name="number">The reserved object number.</param>
    /// <param name="type">The <c>/S</c> type.</param>
    /// <param name="parent">The parent's object number.</param>
    /// <param name="entries">The other entries, such as <c>/K 0</c>.</param>
    internal void SetElement(int number, string type, int parent, string entries) => Set(number, ElementBody(type, parent, entries));

    /// <summary>Builds the file.</summary>
    /// <returns>The PDF bytes.</returns>
    internal byte[] Build()
    {
        Set(CatalogNumber, string.Create(CultureInfo.InvariantCulture, $"<< /Type /Catalog /Pages {PagesNumber} 0 R {Catalog} >>"));
        Set(PagesNumber, string.Create(CultureInfo.InvariantCulture, $"<< /Type /Pages /Kids [{PageNumber} 0 R] /Count 1 >>"));
        Set(PageNumber, string.Create(
            CultureInfo.InvariantCulture,
            $"<< /Type /Page /Parent {PagesNumber} 0 R /MediaBox [0 0 612 792] /Resources << /Font << /F1 {FontNumber} 0 R >> {Resources} >> /Contents {ContentNumber} 0 R {Page} >>"));
        Set(FontNumber, "<< /Type /Font /Subtype /Type1 /BaseFont /TaggedTest >>");
        Set(ContentNumber, MiniPdf.Stream(string.Empty, Content));
        return MiniPdf.Build([.. _objects]);
    }
}
