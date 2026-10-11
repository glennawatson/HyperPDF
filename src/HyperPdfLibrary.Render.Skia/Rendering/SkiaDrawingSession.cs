// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Drawing;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>Reuses one thread's native wrappers for drawing into pinned caller pixels.</summary>
internal sealed class SkiaDrawingSession : IPdfDrawingSession
{
    /// <summary>The opaque PDF page background without initializing Skia's named-colour table.</summary>
    private static readonly SKColor White = new(byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue);

    /// <summary>The drawing session isolated to the current thread.</summary>
    [ThreadStatic]
    private static SkiaDrawingSession? _current;

    /// <summary>The graphics thread's grayscale paint, initialized only for grayscale GPU output.</summary>
    [ThreadStatic]
    private static SKPaint? _gpuGrayPaint;

    /// <summary>The thread's reusable native wrappers.</summary>
    private RenderSurface? _surface;

    /// <summary>Gets the reusable session for the current thread.</summary>
    internal static SkiaDrawingSession Current => _current ??= new();

    /// <inheritdoc/>
    public unsafe bool DrawPage(IPdfRenderPicture content, IPdfRenderPicture? annotations, Matrix3x2 matrix, PdfTileTarget target, bool grayscale)
    {
        var surface = _surface ??= RenderSurface.Current;
        BorrowedPixelDrawing.CheckAvailable(surface);
        fixed (byte* pixels = target.Pixels)
        {
            try
            {
                if (!BorrowedPixelDrawing.Attach(surface, target, (nint)pixels))
                {
                    return false;
                }

                DrawPageContent(new(surface.Canvas, surface.GrayPaint, target.Width, target.Height), content, annotations, matrix, grayscale);
                return true;
            }
            finally
            {
                BorrowedPixelDrawing.Detach(surface);
            }
        }
    }

    /// <inheritdoc/>
    public bool DrawPage(IPdfRenderPicture content, IPdfRenderPicture? annotations, Matrix3x2 matrix, IPdfRenderTarget target, bool grayscale)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (target is not SkiaSurfaceRenderTarget skiaTarget || !skiaTarget.IsValid)
        {
            return false;
        }

        var grayPaint = grayscale ? _gpuGrayPaint ??= RenderSurface.CreateGrayPaint() : null;
        DrawPageContent(new(skiaTarget.Canvas, grayPaint, target.Width, target.Height), content, annotations, matrix, grayscale);
        return true;
    }

    /// <inheritdoc/>
    public unsafe bool DrawImage(IPdfRenderImage image, PdfTileTarget target)
    {
        var surface = _surface ??= RenderSurface.Current;
        BorrowedPixelDrawing.CheckAvailable(surface);
        fixed (byte* pixels = target.Pixels)
        {
            try
            {
                if (!BorrowedPixelDrawing.Attach(surface, target, (nint)pixels))
                {
                    return false;
                }

                DrawImageContent(surface, image, target);
                return true;
            }
            finally
            {
                BorrowedPixelDrawing.Detach(surface);
            }
        }
    }

    /// <summary>Replays recordings into the current borrowed target.</summary>
    /// <param name="destination">The target canvas and dimensions.</param>
    /// <param name="content">The content recording.</param>
    /// <param name="annotations">The optional annotation recording.</param>
    /// <param name="matrix">The page transform.</param>
    /// <param name="grayscale">Whether to convert the replay to grayscale.</param>
    private static void DrawPageContent(DrawDestination destination, IPdfRenderPicture content, IPdfRenderPicture? annotations, Matrix3x2 matrix, bool grayscale)
    {
        var (canvas, grayPaint, width, height) = destination;
        var transform = SkiaConversions.ToSkMatrix(matrix);
        var saved = canvas.Save();
        canvas.ClipRect(new(0, 0, width, height));
        canvas.Clear(White);
        if (grayscale)
        {
            ArgumentNullException.ThrowIfNull(grayPaint);
            _ = canvas.SaveLayer(grayPaint);
        }

        canvas.DrawPicture(SkiaResources.Picture(content), in transform);
        if (annotations is not null)
        {
            canvas.DrawPicture(SkiaResources.Picture(annotations), in transform);
        }

        canvas.RestoreToCount(saved);
    }

    /// <summary>Fits an image into the current borrowed target.</summary>
    /// <param name="surface">The thread's borrowed pixel wrappers.</param>
    /// <param name="image">The image.</param>
    /// <param name="target">The attached target.</param>
    private static void DrawImageContent(RenderSurface surface, IPdfRenderImage image, PdfTileTarget target)
    {
        var canvas = surface.Canvas;
        var saved = canvas.Save();
        canvas.ResetMatrix();
        canvas.ClipRect(new(0, 0, target.Width, target.Height));
        canvas.Clear(White);
        canvas.DrawImage(SkiaResources.Image(image), new SKRect(0, 0, target.Width, target.Height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
        canvas.RestoreToCount(saved);
    }

    /// <summary>Pairs a canvas with its clipping dimensions and grayscale effect.</summary>
    /// <param name="Canvas">The destination canvas.</param>
    /// <param name="GrayPaint">The grayscale paint, or null when it is not needed.</param>
    /// <param name="Width">The target width.</param>
    /// <param name="Height">The target height.</param>
    private readonly record struct DrawDestination(SKCanvas Canvas, SKPaint? GrayPaint, int Width, int Height);
}
