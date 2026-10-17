// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Interchange;

/// <summary>The element and attribute names of XFDF (ISO 19444-1).</summary>
internal static class XfdfNames
{
    /// <summary>The XFDF namespace.</summary>
    internal const string Namespace = "http://ns.adobe.com/xfdf/";

    /// <summary>The root element.</summary>
    internal const string Root = "xfdf";

    /// <summary>The element naming the PDF file.</summary>
    internal const string File = "f";

    /// <summary>The element holding the file identifiers.</summary>
    internal const string Ids = "ids";

    /// <summary>The element holding the form fields.</summary>
    internal const string Fields = "fields";

    /// <summary>The element of one form field.</summary>
    internal const string Field = "field";

    /// <summary>The element holding the annotations.</summary>
    internal const string Annots = "annots";

    /// <summary>The element of a field value.</summary>
    internal const string Value = "value";

    /// <summary>The element of a rich text field value.</summary>
    internal const string ValueRichText = "value-richtext";

    /// <summary>The element of an annotation's text.</summary>
    internal const string Contents = "contents";

    /// <summary>The element of an annotation's rich text.</summary>
    internal const string ContentsRichText = "contents-richtext";

    /// <summary>The element of a default appearance string.</summary>
    internal const string DefaultAppearance = "defaultappearance";

    /// <summary>The element of a default style string.</summary>
    internal const string DefaultStyle = "defaultstyle";

    /// <summary>The element of a pop-up window.</summary>
    internal const string Popup = "popup";

    /// <summary>The element holding the strokes of an ink annotation.</summary>
    internal const string InkList = "inklist";

    /// <summary>The element of one stroke.</summary>
    internal const string Gesture = "gesture";

    /// <summary>The element holding the points of a polygon or polyline.</summary>
    internal const string Vertices = "vertices";

    /// <summary>The element holding an attached file's bytes.</summary>
    internal const string Data = "data";

    /// <summary>The XHTML namespace rich text uses.</summary>
    internal const string Xhtml = "http://www.w3.org/1999/xhtml";

    /// <summary>The length of a 3-character lookup name.</summary>
    private const int NameLength3 = 3;

    /// <summary>The length of a 4-character lookup name.</summary>
    private const int NameLength4 = 4;

    /// <summary>The length of a 5-character lookup name.</summary>
    private const int NameLength5 = 5;

    /// <summary>The length of a 6-character lookup name.</summary>
    private const int NameLength6 = 6;

    /// <summary>The length of a 7-character lookup name.</summary>
    private const int NameLength7 = 7;

    /// <summary>The length of a 8-character lookup name.</summary>
    private const int NameLength8 = 8;

    /// <summary>The length of a 9-character lookup name.</summary>
    private const int NameLength9 = 9;

    /// <summary>The length of a 14-character lookup name.</summary>
    private const int NameLength14 = 14;

    /// <summary>Finds the PDF subtype an annotation element stands for.</summary>
    /// <param name="element">The element's local name.</param>
    /// <param name="subtype">The PDF subtype.</param>
    /// <returns><see langword="true"/> when the element is an annotation this library carries.</returns>
    internal static bool TryGetSubtype(string element, out string subtype)
    {
        subtype = FindSubtype(element);
        return subtype.Length > 0;
    }

    /// <summary>Finds the element for a PDF subtype.</summary>
    /// <param name="subtype">The PDF subtype.</param>
    /// <param name="element">The element's name.</param>
    /// <returns><see langword="true"/> when the subtype has an element.</returns>
    internal static bool TryGetElement(string subtype, out string element)
    {
        element = FindElement(subtype);
        return element.Length > 0;
    }

    /// <summary>Finds an annotation element by PDF subtype.</summary>
    /// <param name="subtype">The case-sensitive PDF subtype.</param>
    /// <returns>The element name, or an empty string.</returns>
    private static string FindElement(string subtype) => subtype switch
    {
        "Text" => "text",
        "Highlight" => "highlight",
        "Underline" => "underline",
        "StrikeOut" => "strikeout",
        "Squiggly" => "squiggly",
        "Ink" => "ink",
        "Square" => "square",
        "Circle" => "circle",
        "Line" => "line",
        "Polygon" => "polygon",
        "PolyLine" => "polyline",
        "FreeText" => "freetext",
        "Stamp" => "stamp",
        "Caret" => "caret",
        "FileAttachment" => "fileattachment",
        _ => string.Empty,
    };

    /// <summary>Finds the PDF subtype of an annotation element.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static string FindSubtype(ReadOnlySpan<char> name) => name.Length switch
    {
        NameLength3 => FindSubtypeLength3(name),
        NameLength4 => FindSubtypeLength4(name),
        NameLength5 => FindSubtypeLength5(name),
        NameLength6 => FindSubtypeLength6(name),
        NameLength7 => FindSubtypeLength7(name),
        NameLength8 => FindSubtypeLength8(name),
        NameLength9 => FindSubtypeLength9(name),
        NameLength14 => FindSubtypeLength14(name),
        _ => string.Empty,
    };

    /// <summary>Finds the PDF subtype of an annotation element.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static string FindSubtypeLength3(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("ink", StringComparison.OrdinalIgnoreCase) => "Ink",
        _ => string.Empty,
    };

    /// <summary>Finds the PDF subtype of an annotation element.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static string FindSubtypeLength4(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("text", StringComparison.OrdinalIgnoreCase) => "Text",
        var key when key.Equals("line", StringComparison.OrdinalIgnoreCase) => "Line",
        _ => string.Empty,
    };

    /// <summary>Finds the PDF subtype of an annotation element.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static string FindSubtypeLength5(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("stamp", StringComparison.OrdinalIgnoreCase) => "Stamp",
        var key when key.Equals("caret", StringComparison.OrdinalIgnoreCase) => "Caret",
        _ => string.Empty,
    };

    /// <summary>Finds the PDF subtype of an annotation element.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static string FindSubtypeLength6(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("square", StringComparison.OrdinalIgnoreCase) => "Square",
        var key when key.Equals("circle", StringComparison.OrdinalIgnoreCase) => "Circle",
        _ => string.Empty,
    };

    /// <summary>Finds the PDF subtype of an annotation element.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static string FindSubtypeLength7(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("polygon", StringComparison.OrdinalIgnoreCase) => "Polygon",
        _ => string.Empty,
    };

    /// <summary>Finds the PDF subtype of an annotation element.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static string FindSubtypeLength8(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("squiggly", StringComparison.OrdinalIgnoreCase) => "Squiggly",
        var key when key.Equals("polyline", StringComparison.OrdinalIgnoreCase) => "PolyLine",
        var key when key.Equals("freetext", StringComparison.OrdinalIgnoreCase) => "FreeText",
        _ => string.Empty,
    };

    /// <summary>Finds the PDF subtype of an annotation element.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static string FindSubtypeLength9(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("highlight", StringComparison.OrdinalIgnoreCase) => "Highlight",
        var key when key.Equals("underline", StringComparison.OrdinalIgnoreCase) => "Underline",
        var key when key.Equals("strikeout", StringComparison.OrdinalIgnoreCase) => "StrikeOut",
        _ => string.Empty,
    };

    /// <summary>Finds the PDF subtype of an annotation element.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static string FindSubtypeLength14(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("fileattachment", StringComparison.OrdinalIgnoreCase) => "FileAttachment",
        _ => string.Empty,
    };
}
