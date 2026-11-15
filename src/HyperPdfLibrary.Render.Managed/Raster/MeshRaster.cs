// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>
/// Draws the triangles of a mesh shading. The colour of each pixel is mixed from the triangle's three corners by how close the pixel
/// is to each, or looked up in the shading's colour ramp by a position mixed the same way.
/// </summary>
internal static class MeshRaster
{
    /// <summary>The corners of a triangle.</summary>
    private const int Corners = 3;

    /// <summary>The largest byte, which stands for one.</summary>
    private const float Full = 255F;

    /// <summary>The centre of a pixel.</summary>
    private const float Half = 0.5F;

    /// <summary>How far outside a triangle a pixel centre may be and still be drawn, so neighbouring triangles leave no gap.</summary>
    private const float Overlap = -1E-3F;

    /// <summary>The area below which a triangle has no inside.</summary>
    private const float Tiny = 1E-9F;

    /// <summary>The shift of the alpha byte of a pixel.</summary>
    private const int AlphaShift = 24;

    /// <summary>The shift of the red byte of a pixel.</summary>
    private const int RedShift = 16;

    /// <summary>The shift of the green byte of a pixel.</summary>
    private const int GreenShift = 8;

    /// <summary>Draws the triangles of one chunk of a mesh.</summary>
    /// <param name="target">The pixels to draw into.</param>
    /// <param name="clip">The clip in force.</param>
    /// <param name="vertices">The triangles.</param>
    /// <param name="job">The ramp, the matrix, the alpha and the blend mode.</param>
    internal static void Draw(RasterLayer target, ClipStack clip, ManagedRenderVertices vertices, in MeshJob job)
    {
        var positions = vertices.Positions;
        Span<Vector2> points = stackalloc Vector2[Corners];
        Span<Vector4> colors = stackalloc Vector4[Corners];
        for (var first = 0; first + Corners <= positions.Length; first += Corners)
        {
            for (var corner = 0; corner < Corners; corner++)
            {
                var index = first + corner;
                points[corner] = Vector2.Transform(new(positions[index].X, positions[index].Y), job.ToDevice);
                colors[corner] = CornerValue(vertices, index, job.Ramp);
            }

            DrawTriangle(target, clip, points, colors, job);
        }
    }

    /// <summary>Gets what is mixed across a triangle from one corner: its premultiplied colour, or its position along the ramp.</summary>
    /// <param name="vertices">The triangles.</param>
    /// <param name="index">The corner.</param>
    /// <param name="ramp">The colour ramp, or <see langword="null"/> when corners carry colours.</param>
    /// <returns>The colour as red, green, blue and alpha from 0 to 1, or the ramp position in the first component.</returns>
    private static Vector4 CornerValue(ManagedRenderVertices vertices, int index, ManagedRenderShader? ramp)
    {
        if (ramp is not null && vertices.Texture is { } texture)
        {
            var start = new Vector2(ramp.Start.X, ramp.Start.Y);
            var axis = new Vector2(ramp.End.X, ramp.End.Y) - start;
            var length = axis.LengthSquared();
            return new(length > 0 ? Vector2.Dot(new Vector2(texture[index].X, texture[index].Y) - start, axis) / length : 0, 0, 0, 0);
        }

        if (vertices.Colors is not { } colors)
        {
            return default;
        }

        var color = colors[index];
        var alpha = color.Alpha / Full;
        return new(color.Red / Full * alpha, color.Green / Full * alpha, color.Blue / Full * alpha, alpha);
    }

    /// <summary>Draws one triangle.</summary>
    /// <param name="target">The pixels to draw into.</param>
    /// <param name="clip">The clip in force.</param>
    /// <param name="points">The corners in device pixels.</param>
    /// <param name="values">What is mixed from each corner.</param>
    /// <param name="job">The ramp, the alpha and the blend mode.</param>
    private static void DrawTriangle(RasterLayer target, ClipStack clip, ReadOnlySpan<Vector2> points, ReadOnlySpan<Vector4> values, in MeshJob job)
    {
        var a = points[0];
        var b = points[1];
        var c = points[Corners - 1];
        var area = Cross(b - a, c - a);
        var bounds = PixelRect.Around(MathF.Min(a.X, MathF.Min(b.X, c.X)), MathF.Min(a.Y, MathF.Min(b.Y, c.Y)), MathF.Max(a.X, MathF.Max(b.X, c.X)), MathF.Max(a.Y, MathF.Max(b.Y, c.Y)))
            .Intersect(clip.Bounds);
        if (MathF.Abs(area) < Tiny || bounds.IsEmpty)
        {
            return;
        }

        for (var y = bounds.Top; y < bounds.Bottom; y++)
        {
            var row = target.Row(y);
            var mask = clip.HasMask ? clip.MaskRow(y, bounds.Left, bounds.Width) : default;
            for (var x = bounds.Left; x < bounds.Right; x++)
            {
                // Each weight is the share of the triangle's area on the far side of the pixel from a corner.
                var pixel = new Vector2(x + Half, y + Half);
                var first = Cross(b - pixel, c - pixel) / area;
                var second = Cross(c - pixel, a - pixel) / area;
                var third = 1F - first - second;
                if (first < Overlap || second < Overlap || third < Overlap)
                {
                    continue;
                }

                var color = ColorOf((values[0] * first) + (values[1] * second) + (values[Corners - 1] * third), job.Ramp);
                var coverage = (mask.IsEmpty ? byte.MaxValue : mask[x - bounds.Left]) * job.Alpha / byte.MaxValue;
                row[x] = Blend(row[x], SpanBlender.Scale(color, coverage), job.Blend);
            }
        }
    }

    /// <summary>Turns the value mixed at a pixel into a premultiplied colour.</summary>
    /// <param name="value">The mixed colour, or the ramp position in the first component.</param>
    /// <param name="ramp">The colour ramp, or <see langword="null"/>.</param>
    /// <returns>The pixel.</returns>
    private static uint ColorOf(Vector4 value, ManagedRenderShader? ramp)
    {
        if (ramp is not null)
        {
            var table = ramp.Ramp;
            return table.Length == 0 ? 0 : table[(int)(Math.Clamp(value.X, 0F, 1F) * (table.Length - 1))];
        }

        var bytes = Vector4.Clamp(value, Vector4.Zero, Vector4.One) * Full;
        return (uint)(bytes.Z + Half) | ((uint)(bytes.Y + Half) << GreenShift) | ((uint)(bytes.X + Half) << RedShift) | ((uint)(bytes.W + Half) << AlphaShift);
    }

    /// <summary>Puts a premultiplied colour onto a pixel.</summary>
    /// <param name="under">The pixel.</param>
    /// <param name="color">The colour, already scaled by its coverage.</param>
    /// <param name="blend">How the colour mixes with the pixel.</param>
    /// <returns>The new pixel.</returns>
    private static uint Blend(uint under, uint color, PdfBlendMode blend) =>
        blend == PdfBlendMode.Normal ? color + SpanBlender.Scale(under, byte.MaxValue - (color >> AlphaShift)) : BlendModes.Blend(blend, color, under);

    /// <summary>Gets the cross product of two vectors: twice the signed area of the triangle they span.</summary>
    /// <param name="left">The first vector.</param>
    /// <param name="right">The second vector.</param>
    /// <returns>The cross product.</returns>
    private static float Cross(Vector2 left, Vector2 right) => (left.X * right.Y) - (left.Y * right.X);
}
