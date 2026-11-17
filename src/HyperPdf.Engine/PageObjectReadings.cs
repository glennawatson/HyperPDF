// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.PageObjects;

namespace HyperPdf.Engine;

/// <summary>Finds how the lines of text of a page read as the page is shown.</summary>
internal static class PageObjectReadings
{
    /// <summary>How far across, as a share of the way along, a baseline may run and still read to the right.</summary>
    private const float UprightTolerance = 1E-3F;

    /// <summary>Gets how a text object reads on the page as it is shown, at any angle.</summary>
    /// <param name="page">The page, whose own turn counts.</param>
    /// <param name="run">The text object.</param>
    /// <returns>The reading; the default for a line that reads to the right, so the object's bounds are its shape.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static TextReading Of(PdfPage page, PdfTextObject run) => Of(page, run, run);

    /// <summary>Gets how a text drawn in pieces reads on the page as it is shown, from where its first piece starts to where its last ends.</summary>
    /// <param name="page">The page, whose own turn counts.</param>
    /// <param name="run">The first piece.</param>
    /// <param name="lastPiece">The last piece, which is <paramref name="run"/> when the text is not in pieces.</param>
    /// <returns>The reading; the default for a line that reads to the right.</returns>
    internal static TextReading Of(PdfPage page, PdfTextObject run, PdfTextObject lastPiece)
    {
        if (run.GlyphCount == 0)
        {
            return default;
        }

        var matrix = run.TextMatrix * run.Matrix;
        var along = new Vector2(matrix.M11, matrix.M12);
        var scale = along.Length();
        if (scale <= 0F)
        {
            return default;
        }

        var origin = run.Glyphs[0].Origin;
        var shown = page.ToPhysical(origin + (along / scale)) - page.ToPhysical(origin);
        var direction = shown / shown.Length();
        if (MathF.Abs(direction.Y) <= UprightTolerance && direction.X > 0F)
        {
            return default;
        }

        // The line reaches from its first glyph's origin to where its last glyph's advance ends, and is as tall as its font is large across the baseline.
        var last = lastPiece.Glyphs[^1];
        var lastMatrix = lastPiece.TextMatrix * lastPiece.Matrix;
        var length = (page.ToPhysical(last.Origin + (new Vector2(lastMatrix.M11, lastMatrix.M12) * last.Advance)) - page.ToPhysical(origin)).Length();
        var height = run.FontSize * MathF.Sqrt((matrix.M21 * matrix.M21) + (matrix.M22 * matrix.M22));
        return new(direction.X, direction.Y, length, height);
    }
}
