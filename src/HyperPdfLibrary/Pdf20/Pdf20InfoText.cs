// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Pdf20;

/// <summary>Reads and compares the values of document information entries, and words them for the summary.</summary>
internal static class Pdf20InfoText
{
    /// <summary>The most characters of a value shown in the summary.</summary>
    private const int ShownLength = 40;

    /// <summary>Reads an entry as text: a string as it is, a name, number or boolean as written.</summary>
    /// <param name="names">The name table.</param>
    /// <param name="value">The entry's value.</param>
    /// <param name="text">Receives the text.</param>
    /// <returns><see langword="false"/> when the value is a list, dictionary or stream, which XMP cannot hold as text.</returns>
    internal static bool TryRead(PdfNameTable names, PdfValue value, out string? text)
    {
        switch (value.Kind)
        {
            case PdfKind.String:
                {
                    text = PdfText.Decode(value.AsStringBytes());
                    return true;
                }

            case PdfKind.Name:
                {
                    text = names.GetString(value.AsName());
                    return true;
                }

            case PdfKind.Integer or PdfKind.Real:
                {
                    text = value.AsNumber().ToString("R", CultureInfo.InvariantCulture);
                    return true;
                }

            case PdfKind.Boolean:
                {
                    text = value.AsBoolean() ? "true" : "false";
                    return true;
                }

            case PdfKind.Null:
                {
                    text = null;
                    return true;
                }

            default:
                {
                    text = null;
                    return false;
                }
        }
    }

    /// <summary>Reads <c>/Trapped</c> as XMP spells it.</summary>
    /// <param name="names">The name table.</param>
    /// <param name="value">The entry's value: the name True, False or Unknown, or a boolean from older files.</param>
    /// <returns>True, False or Unknown, or <see langword="null"/> when it is none of them.</returns>
    internal static string? ReadTrapped(PdfNameTable names, PdfValue value)
    {
        if (value.Kind == PdfKind.Boolean)
        {
            return value.AsBoolean() ? "True" : "False";
        }

        return value.Kind == PdfKind.Name && names.GetString(value.AsName()) is ("True" or "False" or "Unknown") and var name ? name : null;
    }

    /// <summary>Determines whether two values are the same text, ignoring white space at the ends.</summary>
    /// <param name="first">The first value.</param>
    /// <param name="second">The second value.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Same(string first, string second) => first.AsSpan().Trim().Equals(second.AsSpan().Trim(), StringComparison.Ordinal);

    /// <summary>Shortens a value for the summary.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The value in quotation marks, cut with an ellipsis when long.</returns>
    internal static string Quote(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= ShownLength ? $"\"{trimmed}\"" : $"\"{trimmed.AsSpan(0, ShownLength)}…\"";
    }

    /// <summary>Joins names as people write a list: <c>a, b and c</c>.</summary>
    /// <param name="items">The names.</param>
    /// <returns>The list.</returns>
    internal static string Join(List<string> items)
    {
        if (items.Count == 1)
        {
            return items[0];
        }

        var last = items.Count - 1;
        return $"{string.Join(", ", items.GetRange(0, last))} and {items[last]}";
    }
}
