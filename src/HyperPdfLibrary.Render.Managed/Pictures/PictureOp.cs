// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Render.Managed.Images;
using HyperPdfLibrary.Render.Managed.Raster;

namespace HyperPdfLibrary.Render.Managed.Pictures;

/// <summary>One step of a recorded picture, in the space the picture was recorded in.</summary>
/// <param name="Kind">What the step does.</param>
/// <param name="Matrix">The matrix from the step's own space to the picture's space.</param>
[DebuggerDisplay("PictureOp: {Kind}")]
internal readonly record struct PictureOp(PictureOpKind Kind, Matrix3x2 Matrix)
{
    /// <summary>Gets the path that is clipped to, filled or stroked.</summary>
    internal PdfPath? Path { get; init; }

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

    /// <summary>Gets the premultiplied colour of a fill or stroke, or the colour a stencil image paints with.</summary>
    internal uint Color { get; init; }

    /// <summary>Gets how a shape, an image or a layer mixes with what is under it.</summary>
    internal HyperPdfLibrary.Graphics.PdfBlendMode Blend { get; init; }

    /// <summary>Gets the triangles of a mesh shading, in chunks.</summary>
    internal ManagedRenderVertices[]? Vertices { get; init; }

    /// <summary>Gets a value indicating whether a layer replaces what is under it where it painted, as an object of a knockout group does.</summary>
    internal bool Replaces { get; init; }

    /// <summary>Gets the table a soft mask's values are looked up in, or <see langword="null"/> to use them as they are.</summary>
    internal byte[]? Table { get; init; }

    /// <summary>Gets how a line is stroked.</summary>
    internal StrokeStyle Stroke { get; init; }

    /// <summary>Gets a value indicating whether a fill is the outline of a glyph, which is placed and smoothed as text.</summary>
    internal bool IsText { get; init; }

    /// <summary>Gets how opaque an image or a layer is, where 1 is fully.</summary>
    internal float Alpha { get; init; }

    /// <summary>
    /// Gets a value indicating the fill rule of a path (even-odd), whether the page asks for a stroked line to be fitted to pixels,
    /// the smoothing of an image, or the backdrop of a layer.
    /// </summary>
    internal bool Flag { get; init; }
}
