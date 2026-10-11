// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>Owns one thread's reusable pixel wrappers and grayscale paint.</summary>
internal sealed class RenderSurface : IDisposable
{
    /// <summary>The weight of red in luminance.</summary>
    private const float LumaRed = 0.299F;

    /// <summary>The weight of green in luminance.</summary>
    private const float LumaGreen = 0.587F;

    /// <summary>The weight of blue in luminance.</summary>
    private const float LumaBlue = 0.114F;

    /// <summary>The surface for the current thread.</summary>
    [ThreadStatic]
    private static RenderSurface? _current;

    /// <summary>Initializes a new instance of the <see cref="RenderSurface"/> class.</summary>
    internal RenderSurface()
    {
        Bitmap = new();
        Pixmap = new();
        Canvas = new(Bitmap);
        GrayPaint = CreateGrayPaint();
    }

    /// <summary>Gets the current thread's surface holder.</summary>
    internal static RenderSurface Current => _current ??= new();

    /// <summary>Gets the bitmap that borrows the pinned target.</summary>
    internal SKBitmap Bitmap { get; }

    /// <summary>Gets the reusable pixel description.</summary>
    internal SKPixmap Pixmap { get; }

    /// <summary>Gets the reusable canvas wrapper.</summary>
    internal BorrowedPixelCanvas Canvas { get; }

    /// <summary>Gets the paint that renders a layer in grayscale.</summary>
    internal SKPaint GrayPaint { get; }

    /// <summary>Gets or sets a value indicating whether the holder has been disposed.</summary>
    internal bool IsDisposed { get; set; }

    /// <summary>Creates a grayscale paint without allocating caller-buffer drawing wrappers.</summary>
    /// <returns>The owned paint.</returns>
    internal static SKPaint CreateGrayPaint()
    {
        float[] gray =
        [
            LumaRed, LumaGreen, LumaBlue, 0, 0,
            LumaRed, LumaGreen, LumaBlue, 0, 0,
            LumaRed, LumaGreen, LumaBlue, 0, 0,
            0, 0, 0, 1, 0,
        ];
        return new() { ColorFilter = SKColorFilter.CreateColorMatrix(gray) };
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    void IDisposable.Dispose() => BorrowedPixelDrawing.Close(this);
}
