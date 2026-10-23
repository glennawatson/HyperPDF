// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>Draws an image scaled to a rectangle.</summary>
/// <param name="Node">The structure node.</param>
/// <param name="Artifact">The kind of artifact when there is no node.</param>
/// <param name="Image">The image.</param>
/// <param name="Box">The rectangle.</param>
internal sealed record ImageCommand(StructNode? Node, PdfArtifactKind Artifact, PdfCreatedImage Image, PdfCanvasBox Box) : DrawCommand(Node, Artifact)
{
    /// <inheritdoc/>
    internal override void Write(ref PdfPageCanvas canvas, RenderState state) => canvas.DrawImage(Image, Box.X, Box.Y, Box.Width, Box.Height);

    /// <inheritdoc/>
    internal override DrawCommand Offset(float dx, float dy) => this with { Box = Box with { X = Box.X + dx, Y = Box.Y + dy } };
}
