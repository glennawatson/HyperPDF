// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;

namespace HyperPdfLibrary.Conformance;

/// <summary>The annotation types each PDF/A level allows (ISO 19005-1 §6.5.2; ISO 19005-2 and -3 §6.3.1; ISO 19005-4:2020 §6.3.1).</summary>
internal static class PdfAConversionAnnotationTypes
{
    /// <summary>The types every level allows.</summary>
    private static readonly FrozenSet<string> Common = FrozenSet.ToFrozenSet(
        [
            "Text", "Link", "FreeText", "Line", "Square", "Circle", "Highlight", "Underline", "Squiggly", "StrikeOut", "Stamp", "Ink", "Popup", "Widget",
            "PrinterMark", "TrapNet",
        ],
        StringComparer.Ordinal);

    /// <summary>The types parts 2 to 4 add.</summary>
    private static readonly FrozenSet<string> Later = FrozenSet.ToFrozenSet(["Polygon", "PolyLine", "Caret", "Watermark", "Redact"], StringComparer.Ordinal);

    /// <summary>Determines whether a level allows an annotation type.</summary>
    /// <param name="level">The level.</param>
    /// <param name="subtype">The annotation's /Subtype.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal static bool IsAllowed(PdfALevel level, string subtype)
    {
        if (Common.Contains(subtype))
        {
            return true;
        }

        var part = PdfALevels.Part(level);
        if (part == PdfAClaim.Part1)
        {
            return false;
        }

        if (Later.Contains(subtype))
        {
            return true;
        }

        return subtype switch
        {
            "FileAttachment" => part is PdfAClaim.Part2 or PdfAClaim.Part3 || level == PdfALevel.PdfA4F,
            "Projection" => part == PdfAClaim.Part4,
            "3D" or "RichMedia" => level == PdfALevel.PdfA4E,
            _ => false,
        };
    }
}
