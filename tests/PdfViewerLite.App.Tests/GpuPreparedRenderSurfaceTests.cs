// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.Tracing;
using PdfViewerLite.App.Rendering;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Rendering;
using PdfViewerLite.Core.Theming;
using PdfViewerLite.HyperPdf;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks tile lifetime and software fallback across cache removal.</summary>
public sealed class GpuPreparedRenderSurfaceTests
{
    /// <summary>The tile side in pixels.</summary>
    private const int TileSide = 64;

    /// <summary>The bytes in one premultiplied BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The expected bytes of one premultiplied BGRA tile.</summary>
    private const int TileBytes = TileSide * TileSide * BytesPerPixel;

    /// <summary>A queued draw operation keeps a prepared tile alive after cache eviction.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RetainedDrawOperationSurvivesCacheEviction()
    {
        using var test = new TestServices();
        using var document = new HyperPdfEngine().Open(test.CreateDocument("gpu-lifetime.pdf", 1), null);
        var marker = new GpuPreparedRenderSurface(TileSide, TileSide, new GpuAvailability());
        var request = Request(document);
        var prepared = marker.Prepare(request, CancellationToken.None);
        var operation = GpuTileDrawOperation.TryCreate(marker, new(0, 0, TileSide, TileSide), false);
        marker.Dispose();
        var fallback = marker.EnsureSoftwareBitmap();
        var hasBitmapWhileRetained = marker.SoftwareBitmap is not null;
        var renderingRetained = marker.TryRetainForRender();
        operation?.Dispose();
        var hasBitmapDuringRender = marker.SoftwareBitmap is not null;
        if (renderingRetained)
        {
            marker.Release();
        }

        using (Assert.Multiple())
        {
            await Assert.That(prepared).IsTrue();
            await Assert.That(operation).IsNotNull();
            await Assert.That(fallback).IsTrue();
            await Assert.That(hasBitmapWhileRetained).IsTrue();
            await Assert.That(renderingRetained).IsTrue();
            await Assert.That(hasBitmapDuringRender).IsTrue();
            await Assert.That(marker.SoftwareBitmap).IsNull();
            await Assert.That(marker.TryRetain()).IsFalse();
            await Assert.That(marker.ByteSize).IsEqualTo(TileBytes);
        }
    }

    /// <summary>A failed device routes later tiles to worker-rendered software pixels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeviceFailurePreparesLaterTilesInSoftware()
    {
        using var test = new TestServices();
        using var document = new HyperPdfEngine().Open(test.CreateDocument("gpu-fallback.pdf", 1), null);
        var availability = new GpuAvailability();
        var recoveryRequests = 0;
        using var first = new GpuPreparedRenderSurface(TileSide, TileSide, availability, () => recoveryRequests++);
        var firstPrepared = first.Prepare(Request(document), CancellationToken.None);
        var firstHadNoCpuBitmap = first.SoftwareBitmap is null;
        first.MarkGpuUnavailable();
        var firstHadNoSynchronousFallback = first.SoftwareBitmap is null;
        using var second = new GpuPreparedRenderSurface(TileSide, TileSide, availability);
        var secondPrepared = second.Prepare(Request(document), CancellationToken.None);

        using (Assert.Multiple())
        {
            await Assert.That(firstPrepared).IsTrue();
            await Assert.That(firstHadNoCpuBitmap).IsTrue();
            await Assert.That(firstHadNoSynchronousFallback).IsTrue();
            await Assert.That(recoveryRequests).IsEqualTo(1);
            await Assert.That(availability.IsUnavailable).IsTrue();
            await Assert.That(secondPrepared).IsTrue();
            await Assert.That(second.SoftwareBitmap).IsNotNull();
        }
    }

    /// <summary>The default soft-paper effect keeps page recording on the GPU path.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultTonePreparesGpuTileWithoutCpuPixels()
    {
        using var test = new TestServices();
        using var document = new HyperPdfEngine().Open(test.CreateDocument("gpu-tone.pdf", 1), null);
        using var surface = new GpuPreparedRenderSurface(TileSide, TileSide, new GpuAvailability());
        var request = Request(document) with { Tone = ColorSchemes.SoftPaperTone };

        var prepared = surface.Prepare(request, CancellationToken.None);

        await Assert.That(prepared).IsTrue();
        await Assert.That(surface.SoftwareBitmap).IsNull();
    }

    /// <summary>A prepared software tile is observable in EventPipe without a console message.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SoftwareFallbackEmitsTileEvent()
    {
        using var listener = new TileEventListener();
        using var test = new TestServices();
        using var document = new HyperPdfEngine().Open(test.CreateDocument("gpu-events.pdf", 1), null);
        var availability = new GpuAvailability();
        availability.MarkUnavailable();
        using var surface = new GpuPreparedRenderSurface(TileSide, TileSide, availability);

        var prepared = surface.Prepare(Request(document), CancellationToken.None);

        await Assert.That(prepared).IsTrue();
        await Assert.That(listener.SoftwareEvents).IsGreaterThan(0);
    }

    /// <summary>Document close releases cached GPU markers and asks attached views to render another frame.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DocumentCloseSignalsFrameForResourceRetirement()
    {
        using var test = new TestServices();
        var hub = test.Services.RenderHub;
        var marker = new GpuPreparedRenderSurface(TileSide, TileSide, new GpuAvailability());
        var key = TileKey.Preview(1, 0, PageRotation.None, PageTone.None.Id);
        hub.Cache.Add(key, marker);
        var notified = false;
        using var subscription = hub.TilesArrived.SubscribeSafe(_ => notified = true, static _ => { });

        hub.RemoveDocumentTiles(key.DocumentId);

        using (Assert.Multiple())
        {
            await Assert.That(hub.Cache.Count).IsEqualTo(0);
            await Assert.That(marker.TryRetain()).IsFalse();
            await Assert.That(notified).IsTrue();
        }
    }

    /// <summary>Builds a visible tile request for the generated page.</summary>
    /// <param name="document">The open PDF.</param>
    /// <returns>The request.</returns>
    private static RenderRequest Request(IDocument document)
    {
        var client = new RenderClient();
        var tone = PageTone.None;
        return new(
            new TileKey(1, 0, TileGrid.ToScaleKey(1), PageRotation.None, tone.Id, 0, 0),
            document,
            new PageRenderInfo(0, 1, PageRotation.None, 0, 0, RenderFlags.Annotations),
            TileSide,
            TileSide,
            RenderPriority.Visible,
            client,
            client.Generation,
            tone);
    }

    /// <summary>Counts software tile events from the viewer's EventPipe provider.</summary>
    private sealed class TileEventListener : EventListener
    {
        /// <summary>The provider name shared with trace collection.</summary>
        private const string ProviderName = "PdfViewerLite.Rendering.Tiles";

        /// <summary>Completed software tile events.</summary>
        private int _softwareEvents;

        /// <summary>Gets the number of observed software tiles.</summary>
        internal int SoftwareEvents => Volatile.Read(ref _softwareEvents);

        /// <inheritdoc/>
        protected override void OnEventSourceCreated(EventSource eventSource)
        {
            if (eventSource.Name == ProviderName)
            {
                EnableEvents(eventSource, EventLevel.Informational);
            }
        }

        /// <inheritdoc/>
        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventSource.Name == ProviderName && eventData.EventName == "SoftwarePrepared")
            {
                _ = Interlocked.Increment(ref _softwareEvents);
            }
        }
    }
}
