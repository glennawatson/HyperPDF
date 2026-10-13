// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>Attaches pinned caller pixels to a thread's reusable drawing wrappers.</summary>
internal static class BorrowedPixelDrawing
{
    /// <summary>Checks that the holder can start drawing.</summary>
    /// <param name="surface">The thread's holder.</param>
    /// <exception cref="ObjectDisposedException">The holder has been disposed.</exception>
    /// <exception cref="InvalidOperationException">The holder is already drawing.</exception>
    internal static void CheckAvailable(RenderSurface surface)
    {
        ObjectDisposedException.ThrowIf(surface.IsDisposed, surface);
        if (surface.Canvas.Handle != 0)
        {
            throw new InvalidOperationException("A caller-buffer render is already active on this thread.");
        }
    }

    /// <summary>Installs a target whose pixels remain pinned until <see cref="Detach"/> completes.</summary>
    /// <param name="surface">The available thread's holder.</param>
    /// <param name="target">The target dimensions and stride.</param>
    /// <param name="pixels">The pinned target pointer.</param>
    /// <returns>Whether the target was attached.</returns>
    internal static bool Attach(RenderSurface surface, PdfTileTarget target, nint pixels)
    {
        if (!ResetAndInstallPixels(surface, target, pixels))
        {
            return false;
        }

        surface.Canvas.PixelHandle = CreateCanvas(null!, surface.Bitmap.Handle);
        return surface.Canvas.Handle != 0;
    }

    /// <summary>Releases every borrowed pixel reference before the caller unpins its target.</summary>
    /// <param name="surface">The thread's holder.</param>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    internal static void Detach(RenderSurface surface)
    {
        try
        {
            surface.Canvas.Release();
        }
        finally
        {
            try
            {
                surface.Bitmap.Reset();
            }
            finally
            {
                try
                {
                    surface.Pixmap.Reset();
                }
                finally
                {
                    GC.KeepAlive(surface);
                }
            }
        }
    }

    /// <summary>Releases an idle holder's native resources.</summary>
    /// <param name="surface">The thread's holder.</param>
    /// <exception cref="InvalidOperationException">The holder is drawing into borrowed pixels.</exception>
    internal static void Close(RenderSurface surface)
    {
        if (surface.IsDisposed)
        {
            return;
        }

        CheckAvailable(surface);
        surface.IsDisposed = true;
        surface.Canvas.Dispose();
        surface.Bitmap.Dispose();
        surface.Pixmap.Dispose();
        surface.DisposeGrayPaint();
    }

    /// <summary>Updates the reusable pixmap and bitmap with the pinned target.</summary>
    /// <param name="surface">The thread's holder.</param>
    /// <param name="target">The target dimensions and stride.</param>
    /// <param name="pixels">The pinned target pointer.</param>
    /// <returns>Whether the bitmap accepted the pixmap.</returns>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.AggressiveOptimization)]
    private static bool ResetAndInstallPixels(RenderSurface surface, PdfTileTarget target, nint pixels)
    {
        surface.Pixmap.Reset(new(target.Width, target.Height, SKColorType.Bgra8888, SKAlphaType.Premul), pixels, target.Stride);
        return surface.Bitmap.InstallPixels(surface.Pixmap);
    }

    /// <summary>Creates a native canvas through SkiaSharp's existing internal binding.</summary>
    /// <param name="declaringType">The ignored static-method declaring type marker.</param>
    /// <param name="bitmap">The native bitmap handle.</param>
    /// <returns>The native canvas handle, or zero on failure.</returns>
    /// <remarks>
    /// SkiaSharp 4.153.1, commit 4783f51448f9b070dda4f87b83e941c9599e466e:
    /// the public SKCanvas(SKBitmap) constructor calls this internal binding but creates a new managed wrapper.
    /// Package upgrades must verify this exact signature and Native AOT resolution.
    /// </remarks>
    [UnsafeAccessor(UnsafeAccessorKind.StaticMethod, Name = "sk_canvas_new_from_bitmap")]
    private static extern nint CreateCanvas([UnsafeAccessorType("SkiaSharp.SkiaApi, SkiaSharp")] object declaringType, nint bitmap);
}
