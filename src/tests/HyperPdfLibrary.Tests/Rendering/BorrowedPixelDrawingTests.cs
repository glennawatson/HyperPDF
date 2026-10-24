// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Rendering;
using SkiaSharp;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks borrowed pixel cleanup and the thread-confined wrapper lifetime.</summary>
public sealed class BorrowedPixelDrawingTests
{
    /// <summary>The test image edge.</summary>
    private const int Edge = 4;

    /// <summary>The row stride in bytes.</summary>
    private const int Stride = Edge * RenderedImage.BytesPerPixel;

    /// <summary>A drawing exception still releases every borrowed pointer before unpinning.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExceptionDetachesAndAllowsReuse()
    {
        using var surface = new RenderSurface();
        var first = new byte[Stride * Edge];
        await Assert.That(() => Draw(surface, first, true)).Throws<InvalidOperationException>();
        var detached = IsDetached(surface);
        first.AsSpan().Fill(byte.MaxValue);
        var second = new byte[first.Length];
        var rendered = Draw(surface, second, false);

        await Assert.That(detached).IsTrue();
        await Assert.That(rendered && IsDetached(surface)).IsTrue();
        await Assert.That(first.AsSpan().IndexOfAnyExcept(byte.MaxValue)).IsEqualTo(-1);
    }

    /// <summary>Reentry and close are rejected without releasing an active canvas.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ActiveHolderRejectsReentryAndClose()
    {
        using var surface = new RenderSurface();
        var pixels = new byte[Stride * Edge];
        var rejected = CheckActiveGuards(surface, pixels);

        await Assert.That(rejected && IsDetached(surface)).IsTrue();
        await Assert.That(Draw(surface, pixels, false)).IsTrue();
    }

    /// <summary>Repeated cleanup and close do not destroy an already released canvas.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IdleCloseIsIdempotentAndRejectsLaterUse()
    {
        using var surface = new RenderSurface();
        BorrowedPixelDrawing.Detach(surface);
        BorrowedPixelDrawing.Detach(surface);
        BorrowedPixelDrawing.Close(surface);
        BorrowedPixelDrawing.Close(surface);

        await Assert.That(surface.IsDisposed && surface.Canvas.Handle == 0).IsTrue();
        await Assert.That(() => BorrowedPixelDrawing.CheckAvailable(surface)).Throws<ObjectDisposedException>();
    }

    /// <summary>An invalid row stride fails and leaves all wrappers reusable.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InvalidInstallDetachesAndAllowsReuse()
    {
        using var surface = new RenderSurface();
        var pixels = new byte[Stride * Edge];
        var failed = AttachInvalid(surface, pixels);

        await Assert.That(failed && IsDetached(surface)).IsTrue();
        await Assert.That(Draw(surface, pixels, false)).IsTrue();
    }

    /// <summary>Draws synchronously while the caller's array is pinned.</summary>
    /// <param name="surface">The idle holder.</param>
    /// <param name="pixels">The target.</param>
    /// <param name="fail">Whether to throw after drawing.</param>
    /// <returns>Whether attaching succeeded.</returns>
    /// <exception cref="InvalidOperationException">The requested drawing failure.</exception>
    private static unsafe bool Draw(RenderSurface surface, byte[] pixels, bool fail)
    {
        BorrowedPixelDrawing.CheckAvailable(surface);
        fixed (byte* pointer = pixels)
        {
            try
            {
                if (!BorrowedPixelDrawing.Attach(surface, new(pixels, Edge, Edge, Stride), (nint)pointer))
                {
                    return false;
                }

                surface.Canvas.Clear(SKColors.Red);
                if (fail)
                {
                    throw new InvalidOperationException("Drawing failed after attaching pixels.");
                }

                return true;
            }
            finally
            {
                BorrowedPixelDrawing.Detach(surface);
            }
        }
    }

    /// <summary>Checks failure guards while preserving the original active canvas.</summary>
    /// <param name="surface">The idle holder.</param>
    /// <param name="pixels">The target.</param>
    /// <returns>Whether both guards rejected use and preserved the active handle.</returns>
    private static unsafe bool CheckActiveGuards(RenderSurface surface, byte[] pixels)
    {
        fixed (byte* pointer = pixels)
        {
            try
            {
                if (!BorrowedPixelDrawing.Attach(surface, new(pixels, Edge, Edge, Stride), (nint)pointer))
                {
                    return false;
                }

                var handle = surface.Canvas.Handle;
                var reentryRejected = false;
                var closeRejected = false;
                try
                {
                    BorrowedPixelDrawing.CheckAvailable(surface);
                }
                catch (InvalidOperationException)
                {
                    reentryRejected = true;
                }

                try
                {
                    BorrowedPixelDrawing.Close(surface);
                }
                catch (InvalidOperationException)
                {
                    closeRejected = true;
                }

                surface.Canvas.Clear(SKColors.Blue);
                return reentryRejected && closeRejected && surface.Canvas.Handle == handle && !surface.IsDisposed;
            }
            finally
            {
                BorrowedPixelDrawing.Detach(surface);
            }
        }
    }

    /// <summary>Attempts to attach a row too small for its pixels.</summary>
    /// <param name="surface">The idle holder.</param>
    /// <param name="pixels">The target.</param>
    /// <returns>Whether the installation failed.</returns>
    private static unsafe bool AttachInvalid(RenderSurface surface, byte[] pixels)
    {
        fixed (byte* pointer = pixels)
        {
            try
            {
                return !BorrowedPixelDrawing.Attach(surface, new(pixels, Edge, Edge, Stride - RenderedImage.BytesPerPixel), (nint)pointer);
            }
            finally
            {
                BorrowedPixelDrawing.Detach(surface);
            }
        }
    }

    /// <summary>Checks that all borrowed native references have been removed.</summary>
    /// <param name="surface">The holder.</param>
    /// <returns>Whether every wrapper is detached.</returns>
    private static bool IsDetached(RenderSurface surface) =>
        surface.Canvas.Handle == 0 && surface.Bitmap.GetPixels() == 0 && surface.Pixmap.GetPixels() == 0;
}
