// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using PdfViewerLite.App.Rendering;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Rendering;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.ObservableEvents;

namespace PdfViewerLite.App.Controls;

/// <summary>Draws a page preview from the shared tile cache, requesting it at thumbnail priority when missing.</summary>
[DebuggerDisplay("PageThumbnail: Page {PageIndex}")]
public sealed class PageThumbnail : Control
{
    /// <summary>Defines the <see cref="Tab"/> property.</summary>
    public static readonly StyledProperty<DocumentTabViewModel?> TabProperty = AvaloniaProperty.Register<PageThumbnail, DocumentTabViewModel?>(nameof(Tab));

    /// <summary>Defines the <see cref="PageIndex"/> property.</summary>
    public static readonly StyledProperty<int> PageIndexProperty = AvaloniaProperty.Register<PageThumbnail, int>(nameof(PageIndex), -1);

    /// <summary>The border brush.</summary>
    private static readonly IBrush BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0));

    /// <summary>The border pen.</summary>
    private static readonly IPen BorderPen = new Pen(BorderBrush);

    /// <summary>Whether the preview has been drawn, so tile arrivals no longer need a repaint.</summary>
    private bool _hasImage;

    /// <summary>Subscriptions to the tab and render hub while attached.</summary>
    private MultipleDisposable? _subscriptions;

    /// <summary>Property subscriptions while attached.</summary>
    private MultipleDisposable? _controlSubscriptions;

    /// <summary>Initializes static members of the <see cref="PageThumbnail"/> class.</summary>
    static PageThumbnail() => AffectsRender<PageThumbnail>(TabProperty, PageIndexProperty);

    /// <summary>Initializes a new instance of the <see cref="PageThumbnail"/> class.</summary>
    public PageThumbnail()
    {
        // These live as long as the control and only reference it, so they need no owner.
        _ = this.Events().AttachedToVisualTree.SubscribeSafe(_ => Attach(), OnError);
        _ = this.Events().DetachedFromVisualTree.SubscribeSafe(_ => Detach(), OnError);
    }

    /// <summary>Gets or sets the tab.</summary>
    public DocumentTabViewModel? Tab
    {
        get => GetValue(TabProperty);
        set => SetValue(TabProperty, value);
    }

    /// <summary>Gets or sets the zero based page index.</summary>
    public int PageIndex
    {
        get => GetValue(PageIndexProperty);
        set => SetValue(PageIndexProperty, value);
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var rect = new Rect(Bounds.Size);
        var tab = Tab;
        var page = PageIndex;
        _hasImage = false;
        context.FillRectangle(PaperBrush.Get(tab?.PageTone ?? PageTone.None), rect);
        context.DrawRectangle(BorderPen, rect);
        if (tab is null || page < 0 || page >= tab.Source.PageCount)
        {
            return;
        }

        var key = TileKey.Preview(tab.Source.Id, page, PageRotation.None, tab.PageTone.Id);
        if (tab.RenderHub.Cache.TryGet(key, out var surface))
        {
            using (context.PushRenderOptions(new() { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
            {
                TilePresentation.Draw(context, surface, rect, smooth: true);
            }

            _hasImage = true;
            return;
        }

        var document = tab.TryGetDocument();
        if (document is null)
        {
            return;
        }

        var size = tab.Source.PageSizes[page];
        var scale = TileGrid.GetPreviewScale(size, PageRotation.None);
        TileGrid.GetPagePixelSize(size, PageRotation.None, scale, out var width, out var height);
        var info = new PageRenderInfo(page, scale, PageRotation.None, 0, 0, RenderFlags.Annotations);
        var client = tab.ThumbnailClient;
        var request = new RenderRequest(key, document, info, width, height, RenderPriority.Thumbnail, client, client.Generation, tab.PageTone) { CancellationToken = tab.SelectedWorkToken };
        _ = tab.RenderHub.Scheduler.Request(request);
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Redraws when new tiles arrive, until the thumbnail has its image.</summary>
    private void InvalidateWithoutImage()
    {
        if (!_hasImage)
        {
            InvalidateVisual();
        }
    }

    /// <summary>Follows the tab while attached.</summary>
    private void Attach() =>
        _controlSubscriptions =
        [
            this.WhenChanged(static x => x.Tab).SubscribeSafe(Subscribe, OnError),
        ];

    /// <summary>Releases the tab and render hub subscriptions.</summary>
    private void Detach()
    {
        _controlSubscriptions?.Dispose();
        _controlSubscriptions = null;
        Subscribe(null);
    }

    /// <summary>Subscribes to tile arrivals and page tone changes, replacing any previous subscriptions.</summary>
    /// <param name="tab">The tab, or <see langword="null"/> to unsubscribe.</param>
    private void Subscribe(DocumentTabViewModel? tab)
    {
        _subscriptions?.Dispose();
        _subscriptions = null;
        if (tab is null)
        {
            return;
        }

        _subscriptions =
        [
            tab.RenderHub.TilesArrived.SubscribeSafe(_ => InvalidateWithoutImage(), OnError),
            tab.WhenChanged(static x => x.PageTone).Skip(1).SubscribeSafe(_ => InvalidateVisual(), OnError),
        ];
    }
}
