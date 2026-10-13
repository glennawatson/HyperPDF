// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// A text annotation drawn as a note icon (ISO 32000-2 12.5.6.4): a yellow speech box with three lines of text. The
/// icon is at most <see cref="MaxSide"/> points square and anchored at the upper-left corner of <c>/Rect</c>, where
/// ISO 32000-2 12.5.3 anchors annotations that do not zoom; it shrinks to fit a smaller <c>/Rect</c>.
/// </summary>
/// <param name="Bounds">The icon's rectangle.</param>
[DebuggerDisplay("NotePlan: {Bounds}")]
internal readonly record struct NotePlan(PdfRectangle Bounds) : IAppearancePlan<NotePlan>
{
    /// <summary>The side of the icon, in points: 20 points is 0.28 inch, large enough to click or tap and small enough not to cover text.</summary>
    internal const float MaxSide = 20;

    /// <summary>The speech tail's reach, as a fraction of the icon's side.</summary>
    private const float TailFraction = 0.2F;

    /// <summary>The inset of the lines from the icon's sides, as a fraction of its side.</summary>
    private const float LineInsetFraction = 0.1F;

    /// <summary>The lines inside the icon.</summary>
    private const int Lines = 3;

    /// <summary>The parts the box height is split into for its lines.</summary>
    private const float LineParts = 4;

    /// <inheritdoc/>
    public KnownName Blend => KnownName.Normal;

    /// <inheritdoc/>
    public static bool TryRead(PdfDictionary annotation, out NotePlan plan)
    {
        var rect = AppearanceRects.Read(annotation);
        var side = rect.Width > 0 ? Math.Min(MaxSide, rect.Width) : MaxSide;
        side = rect.Height > 0 ? Math.Min(side, rect.Height) : side;
        plan = new(new(rect.Left, rect.Top - side, rect.Left + side, rect.Top));
        return true;
    }

    /// <inheritdoc/>
    public void Emit(ref PdfContentBuilder builder)
    {
        builder.SetFillRgb(1, 1, 0);
        builder.SetStrokeRgb(0, 0, 0);
        builder.SetLineWidth(1);

        // The box is pulled in by half the line so the stroke stays inside the icon; its bottom is raised to leave room for the tail.
        var tail = Bounds.Width * TailFraction;
        var box = AppearanceRects.Inset(Bounds, AppearanceRects.Half);
        box = box with { Bottom = box.Bottom + tail };
        var tailLeft = box.Left + tail;
        var tailRight = tailLeft + tail;
        builder.MoveTo(box.Left, box.Bottom);
        builder.LineTo(box.Left, box.Top);
        builder.LineTo(box.Right, box.Top);
        builder.LineTo(box.Right, box.Bottom);
        builder.LineTo(tailRight, box.Bottom);
        builder.LineTo((tailLeft + tailRight) * AppearanceRects.Half, box.Bottom - tail);
        builder.LineTo(tailLeft, box.Bottom);
        builder.LineTo(box.Left, box.Bottom);

        var inset = Bounds.Width * LineInsetFraction;
        var step = (box.Top - box.Bottom) / LineParts;
        var y = box.Top;
        for (var i = 0; i < Lines; i++)
        {
            y -= step;
            builder.MoveTo(box.Left + inset, y);
            builder.LineTo(box.Right - inset, y);
        }

        builder.FillEvenOddAndStroke();
    }
}
