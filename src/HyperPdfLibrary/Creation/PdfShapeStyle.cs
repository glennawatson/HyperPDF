// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Creation;

/// <summary>How a shape is painted: filled, outlined, both, or neither (which only sets a clip).</summary>
/// <param name="Fill">The fill colour, or <see langword="null"/> to leave the inside unpainted.</param>
/// <param name="Outline">The pen for the outline, or <see langword="null"/> to leave the edge unpainted.</param>
[DebuggerDisplay("PdfShapeStyle: fill {Fill}, outline {Outline}")]
public readonly record struct PdfShapeStyle(PdfCanvasColor? Fill, PdfPen? Outline)
{
    /// <summary>Makes a style that fills a shape and does not outline it.</summary>
    /// <param name="color">The fill colour.</param>
    /// <returns>The style.</returns>
    public static PdfShapeStyle Filled(PdfCanvasColor color) => new(color, null);

    /// <summary>Makes a style that outlines a shape and does not fill it.</summary>
    /// <param name="pen">The pen.</param>
    /// <returns>The style.</returns>
    public static PdfShapeStyle Outlined(PdfPen pen) => new(null, pen);

    /// <summary>Sets the fill colour and the pen the style names.</summary>
    /// <param name="builder">The content being written.</param>
    internal void Apply(ref PdfContentBuilder builder)
    {
        Fill?.ApplyFill(ref builder);
        Outline?.Apply(ref builder);
    }

    /// <summary>Paints the path just built.</summary>
    /// <param name="builder">The content being written.</param>
    /// <param name="closed">Whether to close the path first, as an outline of a closed shape needs.</param>
    internal void Paint(ref PdfContentBuilder builder, bool closed)
    {
        if (Fill is not null && Outline is not null)
        {
            if (closed)
            {
                builder.CloseFillAndStroke();
                return;
            }

            builder.FillAndStroke();
            return;
        }

        if (Fill is not null)
        {
            builder.Fill();
            return;
        }

        if (Outline is not null)
        {
            if (closed)
            {
                builder.CloseAndStroke();
                return;
            }

            builder.Stroke();
            return;
        }

        builder.EndPath();
    }
}
