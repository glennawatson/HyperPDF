// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Composition;

/// <summary>The border and shading of a paragraph: the room they take and the commands that draw them.</summary>
internal static class ParagraphBox
{
    /// <summary>Half, for centring a border line on the edge of the box.</summary>
    private const float Half = 0.5F;

    /// <summary>Works out how much room the borders and padding take on each side.</summary>
    /// <param name="format">The paragraph formatting.</param>
    /// <returns>The room on each side.</returns>
    internal static PdfInsets GetInsets(ResolvedParagraph format)
    {
        var borders = format.Borders;
        var padding = format.Padding;
        return new(
            (borders.Left?.Width ?? 0F) + padding.Left,
            (borders.Top?.Width ?? 0F) + padding.Top,
            (borders.Right?.Width ?? 0F) + padding.Right,
            (borders.Bottom?.Width ?? 0F) + padding.Bottom);
    }

    /// <summary>Moves the paragraph's indents in to make room for its borders and padding.</summary>
    /// <param name="format">The paragraph formatting.</param>
    /// <returns>The formatting with the room made.</returns>
    internal static ResolvedParagraph ApplyInsets(ResolvedParagraph format)
    {
        if (format.Borders.IsEmpty && format.Padding == PdfInsets.None)
        {
            return format;
        }

        var insets = GetInsets(format);
        return format with { LeftIndent = format.LeftIndent + insets.Left, RightIndent = format.RightIndent + insets.Right };
    }

    /// <summary>Draws the shading and borders round a stretch of a paragraph.</summary>
    /// <param name="sink">The commands of the page.</param>
    /// <param name="insertAt">Where in <paramref name="sink"/> the shading goes, so it lies behind the text drawn since then.</param>
    /// <param name="format">The paragraph formatting.</param>
    /// <param name="box">The box round the stretch, borders included.</param>
    /// <param name="first">Whether the stretch starts the paragraph, so the top border is drawn.</param>
    /// <param name="last">Whether the stretch ends the paragraph, so the bottom border is drawn.</param>
    internal static void Emit(List<DrawCommand> sink, int insertAt, ResolvedParagraph format, PdfCanvasBox box, bool first, bool last)
    {
        if (format.Shading is { } shading)
        {
            sink.Insert(insertAt, new FillCommand(null, PdfArtifactKind.Background, box, shading));
        }

        var borders = format.Borders;
        if (borders.Left is { } left)
        {
            AddLine(sink, left, new(box.X + (left.Width * Half), box.Y), new(box.X + (left.Width * Half), box.Top));
        }

        if (borders.Right is { } right)
        {
            AddLine(sink, right, new(box.Right - (right.Width * Half), box.Y), new(box.Right - (right.Width * Half), box.Top));
        }

        if (first && borders.Top is { } top)
        {
            AddLine(sink, top, new(box.X, box.Top - (top.Width * Half)), new(box.Right, box.Top - (top.Width * Half)));
        }

        if (last && borders.Bottom is { } bottom)
        {
            AddLine(sink, bottom, new(box.X, box.Y + (bottom.Width * Half)), new(box.Right, box.Y + (bottom.Width * Half)));
        }
    }

    /// <summary>Adds a border line.</summary>
    /// <param name="sink">The commands of the page.</param>
    /// <param name="side">The border edge.</param>
    /// <param name="from">Where the line starts.</param>
    /// <param name="to">Where the line ends.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void AddLine(List<DrawCommand> sink, PdfBorderSide side, PdfPoint from, PdfPoint to) =>
        sink.Add(new StrokeCommand(null, PdfArtifactKind.Layout, BorderPen.Create(side), from, to));
}
