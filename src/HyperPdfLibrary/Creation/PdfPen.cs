// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Creation;

/// <summary>How a line is stroked: its colour, width, ends, corners and dashes.</summary>
/// <param name="Color">The stroke colour.</param>
/// <param name="Width">The line width in points; zero draws the thinnest line the device can.</param>
[DebuggerDisplay("PdfPen: {Color} {Width} pt")]
public sealed record PdfPen(PdfCanvasColor Color, float Width)
{
    /// <summary>The default limit on how long a mitered corner may become, as a multiple of the line width.</summary>
    private const float DefaultMiterLimit = 10F;

    /// <summary>Gets how the ends of open lines are finished.</summary>
    public PdfLineCap Cap { get; init; }

    /// <summary>Gets how the corners are finished.</summary>
    public PdfLineJoin Join { get; init; }

    /// <summary>Gets the longest a mitered corner may be, as a multiple of the line width, before it is bevelled.</summary>
    public float MiterLimit { get; init; } = DefaultMiterLimit;

    /// <summary>Gets the lengths of the dashes and gaps, alternating; empty for a solid line.</summary>
    public ReadOnlyMemory<float> Dash { get; init; }

    /// <summary>Gets how far into the dash pattern the line starts.</summary>
    public float DashPhase { get; init; }

    /// <summary>Makes a solid black pen.</summary>
    /// <param name="width">The line width in points.</param>
    /// <returns>The pen.</returns>
    public static PdfPen Black(float width) => new(PdfCanvasColor.Black, width);

    /// <summary>Sets the stroke colour, width, ends, corners and dashes.</summary>
    /// <param name="builder">The content being written.</param>
    /// <exception cref="ArgumentOutOfRangeException">The width or miter limit is negative, or a dash length is negative.</exception>
    internal void Apply(ref PdfContentBuilder builder)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(Width);
        ArgumentOutOfRangeException.ThrowIfNegative(MiterLimit);
        Color.ApplyStroke(ref builder);
        builder.SetLineWidth(Width);
        builder.SetLineCap((int)Cap);
        builder.SetLineJoin((int)Join);
        builder.SetMiterLimit(MiterLimit);
        if (Dash.IsEmpty)
        {
            return;
        }

        foreach (var length in Dash.Span)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(length);
        }

        builder.SetDash(Dash.Span, DashPhase);
    }
}
