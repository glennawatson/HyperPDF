// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signing;

/// <summary>
/// How a signature looks on the page. A signature with no <see cref="Rectangle"/> area is invisible: it has a field and a
/// widget with an empty rectangle, and nothing is drawn.
/// </summary>
[DebuggerDisplay("PdfSignatureAppearance: page {PageIndex} {Rectangle}")]
public sealed record PdfSignatureAppearance
{
    /// <summary>The default font size of the text lines; the size is lowered when the lines do not fit the rectangle.</summary>
    internal const float DefaultFontSize = 10;

    /// <summary>Gets the zero-based index of the page the signature is drawn on.</summary>
    public int PageIndex { get; init; }

    /// <summary>Gets the rectangle in default user space (points, origin at the page's lower left). An empty rectangle makes the signature invisible.</summary>
    public PdfRectangle Rectangle { get; init; }

    /// <summary>Gets a value indicating whether the signer's name is drawn. The default is <see langword="true"/>.</summary>
    public bool ShowName { get; init; } = true;

    /// <summary>Gets a value indicating whether the reason is drawn when there is one. The default is <see langword="true"/>.</summary>
    public bool ShowReason { get; init; } = true;

    /// <summary>Gets a value indicating whether the location is drawn when there is one. The default is <see langword="true"/>.</summary>
    public bool ShowLocation { get; init; } = true;

    /// <summary>Gets a value indicating whether the signing date is drawn. The default is <see langword="true"/>.</summary>
    public bool ShowDate { get; init; } = true;

    /// <summary>Gets the lines to draw instead of the name, reason, location and date lines, or <see langword="null"/> to draw those.</summary>
    public IReadOnlyList<string>? Lines { get; init; }

    /// <summary>
    /// Gets an image drawn behind the text, filling the rectangle's left half when there is text and the whole rectangle
    /// when there is none; the image keeps its proportions. A transparent image is refused in a PDF/A-1 file.
    /// </summary>
    public PdfWatermarkBitmap? Image { get; init; }

    /// <summary>Gets the built-in font of the text lines. A PDF/A file needs <see cref="DocumentFont"/> instead, as the built-in fonts are not embedded.</summary>
    public AppearanceFont Font { get; init; } = AppearanceFont.Helvetica;

    /// <summary>Gets an existing simple (single-byte) font of the document to draw the text with, or an invalid id to use <see cref="Font"/>.</summary>
    public PdfObjectId DocumentFont { get; init; }

    /// <summary>Gets the font size in points, or zero to use the default and lower it until the lines fit.</summary>
    public float FontSize { get; init; }

    /// <summary>
    /// Gets the text a screen reader announces for the signature field (the field's /TU entry), or <see langword="null"/> to
    /// build it from the signer, reason, location and date. It never reads as only "Signature".
    /// </summary>
    public string? Description { get; init; }

    /// <summary>Gets a value indicating whether the rectangle has an area, so the signature is drawn.</summary>
    public bool IsVisible => !Rectangle.IsEmpty;

    /// <summary>Gets a value indicating whether the appearance draws any text.</summary>
    internal bool HasText => Lines is { Count: > 0 } || (Lines is null && (ShowName || ShowReason || ShowLocation || ShowDate));
}
