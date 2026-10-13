// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// A link annotation's border (ISO 32000-2 12.5.4 and 12.5.6.5). A link with neither <c>/BS</c> nor a <c>/Border</c> that
/// holds a width has no border, as in common viewers, rather than the standard's default of a one point border. The
/// border is in <c>/C</c> (black when absent; an empty <c>/C</c> means none), solid, dashed (<c>/BS /D</c>) or an
/// underline (<c>/BS /S /U</c>), and sits inside <c>/Rect</c>.
/// </summary>
/// <param name="Rect">The annotation rectangle, which the appearance is fitted to.</param>
/// <param name="Color">The border colour.</param>
/// <param name="Border">The border width and dash.</param>
/// <param name="Underline">Whether only a line along the bottom is drawn.</param>
[DebuggerDisplay("LinkPlan: {Rect}")]
internal readonly record struct LinkPlan(PdfRectangle Rect, PaintColor Color, PaintStroke Border, bool Underline) : IAppearancePlan<LinkPlan>
{
    /// <summary>The fewest entries a <c>/Border</c> array needs to give a width.</summary>
    private const int BorderWithWidth = 3;

    /// <inheritdoc/>
    public PdfRectangle Bounds => Rect;

    /// <inheritdoc/>
    public KnownName Blend => KnownName.Normal;

    /// <inheritdoc/>
    public static bool TryRead(PdfDictionary annotation, out LinkPlan plan)
    {
        plan = default;
        var style = annotation.GetDictionary(KnownName.BS);
        var rect = AppearanceRects.Read(annotation);
        var hasBorder = style is not null || annotation.GetArray(KnownName.Border) is { Count: >= BorderWithWidth };
        if (rect.IsEmpty || !hasBorder || annotation.GetArray(KnownName.C) is { Count: 0 })
        {
            return false;
        }

        var border = PaintStroke.Read(annotation);
        if (border.Width <= 0)
        {
            return false;
        }

        var underline = style is not null && style.IsName(KnownName.S, KnownName.U);
        plan = new(rect, PaintColor.Read(annotation.GetArray(KnownName.C), PaintColor.Black), border, underline);
        return true;
    }

    /// <inheritdoc/>
    public void Emit(ref PdfContentBuilder builder)
    {
        Color.SetStroke(ref builder);
        Border.Apply(ref builder);
        var inner = AppearanceRects.Inset(Rect, Border.Width * AppearanceRects.Half);
        if (Underline)
        {
            builder.MoveTo(inner.Left, inner.Bottom);
            builder.LineTo(inner.Right, inner.Bottom);
        }
        else
        {
            builder.Rectangle(inner.Left, inner.Bottom, inner.Width, inner.Height);
        }

        builder.Stroke();
    }
}
