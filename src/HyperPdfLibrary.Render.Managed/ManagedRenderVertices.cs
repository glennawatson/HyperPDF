// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Render.Managed;

/// <summary>The triangles of a mesh shading, three points each, with a colour or an image position at every point.</summary>
/// <param name="Positions">The corners of the triangles.</param>
/// <param name="Texture">Where each corner is in an image, or <see langword="null"/> when the mesh has colours only.</param>
/// <param name="Colors">The colour at each corner, or <see langword="null"/> when the mesh has image positions only.</param>
[DebuggerDisplay("ManagedRenderVertices: {Positions.Length} corners")]
internal sealed record ManagedRenderVertices(PdfPoint[] Positions, PdfPoint[]? Texture, PdfColor[]? Colors) : IPdfRenderVertices
{
    /// <inheritdoc/>
    public void Dispose()
    {
        // The mesh holds only managed arrays, which the collector frees.
    }
}
