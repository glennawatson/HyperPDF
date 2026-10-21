// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using HyperPdf.App.Rendering;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;
using HyperPdf.Presentation.Canvas;

namespace HyperPdf.App.Controls;

/// <summary>Puts the pages the canvas plans on an Avalonia drawing context: paper, cached tiles, and the marks over a page.</summary>
internal sealed class AvaloniaTilePainter : ITilePainter
{
    /// <summary>The page shadow offset.</summary>
    private const double ShadowOffset = 2;

    /// <summary>The page shadow brush.</summary>
    private static readonly IBrush ShadowBrush = new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0));

    /// <summary>
    /// How page images are sampled, with aliased edges so tiles meet without seams. Only stand-ins are ever scaled: the
    /// preview and the last scale's tiles while a zoom settles. Bilinear with mipmaps draws those about ten times
    /// faster than cubic (BitmapDrawBenchmarks), which keeps the zoom ease smooth; sharp tiles are drawn 1:1.
    /// </summary>
    private static readonly RenderOptions PageRenderOptions = new() { BitmapInterpolationMode = BitmapInterpolationMode.MediumQuality, EdgeMode = EdgeMode.Aliased };

    /// <summary>The canvas the pages belong to.</summary>
    private readonly PageCanvas _canvas;

    /// <summary>Queues each page's GPU tiles so they reach the scene as one operation.</summary>
    private readonly TileDrawBatch _tiles = new();

    /// <summary>The context the frame is drawn into.</summary>
    private DrawingContext? _context;

    /// <summary>The render options in force while the pages are drawn.</summary>
    private DrawingContext.PushedState _options;

    /// <summary>Initializes a new instance of the <see cref="AvaloniaTilePainter"/> class.</summary>
    /// <param name="canvas">The canvas the pages belong to.</param>
    internal AvaloniaTilePainter(PageCanvas canvas) => _canvas = canvas;

    /// <inheritdoc/>
    public void BeginPages() => _options = _context!.PushRenderOptions(PageRenderOptions);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EndPages() => _options.Dispose();

    /// <inheritdoc/>
    public void DrawPaper(in LayoutRect page, PageTone tone)
    {
        var rect = new Rect(page.X, page.Y, page.Width, page.Height);
        _context!.FillRectangle(ShadowBrush, rect.Translate(new(ShadowOffset, ShadowOffset)));
        _context.FillRectangle(PaperBrush.Get(tone), rect);
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void DrawSurface(IRenderSurface surface, in LayoutRect destination, bool smooth) =>
        _tiles.Draw(_context!, surface, new(destination.X, destination.Y, destination.Width, destination.Height), smooth);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void FlushSurfaces() => _tiles.Flush(_context!);

    /// <inheritdoc/>
    public void DrawPageOverlay(int page, in LayoutRect bounds)
    {
        if (_canvas.Tab is { } tab)
        {
            _canvas.DrawSelection(_context!, tab, page, bounds);
        }
    }

    /// <summary>Starts a frame in a drawing context.</summary>
    /// <param name="context">The drawing context.</param>
    internal void Start(DrawingContext context) => _context = context;
}
