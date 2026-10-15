// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Objects;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// Turns a tiling pattern's recorded cell into one period of the pattern. Copies of the cell, each clipped to /BBox, are
/// placed at every multiple of the steps that reaches the period, so a cell larger than its step overlaps its neighbours
/// and a smaller one leaves gaps (ISO 32000-2 8.7.3.1). Small periods are rasterised at a bounded resolution, so the
/// result does not depend on how Skia's picture shader picks its tile resolution; large ones stay a picture.
/// </summary>
internal sealed class TileComposer
{
    /// <summary>How many device pixels per page unit a rasterised period gets: enough to stay sharp up to 400% zoom.</summary>
    private const float Oversample = 4;

    /// <summary>The most pixels along each side of a rasterised period.</summary>
    private const int MaxPixels = 1024;

    /// <summary>The fewest pixels along each side: two device pixels at the oversampling, so a tiny period still holds its detail before it is scaled down.</summary>
    private const int MinPixels = 8;

    /// <summary>The most cell copies along each direction that can reach one period.</summary>
    private const int MaxCopies = 64;

    /// <summary>The recording wrapper reused for large periods on this backend thread.</summary>
    private SKPictureRecorder? _recorder;

    /// <summary>Composes one period of a tiling pattern.</summary>
    /// <param name="cell">The cell's content in pattern space.</param>
    /// <param name="box">The pattern's /BBox.</param>
    /// <param name="stepX">The horizontal step, not zero.</param>
    /// <param name="stepY">The vertical step, not zero.</param>
    /// <param name="scale">The page units one pattern unit spans where the pattern is used.</param>
    /// <returns>The period; the caller owns it.</returns>
    internal PatternCell Compose(SKPicture cell, PdfRectangle box, float stepX, float stepY, float scale)
    {
        var tile = SKRect.Create(box.Left, box.Bottom, Math.Abs(stepX), Math.Abs(stepY));
        var pixelScale = Math.Max(scale, 0) * Oversample;
        var width = tile.Width * pixelScale;
        var height = tile.Height * pixelScale;
        if (!float.IsFinite(width) || !float.IsFinite(height) || width > MaxPixels || height > MaxPixels)
        {
            return new(new SkiaRenderPicture(Record(cell, box, tile)), null, SkiaConversions.ToPdfRect(tile), Matrix3x2.Identity);
        }

        var pixelWidth = Math.Max(MinPixels, (int)MathF.Ceiling(width));
        var pixelHeight = Math.Max(MinPixels, (int)MathF.Ceiling(height));
        using var surface = SKSurface.Create(new SKImageInfo(pixelWidth, pixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(pixelWidth / tile.Width, pixelHeight / tile.Height);
        canvas.Translate(-tile.Left, -tile.Top);
        DrawCopies(canvas, cell, box, tile);
        var image = surface.Snapshot();
        var toPattern = Matrix3x2.CreateScale(tile.Width / pixelWidth, tile.Height / pixelHeight) * Matrix3x2.CreateTranslation(tile.Left, tile.Top);
        return new(null, new SkiaRenderImage(image), SkiaConversions.ToPdfRect(tile), toPattern);
    }

    /// <summary>Draws every copy of the cell that reaches the period, clipped to the period.</summary>
    /// <param name="canvas">The canvas, in pattern space.</param>
    /// <param name="cell">The cell.</param>
    /// <param name="box">The pattern's /BBox.</param>
    /// <param name="tile">The period's rectangle.</param>
    private static void DrawCopies(SKCanvas canvas, SKPicture cell, PdfRectangle box, SKRect tile)
    {
        var bounds = new SKRect(box.Left, box.Bottom, box.Right, box.Top);
        canvas.ClipRect(tile);
        var firstColumn = CopyIndex(tile.Left - bounds.Right, tile.Width, false);
        var lastColumn = CopyIndex(tile.Right - bounds.Left, tile.Width, true);
        var firstRow = CopyIndex(tile.Top - bounds.Bottom, tile.Height, false);
        var lastRow = CopyIndex(tile.Bottom - bounds.Top, tile.Height, true);
        for (var column = firstColumn; column <= lastColumn; column++)
        {
            for (var row = firstRow; row <= lastRow; row++)
            {
                _ = canvas.Save();
                canvas.Translate(column * tile.Width, row * tile.Height);
                canvas.ClipRect(bounds);
                canvas.DrawPicture(cell);
                canvas.Restore();
            }
        }
    }

    /// <summary>Gets the first or last copy index along a direction, bounded so a huge box cannot run away.</summary>
    /// <param name="distance">The distance from the box edge to the period edge.</param>
    /// <param name="step">The step.</param>
    /// <param name="last">Whether to round up for the last index rather than down for the first.</param>
    /// <returns>The index.</returns>
    private static int CopyIndex(float distance, float step, bool last)
    {
        var index = distance / step;
        var rounded = last ? MathF.Ceiling(index) : MathF.Floor(index);
        return float.IsFinite(rounded) ? (int)Math.Clamp(rounded, -MaxCopies, MaxCopies) : 0;
    }

    /// <summary>Records one period as a picture.</summary>
    /// <param name="cell">The cell.</param>
    /// <param name="box">The pattern's /BBox.</param>
    /// <param name="tile">The period's rectangle.</param>
    /// <returns>The picture.</returns>
    private SKPicture Record(SKPicture cell, PdfRectangle box, SKRect tile)
    {
        var recorder = _recorder ??= new();
        var canvas = recorder.BeginRecording(tile);
        DrawCopies(canvas, cell, box, tile);
        return recorder.EndRecording();
    }
}
