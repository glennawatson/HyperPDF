// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Media.U3d;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// Paints the triangles of a U3D frame onto a Skia canvas. On a GPU-backed canvas the triangles are drawn by the graphics card:
/// runs of triangles without a texture go out as one vertex batch with a colour at each corner, and runs that share a texture go
/// out as one batch with the texture as the shader. The triangles arrive far to near, so there is no depth buffer: triangles that
/// cross each other are resolved by drawing order, and colours and texture coordinates are interpolated without perspective
/// correction. <see cref="U3dRenderer.Render"/> draws the exact frame on the CPU.
/// </summary>
public static class SkiaU3dPainter
{
    /// <summary>The value of a full 8-bit channel.</summary>
    private const float ChannelMax = 255F;

    /// <summary>The corners of a triangle.</summary>
    private const int Corners = 3;

    /// <summary>The offset of a triangle's second corner.</summary>
    private const int Second = 1;

    /// <summary>The offset of a triangle's third corner.</summary>
    private const int Third = 2;

    /// <summary>Paints a frame: clears the canvas to the background, then draws the triangles far to near.</summary>
    /// <param name="canvas">The canvas, which may be backed by a GPU.</param>
    /// <param name="frame">The triangles of the frame.</param>
    /// <exception cref="ArgumentNullException"><paramref name="canvas"/> or <paramref name="frame"/> is <see langword="null"/>.</exception>
    public static void Paint(SKCanvas canvas, U3dTriangleList frame)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(frame);
        canvas.Clear(ToColor(new(frame.Background.X, frame.Background.Y, frame.Background.Z), frame.Background.W));
        Dictionary<U3dTexture, SKImage> images = [with(ReferenceEqualityComparer.Instance)];
        try
        {
            var triangles = frame.Triangles;
            var start = 0;
            while (start < triangles.Length)
            {
                var texture = triangles[start].Texture;
                var end = start + 1;
                while (end < triangles.Length && ReferenceEquals(triangles[end].Texture, texture))
                {
                    end++;
                }

                DrawRun(canvas, triangles.AsSpan(start, end - start), texture, images);
                start = end;
            }
        }
        finally
        {
            foreach (var image in images.Values)
            {
                image.Dispose();
            }
        }
    }

    /// <summary>Converts a colour and opacity to a Skia colour.</summary>
    /// <param name="color">The red, green and blue, each from 0 to 1.</param>
    /// <param name="alpha">The alpha from 0 to 1.</param>
    /// <returns>The colour.</returns>
    private static SKColor ToColor(Vector3 color, float alpha) => new(Channel(color.X), Channel(color.Y), Channel(color.Z), Channel(alpha));

    /// <summary>Converts a channel from 0 to 1 to a byte.</summary>
    /// <param name="value">The channel.</param>
    /// <returns>The byte.</returns>
    private static byte Channel(float value) => (byte)Math.Clamp((int)MathF.Round(value * ChannelMax), 0, byte.MaxValue);

    /// <summary>Draws triangles that share a texture, or none, as one batch.</summary>
    /// <param name="canvas">The canvas.</param>
    /// <param name="run">The triangles.</param>
    /// <param name="texture">The shared texture, or <see langword="null"/>.</param>
    /// <param name="images">The images made so far, by texture.</param>
    private static void DrawRun(SKCanvas canvas, ReadOnlySpan<U3dScreenTriangle> run, U3dTexture? texture, Dictionary<U3dTexture, SKImage> images)
    {
        var batch = new Batch(new SKPoint[run.Length * Corners], new SKColor[run.Length * Corners], texture is null ? [] : new SKPoint[run.Length * Corners]);
        for (var i = 0; i < run.Length; i++)
        {
            var triangle = run[i];
            var first = i * Corners;
            Set(batch, first, triangle.A, triangle.ColorA, triangle.UvA, triangle.Opacity);
            Set(batch, first + Second, triangle.B, triangle.ColorB, triangle.UvB, triangle.Opacity);
            Set(batch, first + Third, triangle.C, triangle.ColorC, triangle.UvC, triangle.Opacity);
        }

        // Skia multiplies the vertex colours by the paint (or its shader); a white paint leaves the vertex colours as they are.
        using var paint = new SKPaint { IsAntialias = false, Color = SKColors.White };
        if (texture is null)
        {
            using var plain = SKVertices.CreateCopy(SKVertexMode.Triangles, batch.Positions, batch.Colors);
            canvas.DrawVertices(plain, SKBlendMode.Modulate, paint);
            return;
        }

        Scale(batch.Coordinates, texture);
        using var textured = SKVertices.CreateCopy(SKVertexMode.Triangles, batch.Positions, batch.Coordinates, batch.Colors);
        paint.Shader = SKShader.CreateImage(ImageOf(texture, images), SKShaderTileMode.Repeat, SKShaderTileMode.Repeat);
        canvas.DrawVertices(textured, SKBlendMode.Modulate, paint);
    }

    /// <summary>Writes one corner of a triangle into the batch arrays.</summary>
    /// <param name="batch">The batch.</param>
    /// <param name="index">The index of the corner.</param>
    /// <param name="position">The corner's position in pixels.</param>
    /// <param name="color">The corner's lit colour.</param>
    /// <param name="uv">The corner's texture coordinate.</param>
    /// <param name="opacity">The triangle's opacity.</param>
    private static void Set(Batch batch, int index, Vector2 position, Vector3 color, Vector2 uv, float opacity)
    {
        batch.Positions[index] = new(position.X, position.Y);
        batch.Colors[index] = ToColor(color, opacity);
        if (batch.Coordinates.Length > 0)
        {
            batch.Coordinates[index] = new(uv.X, uv.Y);
        }
    }

    /// <summary>Turns texture coordinates from the range 0 to 1 into pixels of the texture.</summary>
    /// <param name="coordinates">The coordinates, changed in place.</param>
    /// <param name="texture">The texture.</param>
    private static void Scale(SKPoint[] coordinates, U3dTexture texture)
    {
        for (var i = 0; i < coordinates.Length; i++)
        {
            coordinates[i] = new(coordinates[i].X * texture.Width, coordinates[i].Y * texture.Height);
        }
    }

    /// <summary>Gets the Skia image of a texture, making it the first time.</summary>
    /// <param name="texture">The texture.</param>
    /// <param name="images">The images made so far.</param>
    /// <returns>The image.</returns>
    private static SKImage ImageOf(U3dTexture texture, Dictionary<U3dTexture, SKImage> images)
    {
        ref var image = ref CollectionsMarshal.GetValueRefOrAddDefault(images, texture, out _);
        image ??= SKImage.FromPixelCopy(new(texture.Width, texture.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul), texture.Rgba);
        return image;
    }

    /// <summary>The arrays of one batch of triangles.</summary>
    /// <param name="Positions">The corner positions.</param>
    /// <param name="Colors">The corner colours.</param>
    /// <param name="Coordinates">The corner texture coordinates, or none for a batch without a texture.</param>
    private sealed record Batch(SKPoint[] Positions, SKColor[] Colors, SKPoint[] Coordinates);
}
