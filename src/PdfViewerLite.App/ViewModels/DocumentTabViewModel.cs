// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using PdfViewerLite.App.Rendering;
using PdfViewerLite.App.Services;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Layout;
using PdfViewerLite.Core.Navigation;
using PdfViewerLite.Core.Platform;
using PdfViewerLite.Core.Printing;
using PdfViewerLite.Core.Reading;
using PdfViewerLite.Core.Rendering;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Advanced;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// One open document. A tab is cheap until it is first shown: the native document is opened on demand through the
/// <see cref="DocumentPool"/> and may be closed again while the tab is in the background.
/// </summary>
[DebuggerDisplay("DocumentTabViewModel: {FileName}")]
public sealed partial class DocumentTabViewModel : ReactiveObject, IDisposable
{
    /// <summary>How long after saving a file change notice is taken to be our own save.</summary>
    private const long SelfSaveWindowMilliseconds = 2000;

    /// <summary>The width of tab hover previews, in device independent pixels.</summary>
    private const double PreviewSize = 180;

    /// <summary>The width of sidebar thumbnails in device independent pixels.</summary>
    private const double ThumbnailWidth = 120;

    /// <summary>Percent multiplier.</summary>
    private const double Percent = 100;

    /// <summary>The services.</summary>
    private readonly AppServices _services;

    /// <summary>Emits navigation requests for the canvas.</summary>
    private readonly Signal<NavigationRequest> _navigationRequests = new();

    /// <summary>Emits external links to open.</summary>
    private readonly Signal<Uri> _uriRequests = new();

    /// <summary>Emits when the document must be laid out again.</summary>
    private readonly Signal<RxVoid> _documentChanges = new();

    /// <summary>Emits the index of each page whose content was edited.</summary>
    private readonly Signal<int> _pageEdits = new();

    /// <summary>Emits text for the view to put on the clipboard.</summary>
    private readonly Signal<string> _copyRequests = new();

    /// <summary>Owns the subscriptions that react to this tab's own property changes.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>Whether Back has somewhere to go.</summary>
    private readonly IObservable<bool> _canGoBack;

    /// <summary>Whether Forward has somewhere to go.</summary>
    private readonly IObservable<bool> _canGoForward;

    /// <summary>Whether the sidebar was shown before read mode put it away.</summary>
    private bool _sidebarBeforeReading;

    /// <summary>When the file was last saved from this tab, so the change notice that follows is not taken for an outside edit.</summary>
    private long _savedAt;

    /// <summary>Watches the file for changes once loaded.</summary>
    private IDisposable? _fileWatch;

    /// <summary>The Read Aloud state, once used.</summary>
    private ReadAloudViewModel? _readAloud;

    /// <summary>The text recognition state, once used.</summary>
    private TextRecognitionViewModel? _textRecognition;

    /// <summary>Focus Mode, once used.</summary>
    private FocusModeViewModel? _focusMode;

    /// <summary>The layers panel, once used.</summary>
    private LayersViewModel? _layers;

    /// <summary>The measuring tool, once used.</summary>
    private MeasureViewModel? _measure;

    /// <summary>The annotation state, created on first use.</summary>
    private AnnotationsViewModel? _annotations;

    /// <summary>The page management state, created on first use.</summary>
    private PageManagementViewModel? _pages;

    /// <summary>The document's pages in reading order, once asked for.</summary>
    private ReadingDocument? _reading;

    /// <summary>The source version the reading order was worked out for.</summary>
    private long _readingFor = -1;

    /// <summary>The open document the reading order reads from, captured on the UI thread.</summary>
    private IDocument? _readingSource;

    /// <summary>The search panel, created only when first used.</summary>
    private SearchViewModel? _search;

    /// <summary>Cancels the selected tab's open, search and rendering work when another tab is selected.</summary>
    private CancellationTokenSource? _selectedWork;

    /// <summary>How the tab looked before presenting.</summary>
    private PresentationState _beforePresenting;

    /// <summary>Initializes a new instance of the <see cref="DocumentTabViewModel"/> class.</summary>
    /// <param name="source">The document source.</param>
    /// <param name="services">The application services.</param>
    public DocumentTabViewModel(DocumentSource source, AppServices services)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(services);
        Source = source;
        _services = services;
        FileName = Path.GetFileName(source.FilePath);
        Title = FileName;
        ZoomMode = services.Settings.DefaultZoomMode;
        LayoutMode = services.Settings.DefaultLayoutMode;
        SidebarVisible = services.Settings.ShowSidebar;
        PageTone = services.CurrentTheme.PageTone;
        _canGoBack = this.WhenChanged(static vm => vm.CanGoBack);
        _canGoForward = this.WhenChanged(static vm => vm.CanGoForward);
        _canOptimize = this.WhenChanged(static vm => vm.CanOptimize);
        _canRedact = this.WhenChanged(static vm => vm.CanRedact);

        NavigationRequests = new(_navigationRequests);
        UriRequests = new(_uriRequests);
        DocumentChanges = new(_documentChanges);
        PageEdits = new(_pageEdits);
        CopyRequests = new(_copyRequests);

        // The first value of each property is its current state, which needs no reaction.
        _subscriptions.Add(this.WhenChanged(static x => x.CurrentPageIndex)
            .Skip(1)
            .SubscribeSafe(OnCurrentPageChanged, OnError));
        _subscriptions.Add(this.WhenChanged(static x => x.SidebarMode)
            .Skip(1)
            .SubscribeSafe(OnSidebarModeChanged, OnError));
        _subscriptions.Add(this.WhenChanged(static x => x.SelectedThumbnail)
            .Skip(1)
            .SubscribeSafe(OnThumbnailSelected, OnError));
        _subscriptions.Add(this.WhenChanged(static x => x.SelectedOutlineItem)
            .Skip(1)
            .SubscribeSafe(OnOutlineItemSelected, OnError));
        _isTextFormatShownHelper = this.WhenChanged(static vm => vm.Annotations.IsAnnotating, static vm => vm.FillAndSign.IsActive, static (annotating, filling) => annotating || filling)
            .ToProperty(this, static vm => vm.IsTextFormatShown);
        _subscriptions.Add(_isTextFormatShownHelper);
    }

    /// <summary>Gets the width of the hover preview of a tab.</summary>
    public static double PreviewWidth => PreviewSize;

    /// <summary>Gets a value indicating whether the text format row is shown: with the Annotate and Fill &amp; Sign tools.</summary>
    [ObservableAsProperty]
    public partial bool IsTextFormatShown { get; }

    /// <summary>Gets the requests for the canvas to scroll.</summary>
    public AsObservableSignal<NavigationRequest> NavigationRequests { get; }

    /// <summary>Gets the external links the user activated.</summary>
    public AsObservableSignal<Uri> UriRequests { get; }

    /// <summary>Gets notifications that the document must be laid out again (loaded or reloaded).</summary>
    public AsObservableSignal<RxVoid> DocumentChanges { get; }

    /// <summary>Gets the document source.</summary>
    public DocumentSource Source { get; }

    /// <summary>Gets the render hub.</summary>
    public RenderHub RenderHub => _services.RenderHub;

    /// <summary>Gets a value indicating whether the reader asked for movement to be reduced, so views jump instead of easing.</summary>
    public bool ReduceMotion => _services.CurrentTheme.ReduceMotion;

    /// <summary>Gets the render client used by the page canvas.</summary>
    public RenderClient CanvasClient { get; } = new();

    /// <summary>Gets the render client used by sidebar thumbnails.</summary>
    public RenderClient ThumbnailClient { get; } = new();

    /// <summary>Gets the token for work requested while this tab is selected.</summary>
    public CancellationToken SelectedWorkToken => _selectedWork?.Token ?? CancellationToken.None;

    /// <summary>Gets the navigation history.</summary>
    public NavigationHistory History { get; } = new();

    /// <summary>Gets the search state, created on first use.</summary>
    public SearchViewModel Search => _search ??= new(this);

    /// <summary>Gets the file path.</summary>
    public string FilePath => Source.FilePath;

    /// <summary>Gets the file name.</summary>
    public string FileName { get; }

    /// <summary>Gets the folder the file is in.</summary>
    public string Folder => Path.GetDirectoryName(Source.FilePath) ?? string.Empty;

    /// <summary>Gets a value indicating whether Back can return to a place the reader jumped from.</summary>
    [Reactive]
    public partial bool CanGoBack { get; private set; }

    /// <summary>Gets a value indicating whether Forward can return to a place the reader went back from.</summary>
    [Reactive]
    public partial bool CanGoForward { get; private set; }

    /// <summary>Gets the page shown in the hover preview: the page the tab was on when the preview was prepared.</summary>
    [Reactive]
    public partial int PreviewPageIndex { get; private set; }

    /// <summary>Gets the height of the hover preview, following the page's shape.</summary>
    [Reactive]
    public partial double PreviewHeight { get; private set; }

    /// <summary>Gets the hover preview caption, for example "Page 3 of 40".</summary>
    [Reactive]
    public partial string PreviewCaption { get; private set; } = string.Empty;

    /// <summary>Gets the document title.</summary>
    [Reactive]
    public partial string Title { get; private set; }

    /// <summary>Gets a value indicating whether the document has been opened at least once.</summary>
    [Reactive]
    public partial bool IsLoaded { get; private set; }

    /// <summary>Gets the error shown when the document could not be opened.</summary>
    [Reactive]
    public partial string? ErrorMessage { get; private set; }

    /// <summary>Gets a value indicating whether a password is required.</summary>
    [Reactive]
    public partial bool NeedsPassword { get; private set; }

    /// <summary>Gets or sets the password being typed.</summary>
    [Reactive]
    public partial string PasswordEntry { get; set; } = string.Empty;

    /// <summary>Gets the number of pages.</summary>
    [Reactive]
    public partial int PageCount { get; private set; }

    /// <summary>Gets the zero based page shown in the middle of the viewport.</summary>
    [Reactive]
    public partial int CurrentPageIndex { get; private set; }

    /// <summary>Gets or sets the text in the page number box.</summary>
    [Reactive]
    public partial string PageEntry { get; set; } = string.Empty;

    /// <summary>Gets the last reported scroll position.</summary>
    public DocumentPosition Position { get; private set; }

    /// <summary>Gets the zoom factor, 1 being 100%.</summary>
    [Reactive(nameof(ZoomText))]
    public partial double Zoom { get; private set; } = 1;

    /// <summary>Gets the zoom as display text.</summary>
    public string ZoomText => string.Create(CultureInfo.CurrentCulture, $"{Math.Round(Zoom * Percent)}%");

    /// <summary>Gets or sets how the zoom is chosen.</summary>
    [Reactive]
    public partial ZoomMode ZoomMode { get; set; }

    /// <summary>Gets or sets the page arrangement.</summary>
    [Reactive]
    public partial PageLayoutMode LayoutMode { get; set; }

    /// <summary>Gets or sets a value indicating whether a text cursor is moved through the page with the arrow keys (F7).</summary>
    [Reactive]
    public partial bool IsCaretMode { get; set; }

    /// <summary>Gets or sets a value indicating whether pages are shown one at a time instead of scrolling continuously.</summary>
    [Reactive]
    public partial bool IsPageByPage { get; set; }

    /// <summary>Gets a value indicating whether the document is being presented: full screen, one page at a time, nothing else on screen.</summary>
    [Reactive(nameof(ShowsChrome))]
    public partial bool IsPresenting { get; private set; }

    /// <summary>
    /// Gets a value indicating whether read mode is on: the tool bars and sidebar are put away so the pages fill the
    /// window. A small bar offers the way back.
    /// </summary>
    [Reactive(nameof(ShowsChrome))]
    public partial bool IsReading { get; private set; }

    /// <summary>Gets a value indicating whether the tool bars are shown: not while presenting or reading.</summary>
    public bool ShowsChrome => !IsPresenting && !IsReading;

    /// <summary>Gets or sets the page rotation.</summary>
    [Reactive]
    public partial PageRotation Rotation { get; set; }

    /// <summary>Gets or sets the paper and ink colours pages are drawn with.</summary>
    [Reactive]
    public partial PageTone PageTone { get; set; } = PageTone.None;

    /// <summary>Gets or sets a value indicating whether the sidebar is shown.</summary>
    [Reactive]
    public partial bool SidebarVisible { get; set; }

    /// <summary>Gets or sets the sidebar panel.</summary>
    [Reactive(
        nameof(IsThumbnailsMode),
        nameof(IsOutlineMode),
        nameof(IsSearchMode),
        nameof(IsAnnotationsMode),
        nameof(IsAttachmentsMode),
        nameof(IsLayersMode))]
    public partial SidebarMode SidebarMode { get; set; }

    /// <summary>Gets or sets a value indicating whether the thumbnails panel is shown.</summary>
    public bool IsThumbnailsMode
    {
        get => SidebarMode == SidebarMode.Thumbnails;
        set => SetSidebarMode(value, SidebarMode.Thumbnails);
    }

    /// <summary>Gets or sets a value indicating whether the outline panel is shown.</summary>
    public bool IsOutlineMode
    {
        get => SidebarMode == SidebarMode.Outline;
        set => SetSidebarMode(value, SidebarMode.Outline);
    }

    /// <summary>Gets or sets a value indicating whether the search results panel is shown.</summary>
    public bool IsSearchMode
    {
        get => SidebarMode == SidebarMode.Search;
        set => SetSidebarMode(value, SidebarMode.Search);
    }

    /// <summary>Gets or sets a value indicating whether the annotations panel is shown.</summary>
    public bool IsAnnotationsMode
    {
        get => SidebarMode == SidebarMode.Annotations;
        set => SetSidebarMode(value, SidebarMode.Annotations);
    }

    /// <summary>Gets or sets a value indicating whether the attachments panel is shown.</summary>
    public bool IsAttachmentsMode
    {
        get => SidebarMode == SidebarMode.Attachments;
        set => SetSidebarMode(value, SidebarMode.Attachments);
    }

    /// <summary>Gets or sets a value indicating whether the layers panel is shown.</summary>
    public bool IsLayersMode
    {
        get => SidebarMode == SidebarMode.Layers;
        set => SetSidebarMode(value, SidebarMode.Layers);
    }

    /// <summary>Gets the document's layers.</summary>
    public LayersViewModel Layers => _layers ??= new(this);

    /// <summary>Gets the files embedded in the document.</summary>
    public AttachmentsViewModel Attachments => field ??= new(this);

    /// <summary>Gets the annotation state.</summary>
    public AnnotationsViewModel Annotations => _annotations ??= new(this);

    /// <summary>Gets the selection and undoable actions for the document's pages.</summary>
    public PageManagementViewModel Pages => _pages ??= new(this);

    /// <summary>Gets the name recorded as the author of new comments: the one chosen in Preferences, or empty for the user name.</summary>
    public string CommentAuthor => _services.Settings.CommentAuthor;

    /// <summary>Gets a value indicating whether fonts that cannot be saved in a PDF are offered for typing.</summary>
    public bool ShowPreviewOnlyFonts => _services.Settings.ShowPreviewOnlyFonts;

    /// <summary>Gets the measuring tool.</summary>
    public MeasureViewModel Measure => _measure ??= new(this);

    /// <summary>Gets the digital signature state.</summary>
    public SignaturesViewModel Signatures => field ??= new(this, _services);

    /// <summary>Gets the tab's text recognition, created on first use.</summary>
    public TextRecognitionViewModel TextRecognition => _textRecognition ??= new(this, _services);

    /// <summary>Gets Focus Mode, created when first used.</summary>
    public FocusModeViewModel FocusMode => _focusMode ??= new(this, _services);

    /// <summary>Gets the Read Aloud state, created when first used.</summary>
    public ReadAloudViewModel ReadAloud => _readAloud ??= new(this, _services);

    /// <summary>Gets the form filling state.</summary>
    public FormsViewModel Forms => field ??= new(this);

    /// <summary>Gets the Fill &amp; Sign state.</summary>
    public FillAndSignViewModel FillAndSign => field ??= new(this, _services);

    /// <summary>Gets a value indicating whether the document has annotations or form entries that are not saved.</summary>
    [Reactive]
    public partial bool HasUnsavedChanges { get; private set; }

    /// <summary>Gets the pages whose content changed, for example after annotating, so views redraw them.</summary>
    public AsObservableSignal<int> PageEdits { get; }

    /// <summary>Gets the text the view is asked to put on the clipboard.</summary>
    public AsObservableSignal<string> CopyRequests { get; }

    /// <summary>Gets the outline.</summary>
    [Reactive(nameof(HasOutline))]
    public partial IReadOnlyList<OutlineItemViewModel> Outline { get; private set; } = [];

    /// <summary>Gets a value indicating whether the document has an outline.</summary>
    public bool HasOutline => Outline.Count > 0;

    /// <summary>Gets the page thumbnails.</summary>
    [Reactive]
    public partial IReadOnlyList<ThumbnailItemViewModel> Thumbnails { get; private set; } = [];

    /// <summary>Gets or sets the selected thumbnail; selecting one scrolls to its page.</summary>
    [Reactive]
    public partial ThumbnailItemViewModel? SelectedThumbnail { get; set; }

    /// <summary>Gets or sets the selected outline entry; selecting one navigates to it.</summary>
    [Reactive]
    public partial OutlineItemViewModel? SelectedOutlineItem { get; set; }

    /// <summary>Gets the interaction showing the print preview; the output says whether to print.</summary>
    public Interaction<PrintPreviewViewModel, bool> PrintPreviewInteraction { get; } = new();

    /// <summary>Gets a message about the document that waits until dismissed, for example a failed save.</summary>
    [Reactive]
    public partial string? Notice { get; internal set; }

    /// <summary>Gets the interaction asking where to save a copy.</summary>
    public Interaction<string, string?> SaveAsInteraction { get; } = new();

    /// <summary>Gets a value indicating whether the file changed on disk and the user chose to be asked before reloading.</summary>
    [Reactive]
    public partial bool HasPendingReload { get; private set; }

    /// <summary>Opens the document with cancellable I/O and loads its structure on the UI thread.</summary>
    /// <param name="cancellationToken">Cancels the open and first-page work.</param>
    /// <returns>A task that completes inline when the document is ready.</returns>
    public ValueTask EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return IsLoaded && Source.IsOpen ? ValueTask.CompletedTask : LoadAsync(cancellationToken);
    }

    /// <summary>Starts cancellable loading for the tab that has become selected.</summary>
    public void StartSelectedWork()
    {
        if (_selectedWork is not null)
        {
            return;
        }

        var work = CancellationTokenSource.CreateLinkedTokenSource(RenderHub.Scheduler.ShutdownToken);
        _selectedWork = work;
        _ = LoadSelectedAsync(work.Token);
    }

    /// <summary>Cancels loading, searching and queued rendering after this tab loses selection.</summary>
    public void StopSelectedWork()
    {
        var work = _selectedWork;
        _selectedWork = null;
        if (work is null)
        {
            return;
        }

        _ = CancelAndDisposeAsync(work);
        _search?.CancelForTabSwitch();
        RenderHub.Scheduler.Invalidate(Source.Id);
    }

    /// <summary>Gets the open document, opening it if needed.</summary>
    /// <returns>The document, or <see langword="null"/> when it cannot be opened.</returns>
    public IDocument? TryGetDocument()
    {
        if (!Source.IsOpen)
        {
            return null;
        }

        try
        {
            return NeedsPassword || ErrorMessage is not null ? null : Source.Acquire();
        }
        catch (DocumentOpenException)
        {
            return null;
        }
    }

    /// <summary>Gets the document's pages in reading order, for reading aloud and Focus Mode.</summary>
    /// <returns>The reading order, or <see langword="null"/> when the document cannot describe its layout.</returns>
    public ReadingDocument? GetReadingDocument()
    {
        var document = TryGetDocument();
        var layout = ((document?.GetFeature(typeof(ITextLayoutSource))) as ITextLayoutSource);
        if (document is null || layout is null)
        {
            return null;
        }

        // Worker threads use this captured document, never the UI-owned document pool.
        Volatile.Write(ref _readingSource, document);
        if (_reading is null || _readingFor != Source.Id)
        {
            _reading = ReadingDocument.Create(ReadingSource, Source.PageSizes);
            _readingFor = Source.Id;
        }

        return _reading;
    }

    /// <summary>Forgets the worked out reading order after the page text changed, for example once text is recognised.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void InvalidateReading() => _reading?.Clear();

    /// <summary>Scrolls to a page.</summary>
    /// <param name="pageIndex">The zero based page.</param>
    public void GoToPage(int pageIndex)
    {
        if (PageCount == 0)
        {
            return;
        }

        NavigateTo(new(Math.Clamp(pageIndex, 0, PageCount - 1), null, 0));
    }

    /// <summary>Follows a link or outline target.</summary>
    /// <param name="target">The target.</param>
    public void Navigate(LinkTarget target)
    {
        switch (target.Kind)
        {
            case LinkTargetKind.Page:
                {
                    var area = target.Location is { } point ? new PageRect(point.X, point.Y, 0, 0) : (PageRect?)null;
                    NavigateTo(new(target.PageIndex, area, 0));
                    break;
                }

            case LinkTargetKind.Uri when Uri.TryCreate(target.Uri, UriKind.Absolute, out var uri):
                {
                    _uriRequests.OnNext(uri);
                    break;
                }

            case LinkTargetKind.OtherDocument or LinkTargetKind.LaunchFile or LinkTargetKind.EmbeddedDocument:
                {
                    NavigateToFile(target);
                    break;
                }

            default:
                {
                    break;
                }
        }
    }

    /// <summary>Records the current position in history and asks the canvas to scroll.</summary>
    /// <param name="request">The destination.</param>
    public void NavigateTo(NavigationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        History.Push(Position);
        UpdateHistoryState();
        _navigationRequests.OnNext(request);
    }

    /// <summary>Shows a page as a step through the document, such as the next page, without adding it to Back history.</summary>
    /// <param name="pageIndex">The page.</param>
    public void ShowPage(int pageIndex)
    {
        if (PageCount == 0)
        {
            return;
        }

        _navigationRequests.OnNext(new(Math.Clamp(pageIndex, 0, PageCount - 1), null, 0));
    }

    /// <summary>Scrolls an area of a page into view without adding it to Back history, for example a mark being placed.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="area">The area, in page space.</param>
    public void ShowArea(int pageIndex, PageRect area)
    {
        if ((uint)pageIndex < (uint)PageCount)
        {
            _navigationRequests.OnNext(new(pageIndex, area, 0));
        }
    }

    /// <summary>Called by the canvas as the view scrolls.</summary>
    /// <param name="position">The position at the top of the viewport.</param>
    /// <param name="currentPage">The page in the middle of the viewport.</param>
    public void ReportPosition(DocumentPosition position, int currentPage)
    {
        Position = position;
        CurrentPageIndex = currentPage;
    }

    /// <summary>Called by the canvas when a fit mode resolves to a zoom factor.</summary>
    /// <param name="zoom">The zoom.</param>
    public void ReportZoom(double zoom) => Zoom = zoom;

    /// <summary>Sets an explicit zoom, leaving fit modes.</summary>
    /// <param name="zoom">The zoom factor.</param>
    public void SetZoom(double zoom)
    {
        ZoomMode = ZoomMode.Free;
        Zoom = ZoomCalculator.Clamp(zoom);
    }

    /// <summary>Gets the display label of a page, preferring the document's own page labels.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <returns>The label.</returns>
    public string GetPageDisplay(int pageIndex) =>
        pageIndex >= 0 && pageIndex < Thumbnails.Count ? Thumbnails[pageIndex].Label : (pageIndex + 1).ToString(CultureInfo.CurrentCulture);

    /// <summary>Readies the hover preview, opening a cold document asynchronously and sizing it to the current page.</summary>
    /// <param name="cancellationToken">Cancels a pending open.</param>
    /// <returns>A task that completes when the preview is ready.</returns>
    public async Task PreparePreviewAsync(CancellationToken cancellationToken)
    {
        try
        {
            _ = await Source.AcquireAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (DocumentOpenException ex)
        {
            PreviewHeight = 0;
            PreviewCaption = ex.Error == DocumentOpenError.Password ? "Password protected" : ex.Message;
            return;
        }
        catch (Exception ex)
        {
            Trace.TraceError(ex.ToString());
            PreviewHeight = 0;
            PreviewCaption = "This document could not be opened";
            return;
        }

        PreviewPageIndex = Math.Max(0, CurrentPageIndex);
        var page = Math.Min(PreviewPageIndex, Source.PageCount - 1);
        if (page < 0)
        {
            PreviewHeight = 0;
            PreviewCaption = "No pages";
            return;
        }

        var size = Source.PageSizes[page];
        PreviewHeight = size.Width > 0 ? Math.Round(PreviewWidth * size.Height / size.Width) : PreviewWidth;
        PreviewCaption = $"Page {GetPageDisplay(page)} of {Source.PageCount}";
    }

    /// <summary>Drops every tile after layers were shown or hidden, so all pages redraw.</summary>
    public void OnLayersChanged()
    {
        RenderHub.Scheduler.Invalidate(Source.Id);
        RenderHub.RemoveDocumentTiles(Source.Id);
        _pageEdits.OnNext(-1);
    }

    /// <summary>Drops an edited page's tiles so it redraws, and tells views about it.</summary>
    /// <param name="pageIndex">The page.</param>
    public void OnPageEdited(int pageIndex)
    {
        // Tiles rendered before the edit may still be on their way; the scheduler drops them so they cannot be cached.
        RenderHub.Scheduler.Invalidate(Source.Id);
        RenderHub.Cache.RemovePage(Source.Id, pageIndex);
        HasUnsavedChanges = Source.HasUnsavedChanges;
        _pageEdits.OnNext(pageIndex);
    }

    /// <summary>Refreshes layout, thumbnails and navigation after changing page order or geometry.</summary>
    public void OnPageStructureChanged()
    {
        RenderHub.Scheduler.Invalidate(Source.Id);
        RenderHub.RemoveDocumentTiles(Source.Id);
        Source.RefreshStructure();
        _reading = null;
        _readingFor = -1;
        History.Clear();
        ReadPageStructure(Source.Acquire());
        CurrentPageIndex = Math.Clamp(CurrentPageIndex, 0, Math.Max(0, PageCount - 1));
        SelectedThumbnail = PageCount > 0 ? Thumbnails[CurrentPageIndex] : null;
        HasUnsavedChanges = Source.HasUnsavedChanges;
        _annotations?.RefreshItems();
        Signatures.Refresh();
        Attachments.Refresh();
        Layers.Refresh();
        Search.Refresh();
        _pageEdits.OnNext(-1);
        _documentChanges.OnNext(RxVoid.Default);
    }

    /// <summary>
    /// Saves the document. The new file is written next to the old one and then moved over it, so a failure never
    /// leaves a half written file behind.
    /// </summary>
    /// <param name="path">Where to save.</param>
    /// <returns><see langword="true"/> when saved.</returns>
    public bool Save(string path)
    {
        if (TryGetDocument()?.GetFeature(typeof(IAnnotationEditor)) is not IAnnotationEditor editor)
        {
            return false;
        }

        return ApplySaveOutcome(DocumentSave.Save(editor, Source, path));
    }

    /// <summary>Saves a document copy with cancellable output I/O, then replaces the destination atomically.</summary>
    /// <param name="path">The destination file.</param>
    /// <param name="cancellationToken">Cancels the snapshot and output write.</param>
    /// <returns>Whether the file was saved.</returns>
    public async Task<bool> SaveAsync(string path, CancellationToken cancellationToken)
    {
        if (NeedsPassword || ErrorMessage is not null)
        {
            return false;
        }

        IDocument document;
        try
        {
            document = await Source.AcquireAsync(cancellationToken);
        }
        catch (DocumentOpenException)
        {
            return false;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return false;
        }

        if (document.GetFeature(typeof(IAnnotationEditor)) is not IAnnotationEditor editor)
        {
            return false;
        }

        return ApplySaveOutcome(await DocumentSave.SaveAsync(editor, Source, path, cancellationToken).ConfigureAwait(true));
    }

    /// <summary>Turns read mode on or off, putting the sidebar away and bringing it back as it was.</summary>
    /// <param name="reading">Whether to read.</param>
    public void SetReading(bool reading)
    {
        if (reading == IsReading)
        {
            return;
        }

        if (reading)
        {
            _sidebarBeforeReading = SidebarVisible;
            SidebarVisible = false;
            IsReading = true;
            return;
        }

        IsReading = false;
        SidebarVisible = _sidebarBeforeReading;
    }

    /// <summary>Starts or ends presenting, keeping the tab's own view to restore afterwards.</summary>
    /// <param name="presenting">Whether to present.</param>
    public void SetPresenting(bool presenting)
    {
        if (presenting == IsPresenting)
        {
            return;
        }

        if (presenting)
        {
            _beforePresenting = new(ZoomMode, Zoom, IsPageByPage, SidebarVisible);
            SidebarVisible = false;
            IsPageByPage = true;
            ZoomMode = ZoomMode.FitPage;
            IsPresenting = true;
            return;
        }

        IsPresenting = false;
        var before = _beforePresenting;
        SidebarVisible = before.SidebarVisible;
        IsPageByPage = before.PageByPage;
        if (before.ZoomMode == ZoomMode.Free)
        {
            SetZoom(before.Zoom);
        }
        else
        {
            ZoomMode = before.ZoomMode;
        }
    }

    /// <summary>Closes the native document while the tab is in the background; it reopens on demand.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unload() => RenderHub.RemoveDocumentTiles(Source.Id);

    /// <inheritdoc/>
    public void Dispose()
    {
        StopSelectedWork();
        _subscriptions.Dispose();
        _focusMode?.Dispose();
        _readAloud?.Dispose();
        _textRecognition?.Dispose();
        _layers?.Dispose();
        _measure?.Dispose();
        _annotations?.Dispose();
        _pages?.Dispose();
        _fileWatch?.Dispose();
        Search.Dispose();
        _navigationRequests.Dispose();
        _uriRequests.Dispose();
        _fileLaunchRequests.Dispose();
        _documentChanges.Dispose();
        _pageEdits.Dispose();
        _copyRequests.Dispose();
        _selectAllRequests.Dispose();
        RenderHub.Scheduler.Invalidate(Source.Id);
        RenderHub.RemoveDocumentTiles(Source.Id);
        _ = CanvasClient.Advance();
        _ = ThumbnailClient.Advance();
    }

    /// <summary>Traces an error from a property subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Deletes a file, ignoring failures.</summary>
    /// <param name="path">The file.</param>
    /// <returns><see langword="true"/> when deleted.</returns>
    private static bool TryDelete(string path)
    {
        try
        {
            File.Delete(path);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>Cancels callbacks without blocking the UI thread, then releases the token source.</summary>
    /// <param name="work">The old selected-tab token source.</param>
    /// <returns>A task.</returns>
    private static async Task CancelAndDisposeAsync(CancellationTokenSource work)
    {
        try
        {
            await work.CancelAsync().ConfigureAwait(false);
        }
        finally
        {
            work.Dispose();
        }
    }

    /// <summary>Reflects the publication result in the tab and its file watcher.</summary>
    /// <param name="outcome">The write and replacement result.</param>
    /// <returns>Whether the destination was saved.</returns>
    private bool ApplySaveOutcome(DocumentSave.SaveOutcome outcome)
    {
        if (outcome.Saved)
        {
            _ = Interlocked.Exchange(ref _savedAt, Environment.TickCount64);
        }

        if (outcome.Error is { } error)
        {
            Notice = $"Could not save: {error}";
        }

        HasUnsavedChanges = Source.HasUnsavedChanges;
        return outcome.Saved;
    }

    /// <summary>Opens a cold document, reporting ordinary open errors in the tab.</summary>
    /// <param name="cancellationToken">Cancels a pending open.</param>
    /// <returns>A task.</returns>
    private async ValueTask LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            var document = await Source.AcquireAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            CompleteLoad(document);
        }
        catch (DocumentOpenException ex)
        {
            NeedsPassword = ex.Error == DocumentOpenError.Password;
            ErrorMessage = NeedsPassword ? null : ex.Message;
        }
    }

    /// <summary>Shows errors from selected-tab loading without leaving a fire-and-forget task unobserved.</summary>
    /// <param name="cancellationToken">The selected tab token.</param>
    /// <returns>A task.</returns>
    private async Task LoadSelectedAsync(CancellationToken cancellationToken)
    {
        try
        {
            await EnsureLoadedAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception error)
        {
            Trace.TraceError(error.ToString());
        }
    }

    /// <summary>Publishes a successful open and loads its initial navigation state.</summary>
    /// <param name="document">The ready document.</param>
    private void CompleteLoad(IDocument document)
    {
        NeedsPassword = false;
        ErrorMessage = null;
        if (!IsLoaded)
        {
            OnFirstLoad(document);
        }
    }

    /// <summary>Gets layout and optional tagged structure from the captured document, from any thread.</summary>
    /// <returns>Both reading capabilities, or <see langword="null"/> when the document is closed or has no layout.</returns>
    private ReadingSources? ReadingSource()
    {
        var document = Volatile.Read(ref _readingSource);
        return document is null || document.IsDisposed
            || ((document.GetFeature(typeof(ITextLayoutSource))) as ITextLayoutSource) is not { } characters
            ? null
            : new ReadingSources(
            characters,
            ((document.GetFeature(typeof(ITaggedStructureSource))) as ITaggedStructureSource));
    }

    /// <summary>Reads document structure after the first successful open.</summary>
    /// <param name="document">The newly opened document.</param>
    private void OnFirstLoad(IDocument document)
    {
        ReadPageStructure(document);
        Signatures.Refresh();
        Attachments.Refresh();
        Layers.Refresh();
        RefreshOptimize(document);
        RefreshRedaction(document);
        _pages?.Refresh();
        if (!IsSecondaryView)
        {
            _services.RecentDocuments.Add(FilePath);
            _ = CheckContentAsync(document);
            CheckRepairs(document);
            WatchFile();
        }

        IsLoaded = true;
        _documentChanges.OnNext(RxVoid.Default);
    }

    /// <summary>Reads page geometry and navigation without reopening the document or replacing its edits.</summary>
    /// <param name="document">The open document.</param>
    private void ReadPageStructure(IDocument document)
    {
        var sizes = Source.PageSizes;
        var title = Source.Metadata?.Title;
        Title = string.IsNullOrWhiteSpace(title) ? FileName : title;
        Outline = OutlineItemViewModel.Create(Source.Outline);

        var thumbnails = new ThumbnailItemViewModel[sizes.Length];
        for (var i = 0; i < sizes.Length; i++)
        {
            var size = sizes[i];
            var label = document.GetPageLabel(i) ?? (i + 1).ToString(CultureInfo.CurrentCulture);
            var height = size.Width > 0 ? ThumbnailWidth * size.Height / size.Width : ThumbnailWidth;
            thumbnails[i] = new(i, label, ThumbnailWidth, height);
        }

        Thumbnails = thumbnails;
        PageCount = sizes.Length;
        PageEntry = GetPageDisplay(CurrentPageIndex);
    }

    /// <summary>
    /// Shows the print preview, then sends the file it built straight to the chosen printer, saves it as a PDF or opens
    /// the desktop's print dialog, saying what happened in the notice.
    /// </summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task PrintAsync()
    {
        using var preview = new PrintPreviewViewModel(this, _services);
        if (!await PrintPreviewInteraction.Handle(preview).ToTask().ConfigureAwait(true))
        {
            return;
        }

        var destination = preview.Destination;
        var label = preview.SelectedTarget?.Label ?? string.Empty;
        var job = preview.JobOptions;
        var file = preview.TakePrintFile();
        if (destination == PrintDestination.SystemDialog)
        {
            _ = file is not null && TryDelete(file);
            await PrintWithSystemDialogAsync().ConfigureAwait(true);
            return;
        }

        if (file is null)
        {
            return;
        }

        try
        {
            if (destination == PrintDestination.SaveAsPdf)
            {
                await SavePrintAsPdfAsync(file).ConfigureAwait(true);
                return;
            }

            var outcome = await _services.Platform.Printer.SubmitAsync(file, FileName, job, CancellationToken.None).ConfigureAwait(true);
            Notice = outcome.Sent ? $"Sent to {label}." : $"Could not print: {outcome.Detail}";
        }
        finally
        {
            _ = TryDelete(file);
        }
    }

    /// <summary>Hands the whole document, as it looks now, straight to the desktop's print dialog.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task PrintWithSystemDialogAsync()
    {
        var printer = _services.Platform.Printer;
        if (!printer.IsAvailable)
        {
            Notice = "Printing is not available on this desktop. Choose Save as PDF instead.";
            return;
        }

        if (((TryGetDocument())?.GetFeature(typeof(IPageExporter)) as IPageExporter) is not
            {
            } exporter || Source.PageCount == 0)
        {
            return;
        }

        var file = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-print-{Guid.NewGuid():N}.pdf");
        try
        {
            var pages = new int[Source.PageCount];
            for (var i = 0; i < pages.Length; i++)
            {
                pages[i] = i;
            }

            bool written;
            await using (var stream = File.Create(file))
            {
                written = exporter.ExportPages(pages, SheetLayout.Default, stream);
            }

            if (!written)
            {
                Notice = "Could not prepare the document for printing.";
            }
            else if (!await printer.PrintAsync(file, FileName, CancellationToken.None).ConfigureAwait(true))
            {
                Notice = "The print dialog could not be opened.";
            }
        }
        finally
        {
            _ = TryDelete(file);
        }
    }

    /// <summary>Asks where to save the printed pages as a PDF and copies them there.</summary>
    /// <param name="file">The printed pages.</param>
    /// <returns>A task.</returns>
    private async Task SavePrintAsPdfAsync(string file)
    {
        var path = await SaveAsInteraction.Handle(FileName).ToTask().ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        try
        {
            File.Copy(file, path, true);
        }
        catch (IOException ex)
        {
            Notice = $"Could not save: {ex.Message}";
        }
        catch (UnauthorizedAccessException ex)
        {
            Notice = $"Could not save: {ex.Message}";
        }
    }

    /// <summary>Asks where to save a copy and saves it there.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task SaveAsAsync()
    {
        var cancellationToken = SelectedWorkToken;
        var path = await SaveAsInteraction.Handle(FileName).ToTask().ConfigureAwait(true);
        if (!string.IsNullOrEmpty(path))
        {
            _ = await SaveAsync(path, cancellationToken).ConfigureAwait(true);
        }
    }

    /// <summary>Asks the view to put text on the clipboard.</summary>
    /// <param name="text">The text.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CopyText(string text) => _copyRequests.OnNext(text);

    /// <summary>Zooms in one step.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ZoomIn() => SetZoom(ZoomCalculator.ZoomIn(Zoom));

    /// <summary>Zooms out one step.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ZoomOut() => SetZoom(ZoomCalculator.ZoomOut(Zoom));

    /// <summary>Resets the zoom to 100%.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ZoomReset() => SetZoom(1);

    /// <summary>Fits the page width to the window.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FitWidth() => ZoomMode = ZoomMode.FitWidth;

    /// <summary>Fits the whole page to the window.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FitPage() => ZoomMode = ZoomMode.FitPage;

    /// <summary>Rotates the pages anti-clockwise.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RotateLeft() => Rotation = Rotation.CounterClockwise;

    /// <summary>Rotates the pages clockwise.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RotateRight() => Rotation = Rotation.Clockwise;

    /// <summary>Goes to the next page, or the next pair of pages when two are shown side by side.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NextPage() => ShowPage(PageRows.GetNextRowPage(CurrentPageIndex, PageCount, LayoutMode));

    /// <summary>Goes to the previous page, or the previous pair of pages when two are shown side by side.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PreviousPage() => ShowPage(PageRows.GetPreviousRowPage(CurrentPageIndex, LayoutMode));

    /// <summary>Goes to the first page.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void FirstPage() => GoToPage(0);

    /// <summary>Goes to the last page.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void LastPage() => GoToPage(PageCount - 1);

    /// <summary>Shows or hides the sidebar.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ToggleSidebar() => SidebarVisible = !SidebarVisible;

    /// <summary>Opens the find bar.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Find() => Search.Open();

    /// <summary>Hides the "file changed" bar without reloading.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DismissReload() => HasPendingReload = false;

    /// <summary>Saves the document over its own file.</summary>
    /// <returns>A task that reports whether the file was saved.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task<bool> Save() => SaveAsync(FilePath, SelectedWorkToken);

    /// <summary>Turns caret navigation on or off.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ToggleCaretMode() => IsCaretMode = !IsCaretMode;

    /// <summary>Starts or ends presenting.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Present() => SetPresenting(!IsPresenting);

    /// <summary>Turns read mode on or off.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ReadMode() => SetReading(!IsReading);

    /// <summary>Ends presenting.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void StopPresenting() => SetPresenting(false);

    /// <summary>Dismisses the <see cref="Notice"/>.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DismissNotice() => Notice = null;

    /// <summary>Follows the current page: updates the page box and selects the page's thumbnail.</summary>
    /// <param name="pageIndex">The current page.</param>
    private void OnCurrentPageChanged(int pageIndex)
    {
        PageEntry = GetPageDisplay(pageIndex);
        SelectedThumbnail = pageIndex >= 0 && pageIndex < Thumbnails.Count ? Thumbnails[pageIndex] : null;
        if (_pages is { SelectedPages.Count: <= 1 } pages && SelectedThumbnail is not null)
        {
            pages.SelectPages([pageIndex]);
        }

        Forms.OnPageShown(pageIndex);
        CheckRepairsOfOpenDocument();
    }

    /// <summary>Refreshes the annotation list when its panel is opened.</summary>
    /// <param name="mode">The sidebar panel.</param>
    private void OnSidebarModeChanged(SidebarMode mode)
    {
        if (mode == SidebarMode.Annotations)
        {
            Annotations.RefreshItems();
        }
    }

    /// <summary>Scrolls to the page of a newly selected thumbnail.</summary>
    /// <param name="thumbnail">The selected thumbnail.</param>
    private void OnThumbnailSelected(ThumbnailItemViewModel? thumbnail)
    {
        if (thumbnail is not null && thumbnail.PageIndex != CurrentPageIndex)
        {
            GoToPage(thumbnail.PageIndex);
        }
    }

    /// <summary>Navigates to a newly selected outline entry.</summary>
    /// <param name="item">The selected entry.</param>
    private void OnOutlineItemSelected(OutlineItemViewModel? item)
    {
        if (item is not null)
        {
            Navigate(item.Target);
        }
    }

    /// <summary>Returns to where the reader was before the last jump, like a web browser's Back.</summary>
    [ReactiveCommand(CanExecute = nameof(_canGoBack))]
    private void GoBack()
    {
        if (History.TryGoBack(Position, out var target))
        {
            _navigationRequests.OnNext(new(target.PageIndex, null, target.OffsetFraction));
        }

        UpdateHistoryState();
    }

    /// <summary>Returns to where the reader was before going back.</summary>
    [ReactiveCommand(CanExecute = nameof(_canGoForward))]
    private void GoForward()
    {
        if (History.TryGoForward(Position, out var target))
        {
            _navigationRequests.OnNext(new(target.PageIndex, null, target.OffsetFraction));
        }

        UpdateHistoryState();
    }

    /// <summary>Publishes whether Back and Forward have somewhere to go.</summary>
    private void UpdateHistoryState()
    {
        CanGoBack = History.CanGoBack;
        CanGoForward = History.CanGoForward;
    }

    /// <summary>Jumps to the page typed in the page box, accepting page labels or numbers.</summary>
    [ReactiveCommand]
    private void GoToPageEntry()
    {
        var entry = PageEntry.Trim();
        for (var i = 0; i < Thumbnails.Count; i++)
        {
            if (!string.Equals(Thumbnails[i].Label, entry, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            GoToPage(i);
            return;
        }

        if (int.TryParse(entry, NumberStyles.Integer, CultureInfo.CurrentCulture, out var number))
        {
            GoToPage(number - 1);
        }
        else
        {
            PageEntry = GetPageDisplay(CurrentPageIndex);
        }
    }

    /// <summary>Retries opening with the typed password.</summary>
    /// <returns>A task for the open attempt.</returns>
    [ReactiveCommand]
    private async Task SubmitPasswordAsync()
    {
        Source.Password = PasswordEntry;
        PasswordEntry = string.Empty;
        NeedsPassword = false;
        var token = SelectedWorkToken;
        try
        {
            await EnsureLoadedAsync(token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
        }
    }

    /// <summary>Sets the zoom from a percentage such as "150".</summary>
    /// <param name="text">The percentage.</param>
    [ReactiveCommand]
    private void SetZoom(string text)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) && percent > 0)
        {
            SetZoom(percent / Percent);
        }
    }

    /// <summary>Sets the layout from a <see cref="PageLayoutMode"/> name.</summary>
    /// <param name="text">The name.</param>
    [ReactiveCommand]
    private void SetLayout(string text)
    {
        if (Enum.TryParse<PageLayoutMode>(text, out var mode))
        {
            LayoutMode = mode;
        }
    }

    /// <summary>Switches the sidebar panel when a toggle is checked.</summary>
    /// <param name="isChecked">Whether the toggle is checked.</param>
    /// <param name="mode">The panel.</param>
    private void SetSidebarMode(bool isChecked, SidebarMode mode)
    {
        if (isChecked)
        {
            SidebarMode = mode;
        }
    }
}
