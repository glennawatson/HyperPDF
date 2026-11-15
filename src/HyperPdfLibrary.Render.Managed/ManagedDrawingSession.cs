// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Render.Managed.Images;
using HyperPdfLibrary.Render.Managed.Pictures;
using HyperPdfLibrary.Render.Managed.Raster;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Render.Managed;

/// <summary>Draws recorded pictures into tiles. One session belongs to one thread, and reuses its canvas from tile to tile.</summary>
[DebuggerDisplay("ManagedDrawingSession")]
internal sealed class ManagedDrawingSession : IPdfDrawingSession, IDisposable
{
    /// <summary>An opaque white pixel, the paper under a page.</summary>
    private const uint White = 0xFFFFFFFF;

    /// <summary>The alpha of an opaque pixel, in its place in the pixel.</summary>
    private const uint Opaque = 0xFF000000;

    /// <summary>The factor that copies a gray byte into the blue, green and red bytes of a pixel.</summary>
    private const uint GrayToColor = 0x00010101;

    /// <summary>The weight of red in a gray made from a colour, out of 256.</summary>
    private const uint RedWeight = 54;

    /// <summary>The weight of green in a gray made from a colour, out of 256.</summary>
    private const uint GreenWeight = 183;

    /// <summary>The weight of blue in a gray made from a colour, out of 256.</summary>
    private const uint BlueWeight = 19;

    /// <summary>The shift of the red byte of a pixel.</summary>
    private const int RedShift = 16;

    /// <summary>The bits of one byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The mask of one byte.</summary>
    private const uint ByteMask = 0xFF;

    /// <summary>The canvas, reused for every tile.</summary>
    private readonly RasterCanvas _canvas = new();

    /// <summary>The layer that stands for the tile's pixels while a tile is drawn.</summary>
    private readonly RasterLayer _tile = new();

    /// <inheritdoc/>
    public unsafe bool DrawPage(IPdfRenderPicture content, IPdfRenderPicture? annotations, Matrix3x2 matrix, PdfTileTarget target, bool grayscale)
    {
        if (!target.IsValid || content is not ManagedPicture page)
        {
            return false;
        }

        fixed (byte* pixels = target.Pixels)
        {
            _tile.Borrow(pixels, target.Width, target.Height, target.Stride);
            try
            {
                _tile.Fill(White);
                _canvas.Begin(_tile);
                page.Draw(_canvas, matrix);
                (annotations as ManagedPicture)?.Draw(_canvas, matrix);
                if (grayscale)
                {
                    ToGray(_tile);
                }
            }
            finally
            {
                _tile.Release();
            }
        }

        return true;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool DrawPage(IPdfRenderPicture content, IPdfRenderPicture? annotations, Matrix3x2 matrix, IPdfRenderTarget target, bool grayscale) => false;

    /// <inheritdoc/>
    public unsafe bool DrawImage(IPdfRenderImage image, PdfTileTarget target)
    {
        if (!target.IsValid || image is not ManagedRenderImage managed)
        {
            return false;
        }

        fixed (byte* pixels = target.Pixels)
        {
            _tile.Borrow(pixels, target.Width, target.Height, target.Stride);
            try
            {
                _tile.Fill(White);
                _canvas.Begin(_tile);

                // The image fills the tile, with its first line at the top.
                var toTile = new Matrix3x2(target.Width, 0, 0, -target.Height, 0, target.Height);
                ImageDrawing.Draw(_canvas, managed, toTile, new(PictureOpKind.Image, toTile) { Alpha = 1F, Flag = true, Color = Opaque });
            }
            finally
            {
                _tile.Release();
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _canvas.Dispose();
        _tile.Dispose();
    }

    /// <summary>Turns every pixel of a layer into the gray of the same brightness.</summary>
    /// <param name="layer">The layer.</param>
    private static void ToGray(RasterLayer layer)
    {
        for (var y = 0; y < layer.Height; y++)
        {
            foreach (ref var pixel in layer.Row(y))
            {
                var gray = (((pixel >> RedShift) & ByteMask) * RedWeight) + (((pixel >> ByteBits) & ByteMask) * GreenWeight) + ((pixel & ByteMask) * BlueWeight);
                pixel = (pixel & Opaque) | ((gray >> ByteBits) * GrayToColor);
            }
        }
    }
}
