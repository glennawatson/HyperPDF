// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>One thing to draw on a page, laid out and waiting for the page to be written.</summary>
/// <param name="Node">The structure node the command belongs to, or <see langword="null"/> for an artifact.</param>
/// <param name="Artifact">The kind of artifact when <paramref name="Node"/> is <see langword="null"/>.</param>
[DebuggerDisplay("DrawCommand: {Node}")]
internal abstract record DrawCommand(StructNode? Node, PdfArtifactKind Artifact)
{
    /// <summary>Gets a value indicating whether the command paints, so a tagged document must draw it inside marked content.</summary>
    internal virtual bool Paints => true;

    /// <summary>Writes the command to a page.</summary>
    /// <param name="canvas">The page.</param>
    /// <param name="state">The document being written.</param>
    internal abstract void Write(ref PdfPageCanvas canvas, RenderState state);

    /// <summary>Makes a copy that is moved.</summary>
    /// <param name="dx">How far to move right.</param>
    /// <param name="dy">How far to move up.</param>
    /// <returns>The moved command.</returns>
    internal abstract DrawCommand Offset(float dx, float dy);
}
