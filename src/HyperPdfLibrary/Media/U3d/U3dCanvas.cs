// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdfLibrary.Media.U3d;

/// <summary>The pixels and depth of a frame being drawn, and the triangle filling that writes them.</summary>
internal sealed class U3dCanvas : IU3dTriangleTarget, IDisposable
{
    /// <summary>The bytes of a pixel.</summary>
    private const int PixelBytes = 4;

    /// <summary>The value of a full 8-bit channel.</summary>
    private const float ChannelMax = 255F;

    /// <summary>The offset of the second channel.</summary>
    private const int SecondChannel = 1;

    /// <summary>The offset of the third channel.</summary>
    private const int ThirdChannel = 2;

    /// <summary>The offset of the fourth channel.</summary>
    private const int FourthChannel = 3;

    /// <summary>The farthest depth a pixel may have; the clip volume ends there.</summary>
    private const float FarDepth = 1;

    /// <summary>One half, the middle of the screen in normalised coordinates and of a pixel.</summary>
    private const float Half = 0.5F;

    /// <summary>The depth of each pixel.</summary>
    private readonly float[] _depth;

    /// <summary>Initializes a new instance of the <see cref="U3dCanvas"/> class.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    /// <param name="background">The red, green, blue and alpha every pixel starts as, each from 0 to 1.</param>
    internal U3dCanvas(int width, int height, Vector4 background)
    {
        Width = width;
        Height = height;
        Pixels = new byte[checked(width * height * PixelBytes)];
        _depth = ArrayPool<float>.Shared.Rent(width * height);
        _depth.AsSpan(0, width * height).Fill(float.PositiveInfinity);

        // One pixel is written to a small buffer and then repeated by a vectorised fill; reading the buffer back as a native
        // integer and writing the integer out keeps the byte order of the pixel whatever the machine's endianness.
        Span<byte> pixel = stackalloc byte[PixelBytes];
        pixel[0] = Channel(background.X);
        pixel[SecondChannel] = Channel(background.Y);
        pixel[ThirdChannel] = Channel(background.Z);
        pixel[FourthChannel] = Channel(background.W);
        MemoryMarshal.Cast<byte, uint>(Pixels.AsSpan()).Fill(MemoryMarshal.Read<uint>(pixel));
    }

    /// <summary>Gets the width in pixels.</summary>
    internal int Width { get; }

    /// <summary>Gets the height in pixels.</summary>
    internal int Height { get; }

    /// <summary>Gets the pixels, four bytes each in red, green, blue, alpha order.</summary>
    internal byte[] Pixels { get; }

    /// <summary>Gets the number of triangles drawn, which is the number that covered at least one pixel centre.</summary>
    internal int Drawn { get; private set; }

    /// <summary>Returns the depth buffer to the pool; the pixels stay valid.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => ArrayPool<float>.Shared.Return(_depth);

    /// <summary>Draws a triangle that has been clipped to the near plane.</summary>
    /// <param name="a">The first corner.</param>
    /// <param name="b">The second corner.</param>
    /// <param name="c">The third corner.</param>
    /// <param name="style">How to colour the triangle.</param>
    /// <param name="visibility">Which faces to draw: 1 front, 2 back, 3 both.</param>
    void IU3dTriangleTarget.Fill(in U3dRasterVertex a, in U3dRasterVertex b, in U3dRasterVertex c, U3dSurfaceStyle style, uint visibility)
    {
        var screenA = U3dScreenSpace.Project(a.Clip, Width, Height);
        var screenB = U3dScreenSpace.Project(b.Clip, Width, Height);
        var screenC = U3dScreenSpace.Project(c.Clip, Width, Height);
        var area = U3dScreenSpace.Edge(screenA, screenB, screenC);
        if (!U3dScreenSpace.IsVisible(area, visibility))
        {
            return;
        }

        var screen = new ScreenTriangle(screenA, screenB, screenC, area);
        var planes = new Planes(Plane.Of(screenB, screenC, area), Plane.Of(screenC, screenA, area), Plane.Of(screenA, screenB, area));
        var minX = Math.Max(0, (int)MathF.Floor(MathF.Min(screenA.X, MathF.Min(screenB.X, screenC.X))));
        var maxX = Math.Min(Width - 1, (int)MathF.Ceiling(MathF.Max(screenA.X, MathF.Max(screenB.X, screenC.X))));
        var minY = Math.Max(0, (int)MathF.Floor(MathF.Min(screenA.Y, MathF.Min(screenB.Y, screenC.Y))));
        var maxY = Math.Min(Height - 1, (int)MathF.Ceiling(MathF.Max(screenA.Y, MathF.Max(screenB.Y, screenC.Y))));
        var corners = new Corners(a, b, c, style, new(screenA.W, screenB.W, screenC.W));
        var span = new Columns(minX, maxX);
        var covered = false;
        for (var y = minY; y <= maxY; y++)
        {
            covered |= Row(y, in span, in planes, in screen, in corners);
        }

        Drawn += covered ? 1 : 0;
    }

    /// <summary>Converts a channel from 0 to 1 to a byte.</summary>
    /// <param name="value">The channel.</param>
    /// <returns>The byte.</returns>
    private static byte Channel(float value) => (byte)Math.Clamp((int)MathF.Round(value * ChannelMax), 0, byte.MaxValue);

    /// <summary>Reads the nearest texel of a texture, repeating it outside the range 0 to 1.</summary>
    /// <param name="texture">The texture.</param>
    /// <param name="uv">The texture coordinate, with V running from the top of the picture.</param>
    /// <returns>The red, green, blue and alpha of the texel, each from 0 to 1.</returns>
    private static Vector4 Sample(U3dTexture texture, Vector2 uv)
    {
        var u = uv.X - MathF.Floor(uv.X);
        var v = uv.Y - MathF.Floor(uv.Y);
        var x = Math.Min(texture.Width - 1, (int)(u * texture.Width));
        var y = Math.Min(texture.Height - 1, (int)(v * texture.Height));
        var at = ((y * texture.Width) + x) * PixelBytes;
        return new Vector4(texture.Rgba[at], texture.Rgba[at + SecondChannel], texture.Rgba[at + ThirdChannel], texture.Rgba[at + FourthChannel]) / ChannelMax;
    }

    /// <summary>Shades one row of a triangle: whole vectors of pixels first, then the pixels left over.</summary>
    /// <param name="y">The row.</param>
    /// <param name="span">The first and last column the triangle's box covers.</param>
    /// <param name="planes">The weight of each corner as a plane over the screen.</param>
    /// <param name="screen">The triangle's screen corners and area.</param>
    /// <param name="corners">The triangle's corners and style.</param>
    /// <returns><see langword="true"/> when a pixel centre in the row is inside the triangle.</returns>
    private bool Row(int y, in Columns span, in Planes planes, in ScreenTriangle screen, in Corners corners)
    {
        var covered = false;
        var x = span.First;
        for (; x + Vector<float>.Count - 1 <= span.Last; x += Vector<float>.Count)
        {
            covered |= Block(x, y, in planes, in screen, in corners);
        }

        for (; x <= span.Last; x++)
        {
            var py = y + Half;
            var px = x + Half;
            covered |= Pixel((y * Width) + x, new(planes.A.At(px, py), planes.B.At(px, py), planes.C.At(px, py)), in screen, in corners);
        }

        return covered;
    }

    /// <summary>Shades a vector of neighbouring pixels at once: the three weights, the inside test and the depth test are done for every lane, and only lanes that pass are coloured.</summary>
    /// <param name="x">The first column of the block.</param>
    /// <param name="y">The row.</param>
    /// <param name="planes">The weight of each corner as a plane over the screen.</param>
    /// <param name="screen">The triangle's screen corners and area.</param>
    /// <param name="corners">The triangle's corners and style.</param>
    /// <returns><see langword="true"/> when a pixel centre in the block is inside the triangle.</returns>
    private bool Block(int x, int y, in Planes planes, in ScreenTriangle screen, in Corners corners)
    {
        var xs = new Vector<float>(x + Half) + Vector<float>.Indices;
        var py = y + Half;
        var wa = new Vector<float>(planes.A.At(0, py)) + (xs * planes.A.Dx);
        var wb = new Vector<float>(planes.B.At(0, py)) + (xs * planes.B.Dx);
        var wc = new Vector<float>(planes.C.At(0, py)) + (xs * planes.C.Dx);
        var zero = Vector<float>.Zero;
        var inside = Vector.GreaterThanOrEqual(wa, zero) & Vector.GreaterThanOrEqual(wb, zero) & Vector.GreaterThanOrEqual(wc, zero);
        if (inside == Vector<int>.Zero)
        {
            return false;
        }

        var index = (y * Width) + x;
        var depth = (wa * screen.A.Z) + (wb * screen.B.Z) + (wc * screen.C.Z);
        Vector<float> far = new(FarDepth);
        Vector<float> nearest = new(_depth, index);
        var passes = inside & Vector.GreaterThanOrEqual(depth, zero) & Vector.LessThanOrEqual(depth, far) & Vector.LessThan(depth, nearest);
        for (var lane = 0; passes != Vector<int>.Zero && lane < Vector<float>.Count; lane++)
        {
            if (passes[lane] != 0)
            {
                Write(index + lane, new(wa[lane], wb[lane], wc[lane]), depth[lane], in corners);
            }
        }

        return true;
    }

    /// <summary>Shades one pixel on its own.</summary>
    /// <param name="index">The pixel's index.</param>
    /// <param name="weights">The weights of the three corners at the pixel's centre.</param>
    /// <param name="screen">The triangle's screen corners and area.</param>
    /// <param name="corners">The triangle's corners and style.</param>
    /// <returns><see langword="true"/> when the pixel's centre is inside the triangle.</returns>
    private bool Pixel(int index, Vector3 weights, in ScreenTriangle screen, in Corners corners)
    {
        if (weights.X < 0 || weights.Y < 0 || weights.Z < 0)
        {
            return false;
        }

        var depth = (weights.X * screen.A.Z) + (weights.Y * screen.B.Z) + (weights.Z * screen.C.Z);
        if (depth >= 0 && depth <= FarDepth && depth < _depth[index])
        {
            Write(index, weights, depth, in corners);
        }

        return true;
    }

    /// <summary>Colours a pixel that passed the inside and depth tests, with perspective-correct colour and texture coordinate.</summary>
    /// <param name="index">The pixel's index.</param>
    /// <param name="weights">The weights of the three corners.</param>
    /// <param name="depth">The depth.</param>
    /// <param name="corners">The triangle's corners and style.</param>
    private void Write(int index, Vector3 weights, float depth, in Corners corners)
    {
        var pa = weights.X * corners.InverseW.X;
        var pb = weights.Y * corners.InverseW.Y;
        var pc = weights.Z * corners.InverseW.Z;
        var norm = 1 / (pa + pb + pc);
        var color = ((corners.A.Color * pa) + (corners.B.Color * pb) + (corners.C.Color * pc)) * norm;
        var uv = ((corners.A.Uv * pa) + (corners.B.Uv * pb) + (corners.C.Uv * pc)) * norm;
        var texel = corners.Style.Texture is { IsDecoded: true } texture ? Sample(texture, uv) : Vector4.One;
        _depth[index] = depth;
        Blend(index, color * new Vector3(texel.X, texel.Y, texel.Z), corners.Style.Opacity * texel.W);
    }

    /// <summary>Writes a colour into a pixel.</summary>
    /// <param name="pixel">The pixel's index.</param>
    /// <param name="color">The red, green and blue, each from 0 to 1.</param>
    /// <param name="alpha">The alpha from 0 to 1.</param>
    private void Put(int pixel, Vector3 color, float alpha)
    {
        var at = pixel * PixelBytes;
        Pixels[at] = Channel(color.X);
        Pixels[at + SecondChannel] = Channel(color.Y);
        Pixels[at + ThirdChannel] = Channel(color.Z);
        Pixels[at + FourthChannel] = Channel(alpha);
    }

    /// <summary>Blends a colour over the pixel already there.</summary>
    /// <param name="pixel">The pixel's index.</param>
    /// <param name="color">The red, green and blue to draw.</param>
    /// <param name="alpha">How opaque it is.</param>
    private void Blend(int pixel, Vector3 color, float alpha)
    {
        if (alpha >= 1)
        {
            Put(pixel, color, 1);
            return;
        }

        var at = pixel * PixelBytes;
        var below = new Vector3(Pixels[at], Pixels[at + SecondChannel], Pixels[at + ThirdChannel]) / ChannelMax;
        Put(pixel, Vector3.Lerp(below, color, Math.Clamp(alpha, 0, 1)), Pixels[at + FourthChannel] / ChannelMax);
    }

    /// <summary>A triangle's corners in screen coordinates, and its signed area.</summary>
    /// <param name="A">The first corner.</param>
    /// <param name="B">The second corner.</param>
    /// <param name="C">The third corner.</param>
    /// <param name="Area">Twice the signed area.</param>
    private readonly record struct ScreenTriangle(Vector4 A, Vector4 B, Vector4 C, float Area);

    /// <summary>The corners of a triangle with the style it is drawn in.</summary>
    /// <param name="A">The first corner.</param>
    /// <param name="B">The second corner.</param>
    /// <param name="C">The third corner.</param>
    /// <param name="Style">The style.</param>
    /// <param name="InverseW">The reciprocal of each corner's clip W, which weights perspective-correct interpolation.</param>
    private readonly record struct Corners(U3dRasterVertex A, U3dRasterVertex B, U3dRasterVertex C, U3dSurfaceStyle Style, Vector3 InverseW);

    /// <summary>The first and last column of a triangle's box.</summary>
    /// <param name="First">The first column.</param>
    /// <param name="Last">The last column.</param>
    private readonly record struct Columns(int First, int Last);

    /// <summary>One corner's weight as a plane over the screen: <c>Origin + Dx * x + Dy * y</c>, already divided by the triangle's area.</summary>
    /// <param name="Origin">The weight at the screen's origin.</param>
    /// <param name="Dx">How the weight changes with each column.</param>
    /// <param name="Dy">How the weight changes with each row.</param>
    private readonly record struct Plane(float Origin, float Dx, float Dy)
    {
        /// <summary>Makes the weight of the corner opposite an edge.</summary>
        /// <param name="p">The edge's first end.</param>
        /// <param name="q">The edge's second end.</param>
        /// <param name="area">Twice the triangle's signed area.</param>
        /// <returns>The plane.</returns>
        internal static Plane Of(Vector4 p, Vector4 q, float area) =>
            new((((q.Y - p.Y) * p.X) - ((q.X - p.X) * p.Y)) / area, -(q.Y - p.Y) / area, (q.X - p.X) / area);

        /// <summary>Gets the weight at a point.</summary>
        /// <param name="x">The point's column.</param>
        /// <param name="y">The point's row.</param>
        /// <returns>The weight.</returns>
        internal float At(float x, float y) => Origin + (Dx * x) + (Dy * y);
    }

    /// <summary>The weights of a triangle's three corners.</summary>
    /// <param name="A">The first corner's weight.</param>
    /// <param name="B">The second corner's weight.</param>
    /// <param name="C">The third corner's weight.</param>
    private readonly record struct Planes(Plane A, Plane B, Plane C);
}
