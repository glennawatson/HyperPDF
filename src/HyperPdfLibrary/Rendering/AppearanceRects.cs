// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Rendering;

/// <summary>Reads and adjusts the rectangles generated annotation appearances are drawn in.</summary>
internal static class AppearanceRects
{
    /// <summary>One half, to centre a line on an edge or find a middle.</summary>
    internal const float Half = 0.5F;

    /// <summary>The numbers in an inset entry (<c>/RD</c>): left, top, right, bottom (ISO 32000-2 table 180).</summary>
    private const int InsetNumbers = 4;

    /// <summary>The position of the top inset.</summary>
    private const int InsetTop = 1;

    /// <summary>The position of the right inset.</summary>
    private const int InsetRight = 2;

    /// <summary>The position of the bottom inset.</summary>
    private const int InsetBottom = 3;

    /// <summary>Gets an annotation's normalised <c>/Rect</c>.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The rectangle, or an empty one.</returns>
    internal static PdfRectangle Read(PdfDictionary annotation) =>
        annotation.TryGetRectangle(KnownName.Rect, out var rect) ? PdfRectangle.FromCorners(rect.Left, rect.Bottom, rect.Right, rect.Top) : default;

    /// <summary>Moves every side of a rectangle in by an amount; a negative amount grows it.</summary>
    /// <param name="rect">The rectangle.</param>
    /// <param name="amount">How far to move each side.</param>
    /// <returns>The adjusted rectangle.</returns>
    internal static PdfRectangle Inset(PdfRectangle rect, float amount) =>
        new(rect.Left + amount, rect.Bottom + amount, rect.Right - amount, rect.Top - amount);

    /// <summary>Applies the differences of <c>/RD</c> (ISO 32000-2 12.5.6.5) to a rectangle, when the annotation has them.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="rect">The <c>/Rect</c>.</param>
    /// <param name="fallback">The rectangle used without differences.</param>
    /// <returns>The inner rectangle.</returns>
    internal static PdfRectangle ApplyDifferences(PdfDictionary annotation, PdfRectangle rect, PdfRectangle fallback) =>
        annotation.GetArray(KnownName.RD) is { Count: InsetNumbers } inset
            ? new(rect.Left + inset.GetSingle(0), rect.Bottom + inset.GetSingle(InsetBottom), rect.Right - inset.GetSingle(InsetRight), rect.Top - inset.GetSingle(InsetTop))
            : fallback;
}
