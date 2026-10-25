// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>Fills a rectangle, for shading, highlights and underlines.</summary>
/// <param name="Node">The structure node.</param>
/// <param name="Artifact">The kind of artifact when there is no node.</param>
/// <param name="Box">The rectangle.</param>
/// <param name="Color">The colour.</param>
internal sealed record FillCommand(StructNode? Node, PdfArtifactKind Artifact, PdfCanvasBox Box, PdfCanvasColor Color) : DrawCommand(Node, Artifact)
{
    /// <inheritdoc/>
    internal override void Write(ref PdfPageCanvas canvas, RenderState state)
    {
        canvas.Content.SaveState();
        Color.ApplyFill(ref canvas.Content);
        canvas.Content.Rectangle(Box.X, Box.Y, Box.Width, Box.Height);
        canvas.Content.Fill();
        canvas.Content.RestoreState();
    }

    /// <inheritdoc/>
    internal override DrawCommand Offset(float dx, float dy) => this with { Box = Box with { X = Box.X + dx, Y = Box.Y + dy } };
}
