// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// An ink annotation (ISO 32000-2 12.5.6.13): each stroke of <c>/InkList</c> drawn as an open path in the border width,
/// in a rectangle grown by half the line so the stroke's edge fits.
/// </summary>
/// <param name="Strokes">The <c>/InkList</c>, an array of coordinate arrays, read in place.</param>
/// <param name="Color">The line colour, black when absent.</param>
/// <param name="Border">The line width and dash.</param>
/// <param name="Bounds">The rectangle the appearance is fitted to.</param>
[DebuggerDisplay("InkPlan: {Strokes.Count} strokes")]
internal readonly record struct InkPlan(PdfArray Strokes, PaintColor Color, PaintStroke Border, PdfRectangle Bounds) : IAppearancePlan<InkPlan>
{
    /// <summary>The numbers in one point.</summary>
    private const int PointFields = 2;

    /// <inheritdoc/>
    public KnownName Blend => KnownName.Normal;

    /// <inheritdoc/>
    public static bool TryRead(PdfDictionary annotation, out InkPlan plan)
    {
        plan = default;
        var border = PaintStroke.Read(annotation);
        if (annotation.GetArray(KnownName.InkList) is not { Count: > 0 } strokes || border.Width <= 0)
        {
            return false;
        }

        var bounds = AppearanceRects.Inset(AppearanceRects.Read(annotation), -border.Width * AppearanceRects.Half);
        plan = new(strokes, PaintColor.Read(annotation.GetArray(KnownName.C), PaintColor.Black), border, bounds);
        return true;
    }

    /// <inheritdoc/>
    public void Emit(ref PdfContentBuilder builder)
    {
        Color.SetStroke(ref builder);
        Border.Apply(ref builder);
        for (var i = 0; i < Strokes.Count; i++)
        {
            EmitStroke(ref builder, Strokes.GetArray(i));
        }
    }

    /// <summary>Writes one stroke: a move to its first point, a line through the rest, then a stroke.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="points">The stroke's coordinates, or <see langword="null"/> for an entry that is not an array.</param>
    private static void EmitStroke(ref PdfContentBuilder builder, PdfArray? points)
    {
        if (points is not { Count: >= PointFields })
        {
            return;
        }

        builder.MoveTo(points.GetSingle(0), points.GetSingle(1));
        for (var i = PointFields; i + 1 < points.Count; i += PointFields)
        {
            builder.LineTo(points.GetSingle(i), points.GetSingle(i + 1));
        }

        builder.Stroke();
    }
}
