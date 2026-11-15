// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Render.Managed.Raster;

namespace HyperPdfLibrary.Render.Managed.Pictures;

/// <summary>
/// Paints with a tiling pattern. One period of the pattern is drawn once into a small layer at the size it has on the device, and
/// each pixel then reads the layer at its place within the period. The layer is kept while the same pattern is used at the same size.
/// </summary>
[DebuggerDisplay("TileShader: {_cell.Width}x{_cell.Height}")]
internal sealed class TileShader : IDisposable
{
    /// <summary>The largest byte, which stands for one.</summary>
    private const uint Full = 255;

    /// <summary>The centre of a pixel.</summary>
    private const float Half = 0.5F;

    /// <summary>The largest side of the layer one period is drawn into.</summary>
    private const int MaxCellPixels = 2048;

    /// <summary>The smallest period that is drawn, in the pattern's units.</summary>
    private const float Tiny = 1E-6F;

    /// <summary>One period of the pattern.</summary>
    private readonly RasterLayer _cell = new();

    /// <summary>The canvas the period is drawn with, made the first time a pattern is used.</summary>
    private RasterCanvas? _canvas;

    /// <summary>The pattern the layer holds.</summary>
    private ManagedPicture? _drawn;

    /// <summary>The period of the pattern, in the pattern's space.</summary>
    private PdfRect _tile;

    /// <summary>The matrix from device pixels to the pattern's space.</summary>
    private Matrix3x2 _toPattern;

    /// <summary>How opaque the paint is, where 255 is fully.</summary>
    private uint _alpha;

    /// <inheritdoc/>
    public void Dispose()
    {
        _cell.Dispose();
        _canvas?.Dispose();
        _drawn = null;
    }

    /// <summary>Sets what the shader paints, drawing one period of the pattern when the layer does not hold it already.</summary>
    /// <param name="picture">The pattern's period, with the copies of the cell that reach into it.</param>
    /// <param name="tile">The period, in the pattern's space.</param>
    /// <param name="toDevice">The matrix from the pattern's space to device pixels.</param>
    /// <param name="alpha">How opaque the paint is, where 255 is fully.</param>
    /// <returns><see langword="false"/> when the pattern has no size or its matrix cannot be inverted, so nothing is painted.</returns>
    internal bool Set(ManagedPicture picture, PdfRect tile, Matrix3x2 toDevice, uint alpha)
    {
        var width = MathF.Abs(tile.Right - tile.Left);
        var height = MathF.Abs(tile.Bottom - tile.Top);
        if (width < Tiny || height < Tiny || !Matrix3x2.Invert(toDevice, out _toPattern))
        {
            return false;
        }

        // The layer has as many pixels as one period covers on the device, so the pattern is as sharp as the rest of the page.
        var across = PixelsOf(Vector2.TransformNormal(new(width, 0), toDevice).Length());
        var down = PixelsOf(Vector2.TransformNormal(new(0, height), toDevice).Length());
        _alpha = alpha;
        if (ReferenceEquals(_drawn, picture) && _tile == tile && _cell.Width == across && _cell.Height == down)
        {
            return true;
        }

        _tile = tile;
        _drawn = picture;
        _cell.Resize(across, down);
        _cell.Fill(0);
        var canvas = _canvas ??= new();
        canvas.Begin(_cell);
        picture.Draw(canvas, Matrix3x2.CreateTranslation(-MathF.Min(tile.Left, tile.Right), -MathF.Min(tile.Top, tile.Bottom)) * Matrix3x2.CreateScale(across / width, down / height));
        return true;
    }

    /// <summary>Works out the colours of a run of pixels on one line.</summary>
    /// <param name="x">The first pixel of the run, in device pixels.</param>
    /// <param name="y">The line, in device pixels.</param>
    /// <param name="output">Receives the premultiplied colour of each pixel of the run.</param>
    internal void Shade(int x, int y, Span<uint> output)
    {
        var width = MathF.Abs(_tile.Right - _tile.Left);
        var height = MathF.Abs(_tile.Bottom - _tile.Top);
        var origin = new Vector2(MathF.Min(_tile.Left, _tile.Right), MathF.Min(_tile.Top, _tile.Bottom));
        var position = Vector2.Transform(new(x + Half, y + Half), _toPattern) - origin;
        var step = new Vector2(_toPattern.M11, _toPattern.M12);
        for (var i = 0; i < output.Length; i++)
        {
            // The place within the period, as a fraction, picks the pixel of the layer.
            var across = position.X / width;
            var down = position.Y / height;
            var column = Math.Min((int)((across - MathF.Floor(across)) * _cell.Width), _cell.Width - 1);
            var line = Math.Min((int)((down - MathF.Floor(down)) * _cell.Height), _cell.Height - 1);
            var pixel = _cell.Row(line)[column];
            output[i] = _alpha == Full ? pixel : SpanBlender.Scale(pixel, _alpha);
            position += step;
        }
    }

    /// <summary>Turns a length on the device into a number of pixels for the layer.</summary>
    /// <param name="length">The length in device pixels.</param>
    /// <returns>The pixels, at least one and no more than the layer may have.</returns>
    private static int PixelsOf(float length) => float.IsFinite(length) ? Math.Clamp((int)MathF.Ceiling(length), 1, MaxCellPixels) : 1;
}
