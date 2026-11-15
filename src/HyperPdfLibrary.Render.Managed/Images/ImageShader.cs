// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Render.Managed.Raster;

namespace HyperPdfLibrary.Render.Managed.Images;

/// <summary>
/// Paints with an image under any matrix: for each device pixel it finds the image pixel under the pixel's centre. One instance is
/// reused for every image a canvas draws.
/// </summary>
[DebuggerDisplay("ImageShader: smooth {_smooth}")]
internal sealed class ImageShader
{
    /// <summary>The largest byte, which stands for one.</summary>
    private const uint Full = 255;

    /// <summary>The centre of a pixel.</summary>
    private const float Half = 0.5F;

    /// <summary>The steps of a weight between two image pixels.</summary>
    private const float WeightSteps = 256F;

    /// <summary>The largest weight.</summary>
    private const uint WeightOne = 256;

    /// <summary>The mask of the blue and red bytes of a pixel, which are mixed together.</summary>
    private const uint EvenBytes = 0x00FF00FF;

    /// <summary>The bits of one byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The image.</summary>
    private ManagedRenderImage? _image;

    /// <summary>The matrix from device pixels to image pixels.</summary>
    private Matrix3x2 _toImage;

    /// <summary>The premultiplied colour a stencil mask paints with.</summary>
    private uint _tint;

    /// <summary>How opaque a colour image is, where 255 is fully.</summary>
    private uint _alpha;

    /// <summary>Whether neighbouring image pixels are mixed.</summary>
    private bool _smooth;

    /// <summary>Sets what the shader paints.</summary>
    /// <param name="image">The image.</param>
    /// <param name="toImage">The matrix from device pixels to image pixels.</param>
    /// <param name="tint">The premultiplied colour a stencil mask paints with.</param>
    /// <param name="alpha">How opaque a colour image is, where 255 is fully.</param>
    /// <param name="smooth">Whether neighbouring image pixels are mixed.</param>
    internal void Set(ManagedRenderImage image, Matrix3x2 toImage, uint tint, uint alpha, bool smooth)
    {
        _image = image;
        _toImage = toImage;
        _tint = tint;
        _alpha = alpha;
        _smooth = smooth;
    }

    /// <summary>Works out the colours of a run of pixels on one line.</summary>
    /// <param name="x">The first pixel of the run, in device pixels.</param>
    /// <param name="y">The line, in device pixels.</param>
    /// <param name="output">Receives the premultiplied colour of each pixel of the run.</param>
    internal void Shade(int x, int y, Span<uint> output)
    {
        if (_image is not { } image)
        {
            output.Clear();
            return;
        }

        // The image position moves by the same step from one device pixel to the next along a line.
        var position = Vector2.Transform(new(x + Half, y + Half), _toImage);
        var step = new Vector2(_toImage.M11, _toImage.M12);
        for (var i = 0; i < output.Length; i++)
        {
            var pixel = _smooth ? Mixed(image, position) : Nearest(image, position);
            output[i] = Finish(image, pixel);
            position += step;
        }
    }

    /// <summary>Reads the image pixel a position falls in.</summary>
    /// <param name="image">The image.</param>
    /// <param name="position">The position in image pixels.</param>
    /// <returns>The pixel.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Nearest(ManagedRenderImage image, Vector2 position) =>
        image.Read(Math.Clamp((int)MathF.Floor(position.X), 0, image.Width - 1), Math.Clamp((int)MathF.Floor(position.Y), 0, image.Height - 1));

    /// <summary>Mixes the four image pixels around a position.</summary>
    /// <param name="image">The image.</param>
    /// <param name="position">The position in image pixels.</param>
    /// <returns>The mixed pixel.</returns>
    private static uint Mixed(ManagedRenderImage image, Vector2 position)
    {
        // Pixel centres are at half positions, so the four neighbours are found half a pixel back.
        var u = position.X - Half;
        var v = position.Y - Half;
        var left = MathF.Floor(u);
        var top = MathF.Floor(v);
        var x0 = Math.Clamp((int)left, 0, image.Width - 1);
        var y0 = Math.Clamp((int)top, 0, image.Height - 1);
        var x1 = Math.Min(x0 + 1, image.Width - 1);
        var y1 = Math.Min(y0 + 1, image.Height - 1);
        var across = (uint)Math.Clamp((u - left) * WeightSteps, 0F, WeightSteps);
        var down = (uint)Math.Clamp((v - top) * WeightSteps, 0F, WeightSteps);
        var upper = Lerp(image.Read(x0, y0), image.Read(x1, y0), across);
        var lower = Lerp(image.Read(x0, y1), image.Read(x1, y1), across);
        return Lerp(upper, lower, down);
    }

    /// <summary>Mixes two pixels.</summary>
    /// <param name="from">The pixel at weight 0.</param>
    /// <param name="to">The pixel at weight 256.</param>
    /// <param name="weight">The weight of the second pixel, 0 to 256.</param>
    /// <returns>The mix.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Lerp(uint from, uint to, uint weight)
    {
        var keep = WeightOne - weight;
        var even = ((((from & EvenBytes) * keep) + ((to & EvenBytes) * weight)) >> ByteBits) & EvenBytes;
        var odd = ((((from >> ByteBits) & EvenBytes) * keep) + (((to >> ByteBits) & EvenBytes) * weight)) & ~EvenBytes;
        return even | odd;
    }

    /// <summary>Turns an image pixel into the colour painted: a stencil mask paints the tint, and a colour image is faded by its alpha.</summary>
    /// <param name="image">The image.</param>
    /// <param name="pixel">The image pixel.</param>
    /// <returns>The premultiplied colour.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private uint Finish(ManagedRenderImage image, uint pixel)
    {
        if (image.Kind == ImagePixelKind.Coverage)
        {
            return SpanBlender.Scale(_tint, pixel >> (ByteBits + ByteBits + ByteBits));
        }

        return _alpha == Full ? pixel : SpanBlender.Scale(pixel, _alpha);
    }
}
