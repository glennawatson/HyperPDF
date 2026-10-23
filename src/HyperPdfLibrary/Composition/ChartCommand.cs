// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Charts;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>Draws a chart to fill a rectangle.</summary>
/// <param name="Node">The structure node.</param>
/// <param name="Artifact">The kind of artifact when there is no node.</param>
/// <param name="Chart">The chart.</param>
/// <param name="Style">The chart's style.</param>
/// <param name="Box">The rectangle.</param>
internal sealed record ChartCommand(StructNode? Node, PdfArtifactKind Artifact, PdfChart Chart, PdfChartStyle Style, PdfCanvasBox Box) : DrawCommand(Node, Artifact)
{
    /// <inheritdoc/>
    internal override void Write(ref PdfPageCanvas canvas, RenderState state) => canvas.DrawChart(Chart, Style, Box);

    /// <inheritdoc/>
    internal override DrawCommand Offset(float dx, float dy) => this with { Box = Box with { X = Box.X + dx, Y = Box.Y + dy } };
}
