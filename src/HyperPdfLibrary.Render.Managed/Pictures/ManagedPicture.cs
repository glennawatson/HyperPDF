// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Render.Managed.Images;
using HyperPdfLibrary.Render.Managed.Raster;

namespace HyperPdfLibrary.Render.Managed.Pictures;

/// <summary>
/// A recorded picture: the steps that draw a page, a form or a pattern cell, kept so they can be drawn at any scale and into any
/// tile without reading the content stream again.
/// </summary>
/// <param name="ops">The steps in drawing order.</param>
/// <param name="style">How the picture places and smooths its text and fits its lines.</param>
[DebuggerDisplay("ManagedPicture: {_ops.Length} steps")]
internal sealed class ManagedPicture(PictureOp[] ops, RasterStyle style) : IPdfRenderPicture
{
    /// <summary>The largest byte, which stands for one.</summary>
    private const float Full = 255F;

    /// <summary>The bytes one step is counted as when the picture's size is estimated.</summary>
    private const int BytesPerOp = 96;

    /// <summary>The steps in drawing order.</summary>
    private readonly PictureOp[] _ops = ops;

    /// <inheritdoc/>
    public long ApproximateBytesUsed => (long)_ops.Length * BytesPerOp;

    /// <inheritdoc/>
    public void Dispose()
    {
        // The steps hold only managed objects, which the collector frees.
    }

    /// <summary>Draws the picture.</summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="transform">The matrix from the picture's space to device pixels.</param>
    internal void Draw(RasterCanvas canvas, Matrix3x2 transform)
    {
        // A picture drawn inside another, such as a pattern cell, keeps its own style and hands the outer one back.
        var outer = canvas.Style;
        canvas.Style = style;
        foreach (ref readonly var op in _ops.AsSpan())
        {
            if (op.Kind <= PictureOpKind.Restore)
            {
                DrawState(canvas, op.Kind);
            }
            else if (op.Kind >= PictureOpKind.BeginLayer)
            {
                DrawLayer(canvas, op, transform);
            }
            else
            {
                DrawShape(canvas, op, op.Matrix * transform, PaintOf(canvas, op, transform));
            }
        }

        canvas.Style = outer;
    }

    /// <summary>Makes the paint of a step: its colour, or its gradient placed in device pixels.</summary>
    /// <param name="canvas">The canvas, which lends the gradient shader.</param>
    /// <param name="op">The step.</param>
    /// <param name="transform">The matrix from the picture's space to device pixels.</param>
    /// <returns>The paint. A gradient whose matrix cannot be inverted paints nothing.</returns>
    private static RasterPaint PaintOf(RasterCanvas canvas, in PictureOp op, Matrix3x2 transform)
    {
        var alpha = (uint)MathF.Round(Math.Clamp(op.Alpha, 0F, 1F) * Full);
        if (op.Kind != PictureOpKind.Picture && op.Picture is { } pattern)
        {
            return canvas.Tiles.Set(pattern, op.Tile, op.ShaderMatrix * transform, alpha) ? new(0, RasterShaderKind.Tile) { Blend = op.Blend } : default;
        }

        if (op.Shader is not { } gradient || op.Kind == PictureOpKind.Mesh)
        {
            return new(op.Color, RasterShaderKind.None) { Blend = op.Blend };
        }

        if (gradient.Image is ManagedRenderImage sampled)
        {
            // A function-based shading is sampled into an image, which is painted through the shading's matrix.
            if (!Matrix3x2.Invert(gradient.ImageMatrix * op.ShaderMatrix * transform, out var toImage))
            {
                return default;
            }

            canvas.Images.Set(sampled, toImage, 0, alpha, true);
            return new(0, RasterShaderKind.Image) { Blend = op.Blend };
        }

        if (!Matrix3x2.Invert(op.ShaderMatrix * transform, out var toGradient))
        {
            return default;
        }

        canvas.Gradients.Set(gradient, toGradient, alpha);
        return new(0, RasterShaderKind.Gradient) { Blend = op.Blend };
    }

    /// <summary>Does a step that saves or restores the clip.</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="kind">The step.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void DrawState(RasterCanvas canvas, PictureOpKind kind)
    {
        if (kind == PictureOpKind.Save)
        {
            canvas.Save();
        }
        else
        {
            canvas.Restore();
        }
    }

    /// <summary>Does a step that starts, masks or ends a layer.</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="op">The step.</param>
    /// <param name="transform">The matrix from the picture's space to device pixels.</param>
    private static void DrawLayer(RasterCanvas canvas, in PictureOp op, Matrix3x2 transform)
    {
        if (op.Kind == PictureOpKind.BeginLayer)
        {
            canvas.BeginLayer(op.Alpha, op.Flag, op.Blend, op.Replaces);
        }
        else if (op.Kind == PictureOpKind.EndLayer)
        {
            canvas.EndLayer();
        }
        else if (op.Picture is { } mask)
        {
            // The mask is drawn into a layer of its own, over its backdrop colour, and the layer under it is then faded by it.
            canvas.BeginMask(op.Color);
            mask.Draw(canvas, transform);
            canvas.EndMask(op.Flag, op.Table);
        }
    }

    /// <summary>Does a step that clips to, fills or strokes a path, or draws an image.</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="op">The step.</param>
    /// <param name="matrix">The matrix from the step's space to device pixels.</param>
    /// <param name="paint">The step's colour or gradient.</param>
    private static void DrawShape(RasterCanvas canvas, in PictureOp op, Matrix3x2 matrix, in RasterPaint paint)
    {
        switch (op.Kind)
        {
            case PictureOpKind.Clip when op.Path is { } path:
                {
                    canvas.ClipPath(path, op.Flag, matrix);
                    break;
                }

            case PictureOpKind.Fill when op.Path is { } path && op.IsText:
                {
                    canvas.FillText(path, matrix, paint);
                    break;
                }

            case PictureOpKind.Fill when op.Path is { } path:
                {
                    canvas.FillPath(path, op.Flag, matrix, paint);
                    break;
                }

            case PictureOpKind.Stroke when op.Path is { } path:
                {
                    canvas.StrokePath(path, matrix, op.Stroke with { Width = canvas.Style.FitWidth(op.Stroke.Width, matrix, op.Flag) }, paint);
                    break;
                }

            case PictureOpKind.Paint:
                {
                    canvas.FillClip(paint);
                    break;
                }

            case PictureOpKind.Picture when op.Picture is { } picture:
                {
                    picture.Draw(canvas, matrix);
                    break;
                }

            case PictureOpKind.Mesh when op.Vertices is { } chunks:
                {
                    canvas.DrawMesh(chunks, new(op.Shader, matrix, (uint)MathF.Round(Math.Clamp(op.Alpha, 0F, 1F) * Full), op.Blend));
                    break;
                }

            case PictureOpKind.Image when op.Image is { } image:
                {
                    ImageDrawing.Draw(canvas, image, matrix, op);
                    break;
                }

            default:
                {
                    break;
                }
        }
    }
}
