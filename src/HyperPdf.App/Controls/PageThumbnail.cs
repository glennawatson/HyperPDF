// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using HyperPdf.App.Rendering;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Rendering;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace HyperPdf.App.Controls;

/// <summary>Draws a page preview from the shared tile cache, requesting it at thumbnail priority when missing.</summary>
[DebuggerDisplay("PageThumbnail: Page {PageIndex}")]
public sealed class PageThumbnail : Control, IActivatableView
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

    /// <summary>Ends the queued request for the page being shown when the thumbnail moves on.</summary>
    private CancellationTokenSource? _request;

    /// <summary>The page <see cref="_request"/> is for.</summary>
    private int _requestPage = -1;

    /// <summary>The tab <see cref="_request"/> is for.</summary>
    private DocumentTabViewModel? _requestTab;

    /// <summary>The tab work token <see cref="_request"/> follows.</summary>
    private CancellationToken _requestFor;

    /// <summary>Subscriptions to the tab and render hub while attached.</summary>
    private MultipleDisposable? _subscriptions;

    /// <summary>Initializes static members of the <see cref="PageThumbnail"/> class.</summary>
    static PageThumbnail() => AffectsRender<PageThumbnail>(TabProperty, PageIndexProperty);

    /// <summary>Initializes a new instance of the <see cref="PageThumbnail"/> class.</summary>
    public PageThumbnail() =>
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.WhenChanged(static x => x.Tab).SubscribeSafe(Subscribe, OnError));
            disposables.Add(this.WhenChanged(static x => x.PageIndex).Skip(1).SubscribeSafe(_ => CancelRequest(), OnError));

            // Detaching releases the tab and render hub subscriptions.
            disposables.Add(Scope.Create(this, static thumbnail => thumbnail.Subscribe(null)));
        });

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

        var key = TileKey.Preview(tab.TileId, page, PageRotation.None, tab.PageTone.Id);
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
        var request = new RenderRequest(key, document, info, width, height, RenderPriority.Thumbnail, client, client.Generation, tab.PageTone) { CancellationToken = RequestToken(tab, page) };
        _ = tab.RenderHub.Scheduler.Request(request);
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>
    /// Gets the token for the request of a page's preview. It ends with the tab's selected work, and also when the
    /// thumbnail stops showing the page, so a preview that scrolled out of view is not drawn for nothing.
    /// </summary>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <returns>The token.</returns>
    private CancellationToken RequestToken(DocumentTabViewModel tab, int page)
    {
        var selected = tab.SelectedWorkToken;
        if (_request is null || !ReferenceEquals(_requestTab, tab) || _requestPage != page || _requestFor != selected)
        {
            CancelRequest();
            _request = CancellationTokenSource.CreateLinkedTokenSource(selected);
            _requestTab = tab;
            _requestPage = page;
            _requestFor = selected;
        }

        return _request.Token;
    }

    /// <summary>Cancels the request for the preview this thumbnail was showing, if it is still queued.</summary>
    private void CancelRequest()
    {
        if (_request is not { } request)
        {
            return;
        }

        _request = null;
        _requestTab = null;
        _ = CancellationSources.CancelAndDisposeAsync(request);
    }

    /// <summary>Redraws when new tiles arrive, until the thumbnail has its image.</summary>
    private void InvalidateWithoutImage()
    {
        if (!_hasImage)
        {
            InvalidateVisual();
        }
    }

    /// <summary>Subscribes to tile arrivals and page tone changes, replacing any previous subscriptions.</summary>
    /// <param name="tab">The tab, or <see langword="null"/> to unsubscribe.</param>
    private void Subscribe(DocumentTabViewModel? tab)
    {
        _subscriptions?.Dispose();
        _subscriptions = null;

        // The thumbnail can draw before it activates, so a request already queued for this tab is still wanted.
        // Cancelling it here would leave the thumbnail blank, as nothing repaints it afterwards.
        if (tab is null || !ReferenceEquals(_requestTab, tab))
        {
            CancelRequest();
        }

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
