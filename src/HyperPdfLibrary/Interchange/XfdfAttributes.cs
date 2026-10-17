// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Interchange;

/// <summary>Reads the attributes of an XFDF annotation element into a <see cref="PdfInterchangeAnnotation"/>.</summary>
internal static class XfdfAttributes
{
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

    /// <summary>The length of a 9-character lookup name.</summary>
    private const int NameLength9 = 9;

    /// <summary>The length of a 10-character lookup name.</summary>
    private const int NameLength10 = 10;

    /// <summary>The length of a 12-character lookup name.</summary>
    private const int NameLength12 = 12;

    /// <summary>The length of a 13-character lookup name.</summary>
    private const int NameLength13 = 13;

    /// <summary>The length of a 14-character lookup name.</summary>
    private const int NameLength14 = 14;

    /// <summary>The numbers in a point.</summary>
    private const int PointNumbers = 2;

    /// <summary>The numbers in a line's end points.</summary>
    private const int LineNumbers = 4;

    /// <summary>Applies an attribute.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="name">The attribute's local name.</param>
    /// <param name="value">The attribute's value.</param>
    /// <returns><see langword="true"/> when the attribute is one this library reads.</returns>
    internal static bool Apply(PdfInterchangeAnnotation annotation, string name, string value)
    {
        if (FindSetter(name) is not { } setter)
        {
            return false;
        }

        setter(annotation, value);
        return true;
    }

    /// <summary>Reads a whole number.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The number, or <see langword="null"/> when unreadable.</returns>
    internal static int? ParseInt(string text) =>
        int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : null;

    /// <summary>Reads a number.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The number, or <see langword="null"/> when unreadable.</returns>
    internal static float? ParseFloat(string text) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : null;

    /// <summary>Sets one end of a line from <c>x,y</c>.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="text">The point.</param>
    /// <param name="offset">0 for the start, 2 for the end.</param>
    private static void SetLineEnd(PdfInterchangeAnnotation annotation, string text, int offset)
    {
        var point = InterchangeValues.ParseList(text);
        if (point.Length < PointNumbers)
        {
            return;
        }

        var line = new float[LineNumbers];
        annotation.Line.Span.CopyTo(line);
        line[offset] = point[0];
        line[offset + 1] = point[1];
        annotation.Line = line;
    }

    /// <summary>Finds the setter for an annotation attribute.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static Action<PdfInterchangeAnnotation, string>? FindSetter(ReadOnlySpan<char> name) => name.Length switch
    {
        NameLength3 => FindSetterLength3(name),
        NameLength4 => FindSetterLength4(name),
        NameLength5 => FindSetterLength5(name),
        NameLength6 => FindSetterLength6(name),
        NameLength7 => FindSetterLength7(name),
        NameLength9 => FindSetterLength9(name),
        NameLength10 => FindSetterLength10(name),
        NameLength12 => FindSetterLength12(name),
        NameLength13 => FindSetterLength13(name),
        NameLength14 => FindSetterLength14(name),
        _ => null,
    };

    /// <summary>Finds the setter for an annotation attribute.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static Action<PdfInterchangeAnnotation, string>? FindSetterLength3(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("end", StringComparison.OrdinalIgnoreCase) => static (a, v) => SetLineEnd(a, v, PointNumbers),
        _ => null,
    };

    /// <summary>Finds the setter for an annotation attribute.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static Action<PdfInterchangeAnnotation, string>? FindSetterLength4(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("page", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.Page = ParseInt(v) ?? 0,
        var key when key.Equals("rect", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.Rect = InterchangeValues.TryParseRect(v, out var rectangle) ? rectangle : null,
        var key when key.Equals("name", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.Name = v,
        var key when key.Equals("date", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.Date = InterchangeValues.ParseDate(v),
        var key when key.Equals("icon", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.Icon = v,
        var key when key.Equals("open", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.IsOpen = InterchangeValues.ParseBoolean(v),
        var key when key.Equals("head", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.Head = v,
        var key when key.Equals("tail", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.Tail = v,
        _ => null,
    };

    /// <summary>Finds the setter for an annotation attribute.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static Action<PdfInterchangeAnnotation, string>? FindSetterLength5(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("color", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.Color = InterchangeValues.TryParseColor(v, out var color) ? color : null,
        var key when key.Equals("flags", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.Flags = InterchangeValues.ParseFlags(v),
        var key when key.Equals("title", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.Title = v,
        var key when key.Equals("width", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.Width = ParseFloat(v),
        var key when key.Equals("style", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.Style = InterchangeValues.ParseStyle(v),
        var key when key.Equals("state", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.State = v,
        var key when key.Equals("start", StringComparison.OrdinalIgnoreCase) => static (a, v) => SetLineEnd(a, v, 0),
        _ => null,
    };

    /// <summary>Finds the setter for an annotation attribute.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static Action<PdfInterchangeAnnotation, string>? FindSetterLength6(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("dashes", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.Dashes = InterchangeValues.ParseList(v),
        var key when key.Equals("symbol", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.Symbol = v,
        var key when key.Equals("coords", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.Coords = InterchangeValues.ParseList(v),
        var key when key.Equals("fringe", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.Fringe = InterchangeValues.ParseList(v),
        _ => null,
    };

    /// <summary>Finds the setter for an annotation attribute.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static Action<PdfInterchangeAnnotation, string>? FindSetterLength7(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("subject", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.Subject = v,
        var key when key.Equals("opacity", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.Opacity = ParseFloat(v),
        var key when key.Equals("callout", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.Callout = InterchangeValues.ParseList(v),
        _ => null,
    };

    /// <summary>Finds the setter for an annotation attribute.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static Action<PdfInterchangeAnnotation, string>? FindSetterLength9(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("intensity", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.Intensity = ParseFloat(v),
        var key when key.Equals("inreplyto", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.InReplyTo = v,
        var key when key.Equals("replytype", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.ReplyType = v,
        _ => null,
    };

    /// <summary>Finds the setter for an annotation attribute.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static Action<PdfInterchangeAnnotation, string>? FindSetterLength10(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("statemodel", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.StateModel = v,
        _ => null,
    };

    /// <summary>Finds the setter for an annotation attribute.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static Action<PdfInterchangeAnnotation, string>? FindSetterLength12(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("creationdate", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.CreationDate = InterchangeValues.ParseDate(v),
        _ => null,
    };

    /// <summary>Finds the setter for an annotation attribute.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static Action<PdfInterchangeAnnotation, string>? FindSetterLength13(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("justification", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.Justification = ParseInt(v),
        _ => null,
    };

    /// <summary>Finds the setter for an annotation attribute.</summary>
    /// <param name="name">The local name, compared without regard to case.</param>
    /// <returns>The matched value or the fallback.</returns>
    private static Action<PdfInterchangeAnnotation, string>? FindSetterLength14(ReadOnlySpan<char> name) => name switch
    {
        var key when key.Equals("interior-color", StringComparison.OrdinalIgnoreCase) => static (a, v) => a.InteriorColor = InterchangeValues.TryParseColor(v, out var color) ? color : null,
        _ => null,
    };
}
