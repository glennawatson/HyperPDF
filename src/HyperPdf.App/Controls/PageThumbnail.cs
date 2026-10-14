// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
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
using ReactiveUI.Primitives.Signals;

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

    /// <summary>Initializes static members of the <see cref="PageThumbnail"/> class.</summary>
    static PageThumbnail() => AffectsRender<PageThumbnail>(TabProperty, PageIndexProperty);

    /// <summary>Initializes a new instance of the <see cref="PageThumbnail"/> class.</summary>
    public PageThumbnail() =>
        _ = this.WhenActivated(disposables =>
        {
            var tabs = this.WhenChanged(static x => x.Tab);

            // The thumbnail can draw before it activates, so a request already queued for this tab is still wanted.
            // Cancelling it would leave the thumbnail blank, as nothing repaints it afterwards; a request for another
            // tab is not.
            disposables.Add(tabs
                .WhereWith(this, static (thumbnail, tab) => tab is null || !ReferenceEquals(thumbnail._requestTab, tab))
                .SubscribeSafe(_ => CancelRequest(), ErrorTrace.Write));
            disposables.Add(this.WhenChanged(static x => x.PageIndex).Skip(1).SubscribeSafe(_ => CancelRequest(), ErrorTrace.Write));

            // The shown tab's tile arrivals and page tone changes repaint the thumbnail; showing another tab ends the
            // streams of the one before.
            disposables.Add(tabs
                .SelectWith(this, static (thumbnail, tab) => new ShownTab(thumbnail, tab))
                .SwitchMap(Repaints)
                .SubscribeSafe(_ => InvalidateVisual(), ErrorTrace.Write));

            // Taken off screen, the thumbnail gives up its queued request.
            disposables.Add(Scope.Create(this, static thumbnail => thumbnail.CancelRequest()));
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

    /// <summary>
    /// Gets the moments a tab's thumbnail must be drawn again: when new tiles arrive while it has no image yet, and when
    /// the page tone changes.
    /// </summary>
    /// <param name="shown">The thumbnail and the tab it shows.</param>
    /// <returns>The repaints, or none without a tab.</returns>
    private static IObservable<RxVoid> Repaints(ShownTab shown) => shown.Tab is not { } tab
        ? Signal.Empty<RxVoid>()
        : Signal.Merge(
            tab.RenderHub.TilesArrived.WhereWith(shown.Thumbnail, static (thumbnail, _) => !thumbnail._hasImage),
            tab.WhenChanged(static x => x.PageTone).Skip(1).Select(static _ => RxVoid.Default));

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

    /// <summary>A thumbnail and the tab it shows.</summary>
    /// <param name="Thumbnail">The thumbnail.</param>
    /// <param name="Tab">The tab, or <see langword="null"/>.</param>
    private sealed record ShownTab(PageThumbnail Thumbnail, DocumentTabViewModel? Tab);
}
