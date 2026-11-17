// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdf.Core.Documents;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.PageObjects;

namespace HyperPdf.Engine;

/// <summary>Finds the way the lines of text of a page read as the page is shown.</summary>
internal static class PageObjectFlows
{
    /// <summary>The number of ways a line can read: to the right, upward, to the left and downward.</summary>
    private const int Flows = 4;

    /// <summary>Gets the way a text object reads on the page as it is shown, to the nearest quarter turn.</summary>
    /// <param name="page">The page, whose own turn counts.</param>
    /// <param name="run">The text object.</param>
    /// <returns>The way the text reads.</returns>
    internal static TextFlow Of(PdfPage page, PdfTextObject run)
    {
        if (run.GlyphCount == 0)
        {
            return TextFlow.Right;
        }

        var matrix = run.TextMatrix * run.Matrix;
        var origin = run.Glyphs[0].Origin;
        var shown = page.ToPhysical(origin + new Vector2(matrix.M11, matrix.M12)) - page.ToPhysical(origin);

        // Shown space runs downward, so the angle is measured against its up.
        var turns = (int)MathF.Round(MathF.Atan2(-shown.Y, shown.X) / (MathF.Tau / Flows));
        return (TextFlow)(((turns % Flows) + Flows) % Flows);
    }
}
