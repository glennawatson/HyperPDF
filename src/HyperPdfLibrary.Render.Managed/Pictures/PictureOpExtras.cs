// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Render.Managed.Images;
using HyperPdfLibrary.Render.Managed.Raster;

namespace HyperPdfLibrary.Render.Managed.Pictures;

/// <summary>
/// What only some steps of a picture carry: an image, a pattern, a gradient, a mesh, a soft mask's table or the style of a line.
/// A page is mostly glyphs, which carry none of these, so they are kept apart from the step and a glyph's step stays small.
/// </summary>
[DebuggerDisplay("PictureOpExtras")]
internal sealed record PictureOpExtras
{
    /// <summary>Gets the extras of a step that carries none, which a step being made starts from.</summary>
    internal static PictureOpExtras None { get; } = new();

    /// <summary>Gets the image that is drawn.</summary>
    internal ManagedRenderImage? Image { get; init; }

    /// <summary>Gets the picture a picture step draws, or the period of the tiling pattern a fill or stroke paints with.</summary>
    internal ManagedPicture? Picture { get; init; }

    /// <summary>Gets the period of a tiling pattern, in the pattern's space.</summary>
    internal PdfRect Tile { get; init; }

    /// <summary>Gets the gradient a fill, a stroke or a paint step uses in place of a colour.</summary>
    internal ManagedRenderShader? Shader { get; init; }

    /// <summary>Gets the matrix from the gradient's space to the picture's space.</summary>
    internal Matrix3x2 ShaderMatrix { get; init; }

    /// <summary>Gets the triangles of a mesh shading, in chunks.</summary>
    internal ManagedRenderVertices[]? Vertices { get; init; }

    /// <summary>Gets the table a soft mask's values are looked up in.</summary>
    internal byte[]? Table { get; init; }

    /// <summary>Gets how a line is stroked.</summary>
    internal StrokeStyle Stroke { get; init; }
}
