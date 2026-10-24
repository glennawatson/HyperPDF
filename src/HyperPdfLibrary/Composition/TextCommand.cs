// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>Draws one line of text in one font, size and colour.</summary>
/// <param name="Node">The structure node.</param>
/// <param name="Artifact">The kind of artifact when there is no node.</param>
/// <param name="Font">The font.</param>
/// <param name="Size">The size in points.</param>
/// <param name="Color">The colour.</param>
/// <param name="X">The x where the baseline starts.</param>
/// <param name="Y">The y of the baseline.</param>
/// <param name="Text">The text, without line breaks.</param>
internal sealed record TextCommand(StructNode? Node, PdfArtifactKind Artifact, PdfCanvasFont Font, float Size, PdfCanvasColor Color, float X, float Y, string Text) : DrawCommand(Node, Artifact)
{
    /// <inheritdoc/>
    internal override void Write(ref PdfPageCanvas canvas, RenderState state)
    {
        canvas.Content.SaveState();
        Color.ApplyFill(ref canvas.Content);
        canvas.DrawText(Font, Size, X, Y, Text);
        canvas.Content.RestoreState();
    }

    /// <inheritdoc/>
    internal override DrawCommand Offset(float dx, float dy) => this with { X = X + dx, Y = Y + dy };
}
