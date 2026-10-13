// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// A redaction annotation's preview (ISO 32000-2 12.5.6.23): the marked region (<c>/QuadPoints</c>, else <c>/Rect</c>) as
/// an unfilled outline in <c>/C</c> (red when absent). Nothing is hidden or filled: <c>/IC</c>, <c>/RO</c> and
/// <c>/OverlayText</c> describe how the region looks after the redaction is applied, and the content under the mark
/// stays readable for review. Applying the redaction is a separate editing step.
/// </summary>
/// <param name="Quads">The quadrilaterals, or <see langword="null"/> to outline <see cref="Bounds"/>.</param>
/// <param name="Bounds">The rectangle round every region.</param>
/// <param name="Color">The outline colour.</param>
[DebuggerDisplay("RedactPlan: {Bounds}")]
internal readonly record struct RedactPlan(PdfArray? Quads, PdfRectangle Bounds, PaintColor Color) : IAppearancePlan<RedactPlan>
{
    /// <summary>The preview outline colour used when the annotation has no <c>/C</c>.</summary>
    private const uint Red = 0xFF0000;

    /// <summary>The preview outline width in points.</summary>
    private const float OutlineWidth = 1;

    /// <inheritdoc/>
    public KnownName Blend => KnownName.Normal;

    /// <inheritdoc/>
    public static bool TryRead(PdfDictionary annotation, out RedactPlan plan)
    {
        var points = annotation.GetArray(KnownName.QuadPoints);
        var quads = points?.Count >= QuadPoints.QuadNumbers ? points : null;
        var bounds = quads is null ? AppearanceRects.Read(annotation) : QuadPoints.Bounds(quads);
        plan = new(quads, bounds, PaintColor.Read(annotation.GetArray(KnownName.C), PaintColor.FromRgb(Red)));
        return !bounds.IsEmpty;
    }

    /// <inheritdoc/>
    public void Emit(ref PdfContentBuilder builder)
    {
        Color.SetStroke(ref builder);
        builder.SetLineWidth(OutlineWidth);
        if (Quads is null)
        {
            EmitOutline(ref builder, Bounds);
            return;
        }

        var count = QuadPoints.Count(Quads);
        for (var i = 0; i < count; i++)
        {
            EmitOutline(ref builder, QuadPoints.Rectangle(Quads, i));
        }
    }

    /// <summary>Strokes one region just inside its edges, so the line is not clipped by the appearance box.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="region">The region.</param>
    private static void EmitOutline(ref PdfContentBuilder builder, PdfRectangle region)
    {
        var inner = AppearanceRects.Inset(region, OutlineWidth * AppearanceRects.Half);
        builder.Rectangle(inner.Left, inner.Bottom, inner.Width, inner.Height);
        builder.Stroke();
    }
}
