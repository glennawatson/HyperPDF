// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>Marks an area of the page as a link. It paints nothing; the annotation is added when the page ends.</summary>
/// <param name="Node">The structure node of the link, or <see langword="null"/> when the document is not tagged.</param>
/// <param name="Artifact">The kind of artifact when there is no node.</param>
/// <param name="Box">The clickable area.</param>
/// <param name="Target">Where the link goes.</param>
internal sealed record LinkCommand(StructNode? Node, PdfArtifactKind Artifact, PdfCanvasBox Box, PdfLinkTarget Target) : DrawCommand(Node, Artifact)
{
    /// <inheritdoc/>
    internal override bool Paints => false;

    /// <inheritdoc/>
    internal override void Write(ref PdfPageCanvas canvas, RenderState state) => state.PendingLinks.Add(new(Box, Target, Node));

    /// <inheritdoc/>
    internal override DrawCommand Offset(float dx, float dy) => this with { Box = Box with { X = Box.X + dx, Y = Box.Y + dy } };
}
