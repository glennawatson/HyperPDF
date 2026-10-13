// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Render.Skia;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks that a slow page recording does not stop tiles of other, already recorded pages.</summary>
[NotInParallel]
public sealed class PagePicturesRecordingStallTests
{
    /// <summary>The size of the tiles rendered, in pixels.</summary>
    private const int TileSize = 32;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The pages of the test document.</summary>
    private const int Pages = 3;

    /// <summary>The page that is recorded before the gate closes.</summary>
    private const int CachedPage = 1;

    /// <summary>How long a step may take before the test gives up.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    /// <summary>How long a tile of a recorded page may take while another page records.</summary>
    private static readonly TimeSpan TileLimit = TimeSpan.FromSeconds(3);

    /// <summary>How often the test looks at the state of the waiting thread.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(5);

    /// <summary>A second tile of a page that is recording must not hold up the tiles of a page that is already recorded.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ARecordedPageRendersWhileAnotherPageRecords()
    {
        using var gate = new ManualResetEventSlim(false);
        using var entered = new SemaphoreSlim(0);
        var backend = new GateBackend(new SkiaRenderBackend(), gate, entered);
        var services = new PdfDrawingServices(new PdfRenderBackendRegistration().UseSkia() with { Backend = backend });
        using var document = PdfDocumentReader.OpenWith(TestPdf.Create(Pages), new PdfOpenOptions { DrawingServices = services });
        using var renderer = new PdfPageRenderer(document);

        // One page is recorded and cached before the gate closes.
        Render(renderer, CachedPage);
        backend.Close();

        var recording = Task.Run(() => Render(renderer, 0));
        await entered.WaitAsync(Patience);

        // A second tile of the recording page waits for its picture, as it must. A thread of its own lets the test see it wait.
        var sibling = new Thread(() => Render(renderer, 0)) { IsBackground = true };
        sibling.Start();
        await WaitUntilBlockedAsync(sibling);

        // A tile of the cached page needs nothing from the page that is recording.
        var cached = Task.Run(() => Render(renderer, CachedPage));
        var cachedFinished = true;
        try
        {
            await cached.WaitAsync(TileLimit);
        }
        catch (TimeoutException)
        {
            cachedFinished = false;
        }

        gate.Set();
        await recording.WaitAsync(Patience);
        await cached.WaitAsync(Patience);
        await Assert.That(cachedFinished).IsTrue();
    }

    /// <summary>Waits until a thread is blocked on a lock or wait handle.</summary>
    /// <param name="thread">The thread.</param>
    /// <returns>A task.</returns>
    private static async Task WaitUntilBlockedAsync(Thread thread)
    {
        using var timeout = new CancellationTokenSource(Patience);
        using var timer = new PeriodicTimer(PollInterval);
        while ((thread.ThreadState & ThreadState.WaitSleepJoin) == 0)
        {
            _ = await timer.WaitForNextTickAsync(timeout.Token);
        }
    }

    /// <summary>Renders one tile of a page.</summary>
    /// <param name="renderer">The renderer.</param>
    /// <param name="pageIndex">The page index.</param>
    private static void Render(PdfPageRenderer renderer, int pageIndex)
    {
        var pixels = new byte[TileSize * TileSize * BytesPerPixel];
        _ = renderer.Render(new(pageIndex, 1, 0, 0, 0, PdfRenderFlags.None), new(pixels, TileSize, TileSize, TileSize * BytesPerPixel));
    }

    /// <summary>A backend that holds a page recording until the test lets it go.</summary>
    private sealed class GateBackend : IPdfRenderBackend
    {
        /// <summary>The backend everything is passed to.</summary>
        private readonly IPdfRenderBackend _inner;

        /// <summary>Opened by the test to let the held recording go on.</summary>
        private readonly ManualResetEventSlim _gate;

        /// <summary>Released when a recording reaches the gate.</summary>
        private readonly SemaphoreSlim _entered;

        /// <summary>One while the next recording waits at the gate.</summary>
        private int _closed;

        /// <summary>Initializes a new instance of the <see cref="GateBackend"/> class.</summary>
        /// <param name="inner">The backend to pass calls to.</param>
        /// <param name="gate">Opened by the test.</param>
        /// <param name="entered">Released when a recording waits.</param>
        internal GateBackend(IPdfRenderBackend inner, ManualResetEventSlim gate, SemaphoreSlim entered)
        {
            _inner = inner;
            _gate = gate;
            _entered = entered;
        }

        /// <inheritdoc/>
        public IPdfDrawingSession GetDrawingSession() => _inner.GetDrawingSession();

        /// <inheritdoc/>
        public IPictureDevice CreatePictureDevice(PdfRect bounds)
        {
            if (Interlocked.Exchange(ref _closed, 0) == 1)
            {
                _ = _entered.Release();
                _ = _gate.Wait(Patience);
            }

            return _inner.CreatePictureDevice(bounds);
        }

        /// <inheritdoc/>
        public IPdfRenderImage? CreateImage(PdfImageData data) => _inner.CreateImage(data);

        /// <inheritdoc/>
        public IPdfRenderImage CreateImage(PdfImagePixelLayout layout, ReadOnlySpan<byte> pixels, int rowBytes) => _inner.CreateImage(layout, pixels, rowBytes);

        /// <inheritdoc/>
        public IPdfRenderShader CreateLinearGradient(PdfPoint start, PdfPoint end, PdfColor[] colors, float[] positions, PdfShaderTileMode mode) =>
            _inner.CreateLinearGradient(start, end, colors, positions, mode);

        /// <inheritdoc/>
        public IPdfRenderShader CreateTwoPointConicalGradient(PdfPoint start, float startRadius, PdfPoint end, float endRadius, PdfColor[] colors, float[] positions, PdfShaderTileMode mode) =>
            _inner.CreateTwoPointConicalGradient(start, startRadius, end, endRadius, colors, positions, mode);

        /// <inheritdoc/>
        public IPdfRenderShader CreateImageShader(IPdfRenderImage image, PdfShaderTileMode x, PdfShaderTileMode y, bool linear, Matrix3x2 matrix) =>
            _inner.CreateImageShader(image, x, y, linear, matrix);

        /// <inheritdoc/>
        public IPdfRenderVertices CreateVertices(PdfPoint[] positions, PdfColor[] colors) => _inner.CreateVertices(positions, colors);

        /// <inheritdoc/>
        public IPdfRenderVertices CreateVertices(PdfPoint[] positions, PdfPoint[] texture, PdfColor[]? colors) => _inner.CreateVertices(positions, texture, colors);

        /// <inheritdoc/>
        public PatternCell ComposeTile(IPdfRenderPicture cell, PdfRectangle box, float stepX, float stepY, float scale) =>
            _inner.ComposeTile(cell, box, stepX, stepY, scale);

        /// <summary>Makes the next recording wait at the gate.</summary>
        internal void Close() => Volatile.Write(ref _closed, 1);
    }
}
