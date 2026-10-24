// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;

namespace HyperPdfLibrary.Composition;

/// <summary>Names a place on the page that links and outline entries can jump to. It paints nothing.</summary>
/// <param name="Name">The bookmark's name.</param>
/// <param name="X">The x of the place.</param>
/// <param name="Y">The y of the place.</param>
internal sealed record DestinationCommand(string Name, float X, float Y) : DrawCommand(null, PdfArtifactKind.Layout)
{
    /// <inheritdoc/>
    internal override bool Paints => false;

    /// <inheritdoc/>
    internal override void Write(ref PdfPageCanvas canvas, RenderState state) => state.AddDestination(Name, X, Y);

    /// <inheritdoc/>
    internal override DrawCommand Offset(float dx, float dy) => this with { X = X + dx, Y = Y + dy };
}
