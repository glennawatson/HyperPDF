// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Canvas;

/// <summary>
/// What a page canvas knows about the document it shows: the tab, the laid out pages and where the reader is. The canvas of any
/// UI toolkit owns one, and the helpers in this folder work on it through the <see cref="ICanvasHost"/> that shows it.
/// </summary>
[DebuggerDisplay("PageCanvasState: {Tab}")]
public sealed class PageCanvasState
{
    /// <summary>Initializes a new instance of the <see cref="PageCanvasState"/> class.</summary>
    /// <param name="host">The toolkit's canvas.</param>
    public PageCanvasState(ICanvasHost host)
    {
        ArgumentNullException.ThrowIfNull(host);
        Host = host;
    }

    /// <summary>Gets the toolkit's canvas.</summary>
    public ICanvasHost Host { get; }

    /// <summary>Gets what the page tools remember between pointer and key events.</summary>
    public PageToolState Tools { get; } = new();

    /// <summary>Gets the page measurements the fit zoom comes from, kept so a resize does not walk every page.</summary>
    public PageFitExtentsCache FitExtents { get; } = new();

    /// <summary>Gets the current layout.</summary>
    public DocumentLayout Layout { get; private set; } = DocumentLayout.Empty;

    /// <summary>Gets the page sizes the layout was built from.</summary>
    public PageSize[] Sizes { get; private set; } = [];

    /// <summary>Gets the page sizes <see cref="Layout"/> was computed from, or <see langword="null"/> when it must be computed again.</summary>
    public PageSize[]? LayoutSizes { get; private set; }

    /// <summary>Gets the canvas units per point at the current zoom, for sizing an editor placed on a page.</summary>
    public double PageScale => Layout.Options.Scale;

    /// <summary>Gets a value indicating whether a wheel zoom is easing, so tiles at passing scales are not requested.</summary>
    public bool IsZoomEasing => WheelZoom.IsRunning;

    /// <summary>Gets or sets the tab shown.</summary>
    public DocumentTabViewModel? Tab { get; set; }

    /// <summary>Gets or sets a position to restore once the layout has been measured.</summary>
    public Action? PendingScroll { get; set; }

    /// <summary>Gets or sets a value indicating whether the canvas is moving its own scroll offset to hold a zoom anchor.</summary>
    public bool Anchoring { get; set; }

    /// <summary>Gets or sets the page a navigation asked for, until the next position report; -1 when none.</summary>
    public int RequestedPage { get; set; } = -1;

    /// <summary>Gets or sets the zoom when the current pinch started, or 0 when there is no pinch.</summary>
    public double PinchStartZoom { get; set; }

    /// <summary>Gets or sets what the last position report was made from.</summary>
    public ReportedView Reported { get; set; }

    /// <summary>Gets or sets the document the middle page was last handed to the neighbour preparer for.</summary>
    public IDocument? VisitedDocument { get; set; }

    /// <summary>Gets or sets the middle page last handed to the neighbour preparer.</summary>
    public int VisitedPage { get; set; } = -1;

    /// <summary>Gets or sets the scale pages were last drawn at, which neighbouring pages are prepared for.</summary>
    public float NeighbourScale { get; set; } = 1;

    /// <summary>Gets or sets the wheel zoom easing now, or the last one once it has settled.</summary>
    public WheelZoomEase WheelZoom
    {
        get;
        set
        {
            var before = field;
            field = value;
            Host.WheelZoomChanged(before, value);
        }
    }

    /// <summary>Remembers the page sizes the layout is built from.</summary>
    /// <param name="sizes">The page sizes.</param>
    public void UseSizes(PageSize[] sizes) => Sizes = sizes;

    /// <summary>Remembers a layout and the page sizes it was computed from.</summary>
    /// <param name="layout">The layout.</param>
    /// <param name="builtFrom">The page sizes it was computed from, or <see langword="null"/> when it must be computed again.</param>
    public void UseLayout(DocumentLayout layout, PageSize[]? builtFrom)
    {
        Layout = layout;
        LayoutSizes = builtFrom;
    }

    /// <summary>Gets where a page is laid out on the canvas and how its points map there.</summary>
    /// <param name="page">The page.</param>
    /// <param name="transform">How the page's points map onto the canvas.</param>
    /// <returns><see langword="true"/> when the page is laid out.</returns>
    public bool TryGetPageTransform(int page, out PageTransform transform)
    {
        if (Tab is not { } tab || (uint)page >= (uint)Sizes.Length || page >= Layout.PageCount)
        {
            transform = default;
            return false;
        }

        transform = new(Layout.GetPageBounds(page), Sizes[page], tab.Rotation, Layout.Options.Scale);
        return true;
    }

    /// <summary>Converts a canvas point on a known page to page coordinates.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="point">The canvas point.</param>
    /// <returns>The point in page space.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PagePoint ToPage(DocumentTabViewModel tab, int page, CanvasPoint point) =>
        new PageTransform(Layout.GetPageBounds(page), Sizes[page], tab.Rotation, Layout.Options.Scale).MapToPage(point.X, point.Y);

    /// <summary>Converts a point on a page to canvas coordinates.</summary>
    /// <param name="page">The page.</param>
    /// <param name="point">The point in page space.</param>
    /// <returns>The point on the canvas, or the origin when the page is not laid out.</returns>
    public CanvasPoint ToCanvas(int page, PagePoint point) =>
        Tab is { } tab && (uint)page < (uint)Sizes.Length
            ? new PageTransform(Layout.GetPageBounds(page), Sizes[page], tab.Rotation, Layout.Options.Scale).MapToCanvas(point)
            : default;

    /// <summary>Gets the canvas rectangle of a rectangle on a page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="bounds">The rectangle in page points.</param>
    /// <returns>The rectangle on the canvas, or an empty rectangle when the page is not laid out.</returns>
    public LayoutRect GetCanvasRect(int page, PageRect bounds) =>
        Tab is not { } tab || (uint)page >= (uint)Sizes.Length
            ? default
            : new PageTransform(Layout.GetPageBounds(page), Sizes[page], tab.Rotation, Layout.Options.Scale).MapToCanvas(bounds);

    /// <summary>Gets the pages laid out inside an area of the canvas.</summary>
    /// <param name="left">The left side.</param>
    /// <param name="right">The right side.</param>
    /// <param name="top">The upper height.</param>
    /// <param name="bottom">The lower height.</param>
    /// <param name="first">The first page, or -1 when none.</param>
    /// <param name="last">The last page, or -1 when none.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void GetPagesBetween(double left, double right, double top, double bottom, out int first, out int last) =>
        Layout.GetVisiblePages(left, right, top, bottom, out first, out last);
}
