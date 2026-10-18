// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Conformance;

/// <summary>The annotation types each PDF/A level allows (ISO 19005-1 §6.5.2; ISO 19005-2 and -3 §6.3.1; ISO 19005-4:2020 §6.3.1).</summary>
internal static class PdfAConversionAnnotationTypes
{
    /// <summary>Determines whether a level allows an annotation type.</summary>
    /// <param name="level">The level.</param>
    /// <param name="subtype">The annotation's /Subtype.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal static bool IsAllowed(PdfALevel level, string subtype)
    {
        if (IsCommon(subtype))
        {
            return true;
        }

        var part = PdfALevels.Part(level);
        if (part == PdfAClaim.Part1)
        {
            return false;
        }

        if (IsLaterMarkup(subtype))
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

    /// <summary>Recognises annotation types shared by every PDF/A part.</summary>
    /// <param name="subtype">The annotation's case-sensitive subtype.</param>
    /// <returns>Whether all parts allow the type.</returns>
    private static bool IsCommon(string subtype) =>
        IsBasic(subtype) || IsMarkupOrControl(subtype);

    /// <summary>Recognises markup annotation types excluded by PDF/A-1.</summary>
    /// <param name="subtype">The annotation subtype.</param>
    /// <returns>Whether a later part adds the type.</returns>
    private static bool IsLaterMarkup(string subtype) =>
        subtype is "Polygon" or "PolyLine" or "Caret" or "Watermark" or "Redact";

    /// <summary>Recognises text, links, geometry and highlights.</summary>
    /// <param name="subtype">The annotation subtype.</param>
    /// <returns>Whether it is a basic annotation type.</returns>
    private static bool IsBasic(string subtype) =>
        subtype is "Text" or "Link" or "FreeText" or "Line" or "Square" or "Circle" or "Highlight" or "Underline";

    /// <summary>Recognises markup, form controls and production annotations.</summary>
    /// <param name="subtype">The annotation subtype.</param>
    /// <returns>Whether it is a shared markup or control type.</returns>
    private static bool IsMarkupOrControl(string subtype) =>
        subtype is "Squiggly" or "StrikeOut" or "Stamp" or "Ink" or "Popup" or "Widget" or "PrinterMark" or "TrapNet";
}
