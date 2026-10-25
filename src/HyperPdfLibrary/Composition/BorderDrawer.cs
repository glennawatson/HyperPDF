// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Composition;

/// <summary>Draws the border lines round a box, for table cells and frames.</summary>
internal static class BorderDrawer
{
    /// <summary>Half, for centring a line on the edge of the box.</summary>
    private const float Half = 0.5F;

    /// <summary>Adds the lines of a border round a box.</summary>
    /// <param name="sink">Where the commands go.</param>
    /// <param name="borders">The borders.</param>
    /// <param name="box">The box.</param>
    /// <param name="artifact">The kind of artifact the lines are drawn as.</param>
    internal static void Draw(List<DrawCommand> sink, in PdfBorders borders, PdfCanvasBox box, PdfArtifactKind artifact)
    {
        if (borders.Left is { } left)
        {
            Add(sink, left, new(box.X + (left.Width * Half), box.Y), new(box.X + (left.Width * Half), box.Top), artifact);
        }

        if (borders.Right is { } right)
        {
            Add(sink, right, new(box.Right - (right.Width * Half), box.Y), new(box.Right - (right.Width * Half), box.Top), artifact);
        }

        if (borders.Top is { } top)
        {
            Add(sink, top, new(box.X, box.Top - (top.Width * Half)), new(box.Right, box.Top - (top.Width * Half)), artifact);
        }

        if (borders.Bottom is { } bottom)
        {
            Add(sink, bottom, new(box.X, box.Y + (bottom.Width * Half)), new(box.Right, box.Y + (bottom.Width * Half)), artifact);
        }
    }

    /// <summary>Adds a border line.</summary>
    /// <param name="sink">Where the command goes.</param>
    /// <param name="side">The border edge.</param>
    /// <param name="from">Where the line starts.</param>
    /// <param name="to">Where the line ends.</param>
    /// <param name="artifact">The kind of artifact.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void Add(List<DrawCommand> sink, PdfBorderSide side, PdfPoint from, PdfPoint to, PdfArtifactKind artifact) =>
        sink.Add(new StrokeCommand(null, artifact, BorderPen.Create(side), from, to));
}
