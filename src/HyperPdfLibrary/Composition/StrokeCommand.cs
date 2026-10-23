// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Composition;

/// <summary>Strokes a straight line, for borders and rules.</summary>
/// <param name="Node">The structure node.</param>
/// <param name="Artifact">The kind of artifact when there is no node.</param>
/// <param name="Pen">The pen.</param>
/// <param name="From">Where the line starts.</param>
/// <param name="To">Where the line ends.</param>
internal sealed record StrokeCommand(StructNode? Node, PdfArtifactKind Artifact, PdfPen Pen, PdfPoint From, PdfPoint To) : DrawCommand(Node, Artifact)
{
    /// <inheritdoc/>
    internal override void Write(ref PdfPageCanvas canvas, RenderState state) => canvas.DrawPolyline(Pen, [From, To]);

    /// <inheritdoc/>
    internal override DrawCommand Offset(float dx, float dy) => this with { From = new(From.X + dx, From.Y + dy), To = new(To.X + dx, To.Y + dy) };
}
