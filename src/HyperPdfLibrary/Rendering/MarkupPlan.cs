// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// A text markup annotation (ISO 32000-2 12.5.6.10): a highlight fills each quadrilateral and multiplies with the page;
/// an underline, strike-out or squiggly line is stroked in the colour along each quadrilateral.
/// </summary>
/// <param name="Style">Which markup.</param>
/// <param name="Quads">The <c>/QuadPoints</c>, read in place.</param>
/// <param name="Color">The fill colour of a highlight, or the stroke colour of the other styles.</param>
/// <param name="Bounds">The rectangle round every quadrilateral.</param>
[DebuggerDisplay("MarkupPlan: {Style}")]
internal readonly record struct MarkupPlan(MarkupStyle Style, PdfArray Quads, PaintColor Color, PdfRectangle Bounds) : IAppearancePlan<MarkupPlan>
{
    /// <summary>The default highlight colour, yellow.</summary>
    private const uint Yellow = 0xFFFF00;

    /// <summary>The quadrilateral height that gets a one point line: a line a twelfth of the text height is 1 point for 12 point text and grows with the text.</summary>
    private const float LineHeightDivisor = 12;

    /// <summary>The thinnest line, in points, so lines under tiny text stay visible.</summary>
    private const float MinLineWeight = 0.5F;

    /// <summary>The thickest line, in points, so lines under huge text stay lines.</summary>
    private const float MaxLineWeight = 3;

    /// <summary>The wave height and half period of a squiggly line, as a multiple of the line weight.</summary>
    private const float SquiggleWeights = 2;

    /// <inheritdoc/>
    public KnownName Blend => Style == MarkupStyle.Highlight ? KnownName.Multiply : KnownName.Normal;

    /// <inheritdoc/>
    public static bool TryRead(PdfDictionary annotation, out MarkupPlan plan)
    {
        plan = default;
        var style = annotation.GetName(KnownName.Subtype).ToKnownName() switch
        {
            KnownName.Highlight => MarkupStyle.Highlight,
            KnownName.Underline => MarkupStyle.Underline,
            KnownName.StrikeOut => MarkupStyle.StrikeOut,
            KnownName.Squiggly => MarkupStyle.Squiggly,
            _ => MarkupStyle.None,
        };
        if (style == MarkupStyle.None || annotation.GetArray(KnownName.QuadPoints) is not { } quads || QuadPoints.Bounds(quads).IsEmpty)
        {
            return false;
        }

        var fallback = PaintColor.FromRgb(style == MarkupStyle.Highlight ? Yellow : 0);
        plan = new(style, quads, PaintColor.Read(annotation.GetArray(KnownName.C), fallback), QuadPoints.Bounds(quads));
        return true;
    }

    /// <inheritdoc/>
    public void Emit(ref PdfContentBuilder builder)
    {
        if (Style == MarkupStyle.Highlight)
        {
            Color.SetFill(ref builder);
        }
        else
        {
            Color.SetStroke(ref builder);
        }

        var count = QuadPoints.Count(Quads);
        for (var i = 0; i < count; i++)
        {
            EmitQuad(ref builder, Style, QuadPoints.Rectangle(Quads, i));
        }
    }

    /// <summary>Writes the mark of one quadrilateral.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="style">Which markup.</param>
    /// <param name="rect">The quadrilateral's rectangle.</param>
    private static void EmitQuad(ref PdfContentBuilder builder, MarkupStyle style, PdfRectangle rect)
    {
        switch (style)
        {
            case MarkupStyle.Highlight:
            {
                builder.Rectangle(rect.Left, rect.Bottom, rect.Width, rect.Height);
                builder.Fill();
                break;
            }

            case MarkupStyle.Underline:
            {
                // The line sits one line weight above the bottom so its lower edge touches the quadrilateral's.
                var weight = LineWeight(rect);
                builder.SetLineWidth(weight);
                builder.MoveTo(rect.Left, rect.Bottom + weight);
                builder.LineTo(rect.Right, rect.Bottom + weight);
                builder.Stroke();
                break;
            }

            case MarkupStyle.StrikeOut:
            {
                var middle = (rect.Top + rect.Bottom) * AppearanceRects.Half;
                builder.SetLineWidth(LineWeight(rect));
                builder.MoveTo(rect.Left, middle);
                builder.LineTo(rect.Right, middle);
                builder.Stroke();
                break;
            }

            case MarkupStyle.Squiggly:
            {
                EmitSquiggle(ref builder, rect);
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Gets the line weight for text of a quadrilateral's height: a twelfth of it, between half a point and three points.</summary>
    /// <param name="rect">The quadrilateral's rectangle.</param>
    /// <returns>The weight in points.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float LineWeight(PdfRectangle rect) => Math.Clamp(rect.Height / LineHeightDivisor, MinLineWeight, MaxLineWeight);

    /// <summary>Writes the zigzag under squiggly text: a triangle wave whose height and half period are two line weights.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="rect">The quadrilateral's rectangle.</param>
    private static void EmitSquiggle(ref PdfContentBuilder builder, PdfRectangle rect)
    {
        var weight = LineWeight(rect);
        builder.SetLineWidth(weight);
        var step = weight * SquiggleWeights;
        var crest = rect.Bottom + step;
        builder.MoveTo(rect.Left, crest);
        var x = rect.Left + step;
        var rising = false;
        while (x < rect.Right)
        {
            builder.LineTo(x, rising ? crest : rect.Bottom);
            x += step;
            rising = !rising;
        }

        // The last half wave is cut at the right edge, keeping its slope.
        var remainder = rect.Right - (x - step);
        builder.LineTo(rect.Right, rising ? rect.Bottom + remainder : crest - remainder);
        builder.Stroke();
    }
}
