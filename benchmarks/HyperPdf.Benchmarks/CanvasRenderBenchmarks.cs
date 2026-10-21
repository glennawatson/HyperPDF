// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;
using HyperPdf.Presentation.Canvas;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Tests;

namespace HyperPdf.Benchmarks;

/// <summary>
/// Measures planning one frame of the page canvas through the toolkit-neutral <see cref="PageCanvasState"/> and a painter that
/// only counts, on a document whose tiles are already cached, as a repaint with nothing new to ask for is. The painter's own
/// drawing is the toolkit's and is not measured here.
/// </summary>
public class CanvasRenderBenchmarks
{
    /// <summary>The pages in the document.</summary>
    private const int PageCount = 40;

    /// <summary>The width of the viewport.</summary>
    private const double ViewWidth = 1000;

    /// <summary>The height of the viewport.</summary>
    private const double ViewHeight = 800;

    /// <summary>The display scale.</summary>
    private const double Scaling = 1;

    /// <summary>How many warm up frames are tried before the cache is taken to be full.</summary>
    private const int WarmUpFrames = 200;

    /// <summary>How long to wait between warm up frames, in milliseconds.</summary>
    private const int WarmUpDelay = 25;

    /// <summary>The painter, which counts what it is told to draw.</summary>
    private readonly CountingTilePainter _painter = new();

    /// <summary>The overlay painter, which counts what it is told to draw.</summary>
    private readonly CountingOverlayPainter _overlay = new();

    /// <summary>The services of the document's tab.</summary>
    private TestServices _services = null!;

    /// <summary>The view model that owns the tab.</summary>
    private MainViewModel _main = null!;

    /// <summary>The canvas state under test.</summary>
    private PageCanvasState _state = null!;

    /// <summary>The frame to plan.</summary>
    private FrameContext _frame;

    /// <summary>Opens the document and draws frames until nothing more is asked for, so the cache is warm.</summary>
    /// <returns>A task.</returns>
    [GlobalSetup]
    public async Task SetupAsync()
    {
        _services = new();
        _main = new(_services.Services);
        await TestServices.OpenAndWaitAsync(_main, [_services.CreateDocument("canvas-render.pdf", PageCount)]);
        var tab = _main.SelectedTab!;
        var view = new CanvasSize(ViewWidth, ViewHeight);
        var host = new BenchmarkCanvasHost { Viewport = view, Extent = view };
        _state = new(host) { Tab = tab };
        _state.RebuildLayout();
        host.Extent = new(_state.Layout.ExtentWidth, _state.Layout.ExtentHeight);
        var viewport = new LayoutRect(0, 0, ViewWidth, ViewHeight);
        var document = tab.TryGetDocument()!;
        _frame = new(tab, document, tab.RenderHub, viewport, Scaling, _state.GetRenderGeneration(tab, document, viewport, Scaling));
        for (var attempt = 0; attempt < WarmUpFrames; attempt++)
        {
            _state.DrawPages(_painter, _frame);
            if (_state.Render.RequestsThisFrame == 0)
            {
                _state.DrawPages(_painter, _frame);
                break;
            }

            await Task.Delay(WarmUpDelay);
        }
    }

    /// <summary>Closes the document and its services.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _main.Dispose();
        _services.Dispose();
    }

    /// <summary>Plans a repaint of the same view with the same cache, which skips the prefetch passes.</summary>
    /// <returns>The surfaces drawn.</returns>
    [Benchmark]
    public int RepaintUnchangedView()
    {
        _state.DrawPages(_painter, _frame);
        return _painter.Surfaces;
    }

    /// <summary>Plans a repaint that has to look through every prefetch pass, as the first frame after a cache change does.</summary>
    /// <returns>The surfaces drawn.</returns>
    [Benchmark]
    public int RepaintWithEveryPrefetchPass()
    {
        _state.Render.LastPrefetchSettled = false;
        _state.DrawPages(_painter, _frame);
        return _painter.Surfaces;
    }

    /// <summary>Plans the marks over the pages when no tool is under way, as every frame does.</summary>
    /// <returns>The marks drawn.</returns>
    [Benchmark]
    public int OverlayWithNothingUnderWay()
    {
        var tab = _main.SelectedTab!;
        _state.DrawAnnotationOverlay(_overlay, tab, _frame.Viewport);
        _state.DrawRegions(_overlay, tab, _frame.Viewport);
        _state.DrawMeasurement(_overlay, tab);
        _state.DrawArea(_overlay);
        _state.DrawCaret(_overlay, tab);
        return _overlay.Marks;
    }

    /// <summary>An overlay painter with no toolkit: it counts what it is told to draw.</summary>
    private sealed class CountingOverlayPainter : IOverlayPainter
    {
        /// <summary>Gets how many marks were drawn.</summary>
        public int Marks => Lines + Strokes + Rectangles + Ellipses + Fills + Dots + Badges + Labels + Placed;

        /// <summary>Gets how many signature marks were drawn.</summary>
        public int Placed { get; private set; }

        /// <summary>Gets how many lines were drawn.</summary>
        public int Lines { get; private set; }

        /// <summary>Gets how many stroke lines were drawn.</summary>
        public int Strokes { get; private set; }

        /// <summary>Gets how many rectangle outlines were drawn.</summary>
        public int Rectangles { get; private set; }

        /// <summary>Gets how many ellipse outlines were drawn.</summary>
        public int Ellipses { get; private set; }

        /// <summary>Gets how many rectangles were filled.</summary>
        public int Fills { get; private set; }

        /// <summary>Gets how many dots were filled.</summary>
        public int Dots { get; private set; }

        /// <summary>Gets how many badges were drawn.</summary>
        public int Badges { get; private set; }

        /// <summary>Gets how many labels were drawn.</summary>
        public int Labels { get; private set; }

        /// <inheritdoc/>
        public bool CanDraw(OverlayInk ink) => true;

        /// <inheritdoc/>
        public void DrawLine(OverlayInk ink, CanvasPoint from, CanvasPoint to) => Lines++;

        /// <inheritdoc/>
        public void DrawStrokeLine(bool highlighter, uint color, double width, CanvasPoint from, CanvasPoint to) => Strokes++;

        /// <inheritdoc/>
        public void DrawRectangle(OverlayInk ink, in LayoutRect rect) => Rectangles++;

        /// <inheritdoc/>
        public void DrawEllipse(OverlayInk ink, in LayoutRect box) => Ellipses++;

        /// <inheritdoc/>
        public void FillRectangle(OverlayInk ink, in LayoutRect rect) => Fills++;

        /// <inheritdoc/>
        public void FillDot(OverlayInk ink, CanvasPoint centre, double radius) => Dots++;

        /// <inheritdoc/>
        public void DrawBadge(in LayoutRect rect, uint color) => Badges++;

        /// <inheritdoc/>
        public void DrawMark(SignatureMark mark, in LayoutRect area, double inkWidth) => Placed++;

        /// <inheritdoc/>
        public void DrawLabel(OverlayInk ink, string text, CanvasPoint beside) => Labels++;
    }

    /// <summary>A painter with no toolkit: it counts what it is told to draw.</summary>
    private sealed class CountingTilePainter : ITilePainter
    {
        /// <summary>Gets how many surfaces were drawn.</summary>
        public int Surfaces { get; private set; }

        /// <inheritdoc/>
        public void BeginPages()
        {
        }

        /// <inheritdoc/>
        public void EndPages()
        {
        }

        /// <inheritdoc/>
        public void DrawPaper(in LayoutRect page, PageTone tone)
        {
        }

        /// <inheritdoc/>
        public void DrawSurface(IRenderSurface surface, in LayoutRect destination, bool smooth) => Surfaces++;

        /// <inheritdoc/>
        public void FlushSurfaces()
        {
        }

        /// <inheritdoc/>
        public void DrawPageOverlay(int page, in LayoutRect bounds)
        {
        }
    }
}
