// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Diagnostics;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Media;
using HyperPdf.Core.Navigation;
using HyperPdf.Core.Printing;
using HyperPdf.Core.Reading;
using HyperPdf.Core.Rendering;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>
/// One open document. A tab is cheap until it is first shown: the native document is opened on demand through the
/// <see cref="DocumentPool"/> and may be closed again while the tab is in the background.
/// </summary>
[DebuggerDisplay("DocumentTabViewModel: {FileName}")]
public sealed partial class DocumentTabViewModel : ReactiveObject, IDisposable
{
    /// <summary>The width of tab hover previews, in device independent pixels.</summary>
    private const double PreviewSize = 180;

    /// <summary>The width of sidebar thumbnails in device independent pixels.</summary>
    private const double ThumbnailWidth = 120;

    /// <summary>Percent multiplier.</summary>
    private const double Percent = 100;

    /// <summary>The services.</summary>
    private readonly AppServices _services;

    /// <summary>Follows changes other programs make to the open file.</summary>
    private readonly TabFileWatch _fileWatch = new();

    /// <summary>Owns everything the tab holds: its subscriptions, its parts and its work, released in the order they were added.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>Whether Back has somewhere to go.</summary>
    private readonly IObservable<bool> _canGoBack;

    /// <summary>Whether Forward has somewhere to go.</summary>
    private readonly IObservable<bool> _canGoForward;

    /// <summary>Whether the sidebar was shown before read mode put it away.</summary>
    private bool _sidebarBeforeReading;

    /// <summary>The rounded zoom percent <see cref="ZoomText"/> shows, so a zoom frame that keeps it builds no new text.</summary>
    private int _zoomPercent = (int)Percent;

    /// <summary>The commands that save a changed copy, once used.</summary>
    private TabCopyActionSet? _copies;

    /// <summary>Links to other files, once used.</summary>
    private TabFileLinks? _links;

    /// <summary>The Read Aloud state, once used.</summary>
    private ReadAloudViewModel? _readAloud;

    /// <summary>Focus Mode, once used.</summary>
    private FocusModeViewModel? _focusMode;

    /// <summary>The layers panel, once used.</summary>
    private LayersViewModel? _layers;

    /// <summary>The tags panel, once used.</summary>
    private TagsViewModel? _tags;

    /// <summary>The colour separation bar, once used.</summary>
    private PageSeparationViewModel? _separations;

    /// <summary>The measuring tool, once used.</summary>
    private MeasureViewModel? _measure;

    /// <summary>The annotation state, created on first use.</summary>
    private AnnotationsViewModel? _annotations;

    /// <summary>The bookmark editing state, created on first use.</summary>
    private OutlineEditViewModel? _outlineEditing;

    /// <summary>Whether selecting an outline entry is part of an edit, so it must not move the reader.</summary>
    private bool _keepingOutlineSelection;

    /// <summary>The document's text in reading order, created on first use.</summary>
    private TabReading? _reading;

    /// <summary>The search panel, created only when first used.</summary>
    private SearchViewModel? _search;

    /// <summary>How the tab looked before presenting.</summary>
    private PresentationState _beforePresenting;

    /// <summary>Initializes a new instance of the <see cref="DocumentTabViewModel"/> class.</summary>
    /// <param name="source">The document source.</param>
    /// <param name="services">The application services.</param>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> or <paramref name="services"/> is <see langword="null"/>.</exception>
    public DocumentTabViewModel(DocumentSource source, AppServices services)
    {
        ArgumentNullException.ThrowIfNull(source);
        Source = source;
        _services = services ?? throw new ArgumentNullException(nameof(services));
        FileName = Path.GetFileName(source.FilePath);
        Title = FileName;
        WindowName = FileName;
        ZoomMode = services.Settings.DefaultZoomMode;
        LayoutMode = services.Settings.DefaultLayoutMode;
        SidebarVisible = services.Settings.ShowSidebar;
        PageTone = services.CurrentTheme.PageTone;
        IsCaretMode = services.Settings.KeepKeyboardCursor;
        _canGoBack = this.WhenChanged(static vm => vm.CanGoBack);
        _canGoForward = this.WhenChanged(static vm => vm.CanGoForward);
        Notices = new(source);

        // Closing the tab first closes the windows saving a copy, then stops the tab's own work, such as text
        // recognition and form actions, and its selected work, and lets go of the tiles; the parts made later are
        // released after these.
        var lifetime = new CancellationDisposable();
        LifetimeToken = lifetime.Token;
        _subscriptions.Add(Scope.Create(CloseCopyWindows));
        _subscriptions.Add(lifetime);

        // While the tab is selected, its open, search and rendering work runs under one token; deselecting the tab or
        // closing it ends that work.
        _subscriptions.Add(this.WhenChanged(static t => t.IsSelected)
            .DistinctUntilChanged()
            .SwitchMap(selected => selected ? Signal.Using(StartSelectedWork, static work => work.Loading) : Signal.Never<RxVoid>())
            .SubscribeSafeErrors(ErrorTrace.Write));
        _subscriptions.Add(Scope.Create(ReleaseTiles));
        _subscriptions.Add(this.WhenChanged(static x => x.Zoom).Skip(1).SubscribeSafe(ShowZoomText, ErrorTrace.Write));

        // The first value of each property is its current state, which needs no reaction.
        _subscriptions.Add(this.WhenChanged(static x => x.CurrentPageIndex).Skip(1).SubscribeSafe(FollowCurrentPage, ErrorTrace.Write));

        // The annotation list is read again each time its panel opens.
        _subscriptions.Add(this.WhenChanged(static x => x.SidebarMode).Skip(1).SubscribeSafe(_ => ShowSidebarPane(), ErrorTrace.Write));

        // Picking a thumbnail scrolls to its page, unless the thumbnail is being selected to follow the page shown.
        // The filter reads the tab's own page: a constructor cannot hand 'this' to a state-passing operator.
        _subscriptions.Add(this.WhenChanged(static x => x.SelectedThumbnail)
            .Skip(1)
            .KeepNotNull()
            .Where(thumbnail => thumbnail.PageIndex != CurrentPageIndex)
            .SubscribeSafe(thumbnail => GoToPage(thumbnail.PageIndex), ErrorTrace.Write));

        // Picking a bookmark goes to it, unless the bookmark is being selected as part of an edit. The filter reads the
        // tab's own flag: a constructor cannot hand 'this' to a state-passing operator.
        _subscriptions.Add(this.WhenChanged(static x => x.SelectedOutlineItem)
            .Skip(1)
            .KeepNotNull()
            .Where(_ => !_keepingOutlineSelection)
            .SubscribeSafe(item => Navigate(item.Target), ErrorTrace.Write));
        _isTextFormatShownHelper = this.WhenChanged(static vm => vm.Annotations.IsAnnotating, static vm => vm.FillAndSign.IsActive, static (annotating, filling) => annotating || filling)
            .ToProperty(this, static vm => vm.IsTextFormatShown);
        _subscriptions.Add(_isTextFormatShownHelper);

        // Each part that draws on the pages keeps its latest change. The changed tiles are dropped before the version
        // moves on, so views that redraw for the new version never draw a tile from before the change.
        _contentVersionHelper = Signal.Merge(
                this.WhenChanged(static vm => vm.LastPageChange),
                this.WhenChanged(static vm => vm.Annotations.LastPageChange),
                this.WhenChanged(static vm => vm.Forms.LastPageChange),
                this.WhenChanged(static vm => vm.Layers.LastPageChange),
                this.WhenChanged(static vm => vm.TextRecognitionInUse).KeepNotNull().SwitchMap(static recognition => recognition.WhenChanged(static r => r.LastPageChange)))
            .Where(static change => change.IsChange)
            .Do(DropChangedTiles)
            .Scan(0, static (version, _) => version + 1)
            .ToProperty(this, static vm => vm.ContentVersion);
        _subscriptions.Add(_contentVersionHelper);

        // Page management counts its edits to the page order and geometry; each one reads the pages again.
        _subscriptions.Add(this.WhenChanged(static vm => vm.PagesInUse)
            .KeepNotNull()
            .SwitchMap(static pages => pages.WhenChanged(static p => p.StructureVersion).Skip(1))
            .SubscribeSafe(_ => RereadPageStructure(), ErrorTrace.Write));

        // The file watcher reports on its own thread; the tab answers on the UI thread.
        _subscriptions.Add(_fileWatch.Changes
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeSafe(_ => CheckChangedFile(), ErrorTrace.Write));
    }

    /// <summary>Gets the width of the hover preview of a tab.</summary>
    public static double PreviewWidth => PreviewSize;

    /// <summary>Gets a value indicating whether the text format row is shown: with the Annotate and Fill &amp; Sign tools.</summary>
    [ObservableAsProperty]
    public partial bool IsTextFormatShown { get; }

    /// <summary>
    /// Gets the place the reader last asked to go, which the pages scroll to. Each request is a new value, so asking for
    /// the same place twice scrolls there twice.
    /// </summary>
    [Reactive]
    public partial NavigationRequest Destination { get; private set; }

    /// <summary>Gets a number that changes each time the document's pages are read again, after it loads, reloads or its pages change, so views lay them out again.</summary>
    [Reactive]
    public partial int LayoutVersion { get; private set; }

    /// <summary>Gets a number that changes each time what is drawn on the pages changes, for example after annotating, so views redraw them.</summary>
    [ObservableAsProperty]
    public partial int ContentVersion { get; }

    /// <summary>Gets the tab's own latest change to what the pages show, such as a reopened document or rearranged pages.</summary>
    [Reactive]
    public partial PageChange LastPageChange { get; private set; }

    /// <summary>Gets the request to open a web or mail link with the desktop's default app.</summary>
    public Interaction<Uri, RxVoid> OpenLinkInteraction { get; } = new();

    /// <summary>Gets the document source.</summary>
    public DocumentSource Source { get; }

    /// <summary>Gets the render hub.</summary>
    public RenderHub RenderHub => _services.RenderHub;

    /// <summary>Gets a value indicating whether the reader asked for movement to be reduced, so views jump instead of easing.</summary>
    public bool ReduceMotion => _services.CurrentTheme.ReduceMotion;

    /// <summary>Gets the identity this view's tiles carry; the second view of a split document keeps its tiles apart from the first.</summary>
    public int TileId => TabTiles.IdFor(Source.Id, IsSecondaryView);

    /// <summary>Gets the render client used by the page canvas.</summary>
    public RenderClient CanvasClient { get; } = new();

    /// <summary>Gets the render client used by sidebar thumbnails.</summary>
    public RenderClient ThumbnailClient { get; } = new();

    /// <summary>Gets the token that is cancelled when the tab is disposed; work on the tab's document that is not tied to selection uses it.</summary>
    public CancellationToken LifetimeToken { get; }

    /// <summary>Gets the token for work requested while this tab is selected.</summary>
    public CancellationToken SelectedWorkToken { get; private set; } = CancellationToken.None;

    /// <summary>Gets or sets a value indicating whether the window shows this tab, which runs its open, search and rendering work.</summary>
    [Reactive]
    public partial bool IsSelected { get; set; }

    /// <summary>Gets the navigation history.</summary>
    public NavigationHistory History { get; } = new();

    /// <summary>Gets the search state, created on first use.</summary>
    public SearchViewModel Search => _search ??= Owned(new SearchViewModel(this));

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

    /// <summary>Gets the name the window shows for the document: its own title when it asks for that and the reader allows it, otherwise the file name.</summary>
    [Reactive]
    public partial string WindowName { get; private set; }

    /// <summary>Gets a value indicating whether the document has been opened at least once.</summary>
    [Reactive]
    public partial bool IsLoaded { get; private set; }

    /// <summary>Gets the error shown when the document could not be opened.</summary>
    [Reactive]
    public partial string? ErrorMessage { get; private set; }

    /// <summary>Gets a value indicating whether a password is required.</summary>
    [Reactive]
    public partial bool NeedsPassword { get; private set; }

    /// <summary>Gets a value indicating whether opening requires a recipient certificate.</summary>
    [Reactive]
    public partial bool NeedsCertificate { get; private set; }

    /// <summary>Gets or sets the password being typed.</summary>
    [Reactive]
    public partial string PasswordEntry { get; set; } = string.Empty;

    /// <summary>Gets or sets the PKCS #12 certificate file chosen to unlock this document.</summary>
    [Reactive]
    public partial string CertificatePathEntry { get; set; } = string.Empty;

    /// <summary>Gets or sets the certificate file password being typed.</summary>
    [Reactive]
    public partial string CertificatePasswordEntry { get; set; } = string.Empty;

    /// <summary>Gets the reason a certificate open failed, or null.</summary>
    [Reactive]
    public partial string? CertificateError { get; private set; }

    /// <summary>Gets the request to choose a PKCS #12 certificate file.</summary>
    public Interaction<RxVoid, string?> CertificateBrowseInteraction { get; } = new();

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
    [Reactive]
    public partial double Zoom { get; private set; } = 1;

    /// <summary>Gets the zoom as display text; it changes only when the rounded percent does, not on every easing frame.</summary>
    public string ZoomText
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = ZoomLabel.Format((int)Percent);

    /// <summary>Gets or sets how the zoom is chosen.</summary>
    [Reactive]
    public partial ZoomMode ZoomMode { get; set; }

    /// <summary>Gets or sets the page arrangement.</summary>
    [Reactive]
    public partial PageLayoutMode LayoutMode { get; set; }

    /// <summary>Gets or sets the direction pages are laid out and scrolled in.</summary>
    [Reactive]
    public partial ScrollMode ScrollMode { get; set; }

    /// <summary>Gets or sets a value indicating whether two page spreads read right to left, as the document asks and the reader allows.</summary>
    [Reactive]
    public partial bool RightToLeft { get; set; }

    /// <summary>Gets or sets the print defaults the document asks for and the reader allows, or <see langword="null"/> when the print window starts with the viewer's own.</summary>
    public PrintDefaults? PrintDefaults { get; set; }

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
        nameof(IsLayersMode),
        nameof(IsTagsMode),
        nameof(IsPartsMode),
        nameof(IsMediaMode))]
    public partial SidebarMode SidebarMode { get; set; }

    /// <summary>Gets or sets a value indicating whether the thumbnails panel is shown.</summary>
    public bool IsThumbnailsMode { get => SidebarMode == SidebarMode.Thumbnails; set => SetSidebarMode(value, SidebarMode.Thumbnails); }

    /// <summary>Gets or sets a value indicating whether the outline panel is shown.</summary>
    public bool IsOutlineMode { get => SidebarMode == SidebarMode.Outline; set => SetSidebarMode(value, SidebarMode.Outline); }

    /// <summary>Gets or sets a value indicating whether the search results panel is shown.</summary>
    public bool IsSearchMode { get => SidebarMode == SidebarMode.Search; set => SetSidebarMode(value, SidebarMode.Search); }

    /// <summary>Gets or sets a value indicating whether the annotations panel is shown.</summary>
    public bool IsAnnotationsMode { get => SidebarMode == SidebarMode.Annotations; set => SetSidebarMode(value, SidebarMode.Annotations); }

    /// <summary>Gets or sets a value indicating whether the attachments panel is shown.</summary>
    public bool IsAttachmentsMode { get => SidebarMode == SidebarMode.Attachments; set => SetSidebarMode(value, SidebarMode.Attachments); }

    /// <summary>Gets or sets a value indicating whether the layers panel is shown.</summary>
    public bool IsLayersMode { get => SidebarMode == SidebarMode.Layers; set => SetSidebarMode(value, SidebarMode.Layers); }

    /// <summary>Gets or sets a value indicating whether the tags panel is shown.</summary>
    public bool IsTagsMode { get => SidebarMode == SidebarMode.Tags; set => SetSidebarMode(value, SidebarMode.Tags); }

    /// <summary>Gets or sets a value indicating whether the parts and articles panel is shown.</summary>
    public bool IsPartsMode { get => SidebarMode == SidebarMode.Parts; set => SetSidebarMode(value, SidebarMode.Parts); }

    /// <summary>Gets or sets a value indicating whether the media panel is shown.</summary>
    public bool IsMediaMode { get => SidebarMode == SidebarMode.Media; set => SetSidebarMode(value, SidebarMode.Media); }

    /// <summary>Gets the document's sound and video.</summary>
    public DocumentMediaViewModel Media => field ??= new(this);

    /// <summary>Gets the document's parts and article threads.</summary>
    public DocumentPartsViewModel Parts => field ??= Owned(new DocumentPartsViewModel(this));

    /// <summary>Gets the document's layers.</summary>
    public LayersViewModel Layers => _layers ??= Owned(new LayersViewModel(this));

    /// <summary>Gets the document's tags and the edits to them.</summary>
    public TagsViewModel Tags => _tags ??= Owned(new TagsViewModel(this));

    /// <summary>Gets the colour separation details of the current page, for a preseparated document.</summary>
    public PageSeparationViewModel Separations => _separations ??= Owned(new PageSeparationViewModel(this));

    /// <summary>Gets the files embedded in the document.</summary>
    public AttachmentsViewModel Attachments => field ??= new(this);

    /// <summary>Gets what happens as each page appears while presenting; made when the page view first shows the tab.</summary>
    public PagePresentationViewModel PagePresentation => field ??= Owned(new PagePresentationViewModel(this, _services));

    /// <summary>Gets the bookmark editing state.</summary>
    public OutlineEditViewModel OutlineEditing => _outlineEditing ??= Owned(new OutlineEditViewModel(this));

    /// <summary>Gets the annotation state.</summary>
    public AnnotationsViewModel Annotations => _annotations ??= Owned(new AnnotationsViewModel(this));

    /// <summary>Gets the selection and undoable actions for the document's pages.</summary>
    public PageManagementViewModel Pages => PagesInUse ??= Owned(new PageManagementViewModel(this));

    /// <summary>Gets the name recorded as the author of new comments: the one chosen in Preferences, or empty for the user name.</summary>
    public string CommentAuthor => _services.Settings.CommentAuthor;

    /// <summary>Gets a value indicating whether fonts that cannot be saved in a PDF are offered for typing.</summary>
    public bool ShowPreviewOnlyFonts => _services.Settings.ShowPreviewOnlyFonts;

    /// <summary>Gets the measuring tool.</summary>
    public MeasureViewModel Measure => _measure ??= Owned(new MeasureViewModel(this));

    /// <summary>Gets the digital signature state.</summary>
    public SignaturesViewModel Signatures => field ??= new(this, _services);

    /// <summary>Gets the tab's text recognition, created on first use.</summary>
    public TextRecognitionViewModel TextRecognition => TextRecognitionInUse ??= Owned(new TextRecognitionViewModel(this, _services));

    /// <summary>Gets Focus Mode, created when first used.</summary>
    public FocusModeViewModel FocusMode => _focusMode ??= Owned(new FocusModeViewModel(this, _services));

    /// <summary>Gets the Read Aloud state, created when first used.</summary>
    public ReadAloudViewModel ReadAloud => _readAloud ??= Owned(new ReadAloudViewModel(this, _services));

    /// <summary>Gets the form filling state.</summary>
    public FormsViewModel Forms => field ??= Owned(new FormsViewModel(this));

    /// <summary>Gets the Fill &amp; Sign state.</summary>
    public FillAndSignViewModel FillAndSign => field ??= new(this, _services);

    /// <summary>Gets the commands that save a changed copy, such as an optimised, PDF/A, flattened or protected one.</summary>
    public TabCopyActionSet Copies => _copies ??= Owned(new TabCopyActionSet(this));

    /// <summary>Gets the links to other files: PDFs open in a tab, other files with the desktop's app after asking.</summary>
    public TabFileLinks Links => _links ??= new(this);

    /// <summary>Gets the notices about content that cannot be shown and about a repaired file.</summary>
    public TabNoticesViewModel Notices { get; }

    /// <summary>Gets the page tools: select text, hand, zoom to area, snapshot and auto-scroll.</summary>
    public PageToolsViewModel PageTools { get; } = new();

    /// <summary>Gets how another PDF is opened in a tab, at a zero-based page; set by the window.</summary>
    public Action<string, int>? OpenDocument { get; init; }

    /// <summary>Gets how the window splits or unsplits the view of this document; set by the window.</summary>
    public Action<DocumentTabViewModel>? ToggleSplitView { get; init; }

    /// <summary>Gets a value indicating whether this is the second view of a split, which leaves file watching and messages to the first.</summary>
    public bool IsSecondaryView { get; init; }

    /// <summary>Gets a value indicating whether the window shows this document split into two views.</summary>
    [Reactive]
    public partial bool IsSplitView { get; internal set; }

    /// <summary>Gets a value indicating whether the document has annotations or form entries that are not saved.</summary>
    [Reactive]
    public partial bool HasUnsavedChanges { get; private set; }

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

    /// <summary>Gets printing: the print preview, the desktop's print dialog and saving the printed pages as a PDF.</summary>
    public TabPrintingViewModel Printing => field ??= new(this);

    /// <summary>Gets a message about the document that waits until dismissed, for example a failed save.</summary>
    [Reactive]
    public partial string? Notice { get; set; }

    /// <summary>Gets the interaction asking where to save a copy.</summary>
    public Interaction<string, string?> SaveAsInteraction { get; } = new();

    /// <summary>Gets a value indicating whether the file changed on disk and the user chose to be asked before reloading.</summary>
    [Reactive]
    public partial bool HasPendingReload { get; private set; }

    /// <summary>Gets the application services, for the parts of the tab that need settings or desktop services.</summary>
    public AppServices Services => _services;

    /// <summary>Gets the tint and sharpness a background tab keeps until it is selected.</summary>
    internal TabSettingsSlot PendingSettings { get; } = new();

    /// <summary>Gets how media plays on this computer.</summary>
    internal MediaPlaybackFactory MediaPlayback => _services.MediaPlayback;

    /// <summary>Gets the document's text in reading order, which Read Aloud and Focus Mode share.</summary>
    internal TabReading Reading => _reading ??= new(this);

    /// <summary>Gets the text recognition state once it has been used, or <see langword="null"/> before.</summary>
    [Reactive]
    internal partial TextRecognitionViewModel? TextRecognitionInUse { get; private set; }

    /// <summary>Gets the page management state once it has been used, or <see langword="null"/> before.</summary>
    [Reactive]
    internal partial PageManagementViewModel? PagesInUse { get; private set; }

    /// <summary>Opens the document with cancellable I/O and loads its structure on the UI thread.</summary>
    /// <param name="cancellationToken">Cancels the open and first-page work.</param>
    /// <returns>A task that completes inline when the document is ready.</returns>
    public ValueTask EnsureLoadedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return IsLoaded && Source.IsOpen ? ValueTask.CompletedTask : LoadAsync(cancellationToken);
    }

    /// <summary>Applies a new tint over fillable form fields to the open document and draws its pages again.</summary>
    /// <param name="highlight">The tint.</param>
    public void ApplyFormHighlight(FormHighlight highlight)
    {
        if (!IsLoaded || TryGetDocument() is not { } document || DocumentFeature<IFormHighlight>.Get(document) is not { } tinted || tinted.Highlight == highlight)
        {
            return;
        }

        tinted.Highlight = highlight;
        TabTiles.InvalidateBoth(RenderHub, Source.Id);
    }

    /// <summary>Applies new page sharpness choices to the open document and draws its pages again.</summary>
    /// <param name="sharpness">The choices.</param>
    public void ApplyRenderSharpness(RenderSharpness sharpness)
    {
        if (!IsLoaded || TryGetDocument() is not { } document || DocumentFeature<IRenderSharpness>.Get(document) is not { } sharp || sharp.Sharpness == sharpness)
        {
            return;
        }

        sharp.Sharpness = sharpness;
        TabTiles.InvalidateBoth(RenderHub, Source.Id);
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
            return NeedsPassword || NeedsCertificate || ErrorMessage is not null ? null : Source.Acquire();
        }
        catch (DocumentOpenException)
        {
            return null;
        }
    }

    /// <summary>Gets the document's pages in reading order, for reading aloud and Focus Mode.</summary>
    /// <returns>The reading order, or <see langword="null"/> when the document cannot describe its layout.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadingDocument? GetReadingDocument() => Reading.Get();

    /// <summary>Forgets the worked out reading order after the page text changed, for example once text is recognised.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void InvalidateReading() => _reading?.Invalidate();

    /// <summary>Scrolls to a page.</summary>
    /// <param name="pageIndex">The zero based page.</param>
    public void GoToPage(int pageIndex)
    {
        if (PageCount == 0)
        {
            return;
        }

        NavigateTo(Math.Clamp(pageIndex, 0, PageCount - 1), null, 0);
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
                    NavigateTo(target.PageIndex, area, 0);
                    break;
                }

            case LinkTargetKind.Uri when Uri.TryCreate(target.Uri, UriKind.Absolute, out var uri):
                {
                    // The handler finishes once the desktop has the link, so the subscription ends by itself.
                    _ = OpenLinkInteraction.WhenHandled(uri).SubscribeSafeErrors(ErrorTrace.Write);
                    break;
                }

            case LinkTargetKind.OtherDocument or LinkTargetKind.LaunchFile or LinkTargetKind.EmbeddedDocument:
                {
                    Links.NavigateToFile(target);
                    break;
                }

            case LinkTargetKind.AttachedFile:
                {
                    _ = Attachments.Reveal(target);
                    break;
                }

            default:
                {
                    break;
                }
        }
    }

    /// <summary>Records the current position in history and moves <see cref="Destination"/> to a new place, which the pages scroll to.</summary>
    /// <param name="pageIndex">The zero based destination page.</param>
    /// <param name="target">An area of the page to bring into view, or <see langword="null"/> for the top of the page.</param>
    /// <param name="offsetFraction">The vertical position within the page from 0 to 1, used when <paramref name="target"/> is null.</param>
    public void NavigateTo(int pageIndex, PageRect? target, double offsetFraction)
    {
        History.Push(Position);
        UpdateHistoryState();
        Destination = Destination.Next(pageIndex, target, offsetFraction);
    }

    /// <summary>Shows a page as a step through the document, such as the next page, without adding it to Back history.</summary>
    /// <param name="pageIndex">The page.</param>
    public void ShowPage(int pageIndex)
    {
        if (PageCount == 0)
        {
            return;
        }

        Destination = Destination.Next(Math.Clamp(pageIndex, 0, PageCount - 1), null, 0);
    }

    /// <summary>Scrolls an area of a page into view without adding it to Back history, for example a mark being placed.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <param name="area">The area, in page space.</param>
    public void ShowArea(int pageIndex, PageRect area)
    {
        if ((uint)pageIndex < (uint)PageCount)
        {
            Destination = Destination.Next(pageIndex, area, 0);
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
            PreviewCaption = ex.Error switch
            {
                DocumentOpenError.Password => "Password protected",
                DocumentOpenError.Certificate => "Certificate required",
                _ => ex.Message,
            };
            return;
        }
        catch (Exception ex)
        {
            AppLog.Error(ex);
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

    /// <summary>Shows the bookmarks of an edited document without moving the reader.</summary>
    /// <param name="items">The rebuilt entries.</param>
    /// <param name="selectId">The object number of the entry to select, or 0 for none.</param>
    public void ShowEditedOutline(IReadOnlyList<OutlineItemViewModel> items, int selectId)
    {
        _keepingOutlineSelection = true;
        Outline = items;
        SelectedOutlineItem = OutlineItemViewModel.Find(items, selectId);
        _keepingOutlineSelection = false;
        HasUnsavedChanges = Source.HasUnsavedChanges;
    }

    /// <summary>
    /// Saves the document. The new file is written next to the old one and then moved over it, so a failure never
    /// leaves a half written file behind.
    /// </summary>
    /// <param name="path">Where to save.</param>
    /// <returns><see langword="true"/> when saved.</returns>
    public bool Save(string path)
    {
        if (DocumentFeature<IAnnotationEditor>.Get(TryGetDocument()) is not { } editor)
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

        if (DocumentFeature<IAnnotationEditor>.Get(document) is not { } editor)
        {
            return false;
        }

        // The document's own actions bracket the save: they may change fields before the file is written.
        _ = await Forms.RunDocumentEventAsync(FormDocumentEvent.WillSave).ConfigureAwait(true);
        var saved = ApplySaveOutcome(await DocumentSave.SaveAsync(editor, Source, path, cancellationToken).ConfigureAwait(true));
        if (saved)
        {
            _ = await Forms.RunDocumentEventAsync(FormDocumentEvent.DidSave).ConfigureAwait(true);
        }

        return saved;
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
        _beforePresenting.Restore(this);
    }

    /// <summary>Closes the native document while the tab is in the background; it reopens on demand.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unload() => RenderHub.RemoveDocumentTiles(TileId);

    /// <summary>Reloads the document from disk, keeping the current page.</summary>
    /// <returns>A task for the selected tab's reload.</returns>
    [ReactiveCommand]
    public async Task ReloadAsync()
    {
        HasPendingReload = false;
        var page = CurrentPageIndex;
        var token = SelectedWorkToken;
        if (_copies?.CloseWindows() == true)
        {
            Notice = "The file changed on disk and was reloaded, so the window for saving a copy was closed.";
        }

        TabTiles.DropBoth(RenderHub, Source.Id);
        Source.Reload();
        _reading?.Reset();
        IsLoaded = false;
        LayoutVersion++;
        if (!IsSelected)
        {
            return;
        }

        try
        {
            await EnsureLoadedAsync(token).ConfigureAwait(true);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            return;
        }

        if (!IsLoaded)
        {
            return;
        }

        GoToPage(page);
        Search.Refresh();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _subscriptions.Dispose();
        Media.Dispose();
    }

    /// <summary>Reloads a changed file, or offers to, depending on the user's choice. Unsaved edits are never discarded silently.</summary>
    internal void CheckChangedFile()
    {
        var response = _fileWatch.Check(FilePath, _services.Settings.FileChangeAction, Source.HasUnsavedChanges);
        if (response == FileChangeResponse.Ask)
        {
            HasPendingReload = true;
        }
        else if (response == FileChangeResponse.Reload)
        {
            _ = ReloadChangedFileAsync();
        }
    }

    /// <summary>Reflects the publication result in the tab and its file watcher.</summary>
    /// <param name="outcome">The write and replacement result.</param>
    /// <returns>Whether the destination was saved.</returns>
    private bool ApplySaveOutcome(in DocumentSaveOutcome outcome)
    {
        if (outcome.Saved)
        {
            _fileWatch.MarkSaved();
        }

        if (outcome.Error is { } error)
        {
            Notice = $"Could not save: {error}";
        }
        else if (SaveText.Notice(outcome) is { } notice)
        {
            Notice = notice;
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

            // Reads the separation details with async I/O, so the page structure read that follows finds them loaded.
            if (DocumentFeature<ISeparationSource>.Get(document) is { } separations)
            {
                _ = await separations.GetSeparationsAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();
            CompleteLoad(document);
        }
        catch (DocumentOpenException ex)
        {
            NeedsPassword = ex.Error == DocumentOpenError.Password;
            NeedsCertificate = ex.Error == DocumentOpenError.Certificate;
            CertificateError = NeedsCertificate ? ex.Message : null;
            ErrorMessage = NeedsPassword || NeedsCertificate ? null : ex.Message;
        }
    }

    /// <summary>Shows errors from selected-tab loading without leaving a fire-and-forget task unobserved.</summary>
    /// <param name="cancellationToken">The selected tab token.</param>
    /// <returns>A task that completes once the document is open, or could not be.</returns>
    private async Task<RxVoid> LoadSelectedAsync(CancellationToken cancellationToken)
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
            ErrorTrace.Write(error);
        }

        return RxVoid.Default;
    }

    /// <summary>Starts the selected tab's work: its token, a search the switch interrupted, and opening the document.</summary>
    /// <returns>The work, which ends when disposed.</returns>
    private SelectedWork StartSelectedWork()
    {
        // Settings a background tab kept reach its document before its first draw.
        if (PendingSettings.TryTake(out var pending))
        {
            ApplyFormHighlight(pending.Highlight);
            ApplyRenderSharpness(pending.Sharpness);
        }

        var work = new SelectedWork(this);
        SelectedWorkToken = work.Token;
        _search?.ResumeAfterTabSwitch();
        return work;
    }

    /// <summary>Ends the selected tab's work: searching stops and queued rendering is dropped.</summary>
    private void EndSelectedWork()
    {
        SelectedWorkToken = CancellationToken.None;
        _search?.CancelForTabSwitch();
        RenderHub.Scheduler.Invalidate(TileId);
    }

    /// <summary>Publishes a successful open and loads its initial navigation state.</summary>
    /// <param name="document">The ready document.</param>
    private void CompleteLoad(IDocument document)
    {
        NeedsPassword = false;
        NeedsCertificate = false;
        CertificateError = null;
        ErrorMessage = null;
        if (!IsLoaded)
        {
            ReadFirstOpen(document);
            return;
        }

        TakeUpReopenedDocument(document);
    }

    /// <summary>
    /// Takes up a document the pool closed while the tab was in the background and opened again. Tiles prepared from the
    /// closed document can never draw, so they are dropped, and views draw the page again.
    /// </summary>
    /// <param name="document">The reopened document.</param>
    private void TakeUpReopenedDocument(IDocument document)
    {
        Copies.Refresh(document);
        _search?.ResumeAfterTabSwitch();
        LastPageChange = LastPageChange.Next(PageChange.AllPages);
    }

    /// <summary>Reads document structure after the first successful open.</summary>
    /// <param name="document">The newly opened document.</param>
    private void ReadFirstOpen(IDocument document)
    {
        // Requirements are checked before any action can run (ISO 32000-2 §12.11.1).
        Notices.CheckRequirements(document);
        ReadPageStructure(document);
        Signatures.Refresh();
        Attachments.Refresh();
        Layers.Refresh();
        Parts.Refresh();
        Media.Refresh();
        Copies.Refresh(document);
        PagesInUse?.Refresh();
        if (!IsSecondaryView)
        {
            _services.RecentDocuments.Add(FilePath);
            _ = Notices.CheckContentAsync(document, Attachments.Items.Count);
            Notices.CheckRepairs(document);
            Notices.CheckIntegrity(Source.Metadata);
            _fileWatch.Start(FilePath);
        }

        if (Source.Metadata?.WrappedFileName is { Length: > 0 } wrappedName)
        {
            Notice = $"This file contains an encrypted document named {wrappedName}. Open it from Attachments.";
        }

        TabOpenHints.Apply(this, _services.Settings);
        IsLoaded = true;
        LayoutVersion++;
    }

    /// <summary>Refreshes layout, thumbnails and navigation after page management changed the page order or geometry.</summary>
    private void RereadPageStructure()
    {
        TabTiles.DropBoth(RenderHub, Source.Id);
        Source.RefreshStructure();
        _reading?.Reset();
        History.Clear();
        ReadPageStructure(Source.Acquire());
        CurrentPageIndex = Math.Clamp(CurrentPageIndex, 0, Math.Max(0, PageCount - 1));
        SelectedThumbnail = PageCount > 0 ? Thumbnails[CurrentPageIndex] : null;
        HasUnsavedChanges = Source.HasUnsavedChanges;
        _annotations?.InvalidateItems();
        ShowSidebarPane();
        Signatures.Refresh();
        Attachments.Refresh();
        Layers.Refresh();
        Parts.Refresh();
        Media.Refresh();
        _tags?.Refresh();
        Search.Refresh();
        LastPageChange = LastPageChange.Next(PageChange.AllPages);
        LayoutVersion++;
    }

    /// <summary>Reads page geometry and navigation without reopening the document or replacing its edits.</summary>
    /// <param name="document">The open document.</param>
    private void ReadPageStructure(IDocument document)
    {
        var sizes = Source.PageSizes;
        var title = Source.Metadata?.Title;
        Title = string.IsNullOrWhiteSpace(title) ? FileName : title;
        WindowName = TabOpenHints.TitleOf(Source.Metadata, FileName, _services.Settings);
        Outline = OutlineItemViewModel.Create(Source.Outline, _services.Settings.IgnoreOutlineColours);

        // A separation page's thumbnail names its colourant, so it is not mistaken for a page of its own.
        Separations.Load(DocumentFeature<ISeparationSource>.Get(document)?.GetSeparations() ?? DocumentSeparations.Empty, CurrentPageIndex);
        var thumbnails = new ThumbnailItemViewModel[sizes.Length];
        for (var i = 0; i < sizes.Length; i++)
        {
            var size = sizes[i];
            var label = document.GetPageLabel(i) ?? (i + 1).ToString(CultureInfo.CurrentCulture);
            var height = size.Width > 0 ? ThumbnailWidth * size.Height / size.Width : ThumbnailWidth;
            thumbnails[i] = new(i, label, ThumbnailWidth, height) { Colorant = Separations.ColorantOf(i) };
        }

        Thumbnails = thumbnails;
        PageCount = sizes.Length;
        PageEntry = GetPageDisplay(CurrentPageIndex);
    }

    /// <summary>Asks where to save a copy and saves it there.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task SaveAsAsync() => _ = await AskAndSaveAsync().ConfigureAwait(true);

    /// <summary>Asks where to save a copy, suggesting a PDF name for a picture opened as a document, and saves it there.</summary>
    /// <returns>A task that reports whether the file was saved.</returns>
    private async Task<bool> AskAndSaveAsync()
    {
        var cancellationToken = SelectedWorkToken;
        var suggested = PictureFiles.IsPictureFile(FileName) ? PictureFiles.SuggestedPdfName(FileName) : FileName;
        var path = await SaveAsInteraction.Handle(suggested).ToTask().ConfigureAwait(true);
        return !string.IsNullOrEmpty(path) && await SaveAsync(path, cancellationToken).ConfigureAwait(true);
    }

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
    private void NextPage() => ShowPage(PageRows.GetNextPage(CurrentPageIndex, PageCount, LayoutMode, ScrollMode));

    /// <summary>Goes to the previous page, or the previous pair of pages when two are shown side by side.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PreviousPage() => ShowPage(PageRows.GetPreviousPage(CurrentPageIndex, LayoutMode, ScrollMode));

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

    /// <summary>Saves the document over its own file; a picture opened as a document is never overwritten, so it asks where to save a PDF.</summary>
    /// <returns>A task that reports whether the file was saved.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task<bool> Save() => PictureFiles.IsPictureFile(FilePath) ? AskAndSaveAsync() : SaveAsync(FilePath, SelectedWorkToken);

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

    /// <summary>Rebuilds the zoom label when the rounded percent of the zoom changed.</summary>
    /// <param name="zoom">The zoom factor.</param>
    private void ShowZoomText(double zoom)
    {
        if (ZoomLabel.TryChange(zoom, ref _zoomPercent, out var text))
        {
            ZoomText = text;
        }
    }

    /// <summary>Brings the annotation or layer list up to date when its panel is shown.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ShowSidebarPane() => TabSidebarPane.Show(this);

    /// <summary>
    /// Follows the current page: updates the page box, selects the page's thumbnail, shows its separation and checks
    /// for repairs. Forms and layers follow the page themselves.
    /// </summary>
    /// <param name="pageIndex">The current page.</param>
    private void FollowCurrentPage(int pageIndex)
    {
        PageEntry = GetPageDisplay(pageIndex);
        SelectedThumbnail = pageIndex >= 0 && pageIndex < Thumbnails.Count ? Thumbnails[pageIndex] : null;
        if (PagesInUse is { SelectedPages.Count: <= 1 } pages && SelectedThumbnail is not null && !(pages.SelectedPages.Count == 1 && pages.SelectedPages[0] == pageIndex))
        {
            pages.SelectPages([pageIndex]);
        }

        Separations.ShowPage(pageIndex);
        CheckRepairsOfOpenDocument();
    }

    /// <summary>Drops the tiles a page change made stale, so they are drawn again, and notes whether the document now has unsaved changes.</summary>
    /// <param name="change">The change.</param>
    private void DropChangedTiles(PageChange change)
    {
        if (change.PageIndex == PageChange.AllPages)
        {
            TabTiles.DropBoth(RenderHub, Source.Id);
        }
        else
        {
            // Tiles rendered before the edit may still be on their way; the scheduler drops them so they cannot be cached.
            TabTiles.InvalidateBoth(RenderHub, Source.Id);
            RenderHub.Cache.RemovePage(Source.Id, ~Source.Id, change.PageIndex);
        }

        HasUnsavedChanges = Source.HasUnsavedChanges;
    }

    /// <summary>Returns to where the reader was before the last jump, like a web browser's Back.</summary>
    [ReactiveCommand(CanExecute = nameof(_canGoBack))]
    private void GoBack()
    {
        if (History.TryGoBack(Position, out var target))
        {
            Destination = Destination.Next(target.PageIndex, null, target.OffsetFraction);
        }

        UpdateHistoryState();
    }

    /// <summary>Returns to where the reader was before going back.</summary>
    [ReactiveCommand(CanExecute = nameof(_canGoForward))]
    private void GoForward()
    {
        if (History.TryGoForward(Position, out var target))
        {
            Destination = Destination.Next(target.PageIndex, null, target.OffsetFraction);
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

    /// <summary>Chooses a certificate file through the desktop picker.</summary>
    /// <returns>A task for the picker.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task BrowseCertificateAsync() => DocumentCertificateBrowsing.BrowseAsync(this);

    /// <summary>Retries opening with the selected recipient certificate.</summary>
    /// <returns>A task for the open attempt.</returns>
    [ReactiveCommand]
    private async Task SubmitCertificateAsync()
    {
        if (string.IsNullOrWhiteSpace(CertificatePathEntry))
        {
            CertificateError = "Choose a certificate file first.";
            return;
        }

        Source.UseCertificate(CertificatePathEntry, CertificatePasswordEntry);
        CertificatePasswordEntry = string.Empty;
        CertificateError = null;
        NeedsCertificate = false;
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

    /// <summary>Sets the scroll mode from a <see cref="ScrollMode"/> name.</summary>
    /// <param name="text">The name.</param>
    [ReactiveCommand]
    private void SetScrollMode(string text)
    {
        if (Enum.TryParse<ScrollMode>(text, out var mode))
        {
            ScrollMode = mode;
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

    /// <summary>Reloads on the UI thread while observing errors from the file watcher callback.</summary>
    /// <returns>A task.</returns>
    private async Task ReloadChangedFileAsync()
    {
        try
        {
            await ReloadAsync().ConfigureAwait(true);
        }
        catch (Exception error)
        {
            AppLog.Error(error);
        }
    }

    /// <summary>Closes the windows that save a copy, when any were opened.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CloseCopyWindows() => _ = _copies?.CloseWindows();

    /// <summary>Hands a part made on first use to the tab, which releases it when the tab closes.</summary>
    /// <typeparam name="TPart">The part's type.</typeparam>
    /// <param name="part">The new part.</param>
    /// <returns>The same part.</returns>
    private TPart Owned<TPart>(TPart part)
        where TPart : IDisposable
    {
        _subscriptions.Add(part);
        return part;
    }

    /// <summary>Lets go of the tab's tiles. Putting the second view away leaves the first view's tiles alone; closing the document drops both views'.</summary>
    private void ReleaseTiles()
    {
        RenderHub.Scheduler.Invalidate(TileId);
        RenderHub.RemoveDocumentTiles(TileId);
        if (!IsSecondaryView)
        {
            TabTiles.DropBoth(RenderHub, Source.Id);
        }

        _ = CanvasClient.Advance();
        _ = ThumbnailClient.Advance();
    }

    /// <summary>Checks an open document again for repairs, because some damage shows only once a page is read or drawn.</summary>
    private void CheckRepairsOfOpenDocument()
    {
        if (!Notices.RepairNoticeShown && IsLoaded && Source.IsOpen)
        {
            Notices.CheckRepairs(Source.Acquire());
        }
    }

    /// <summary>Splits the view in two, or puts the second view away.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SplitView() => ToggleSplitView?.Invoke(this);

    /// <summary>The work of a selected tab: one token for its open, search and rendering work, cancelled when it ends.</summary>
    /// <param name="tab">The tab.</param>
    [DebuggerDisplay("SelectedWork: {Token.IsCancellationRequested}")]
    private sealed class SelectedWork(DocumentTabViewModel tab) : IDisposable
    {
        /// <summary>Cancels the work when the tab stops being selected.</summary>
        private readonly CancellationTokenSource _work = CancellationTokenSource.CreateLinkedTokenSource(tab.RenderHub.Scheduler.ShutdownToken);

        /// <summary>Gets the token the work runs under.</summary>
        internal CancellationToken Token => _work.Token;

        /// <summary>Gets the opening of the document, which lasts as long as the tab is selected.</summary>
        internal IObservable<RxVoid> Loading => Signal.FromAsync(() => tab.LoadSelectedAsync(Token)).Concat(Signal.Never<RxVoid>());

        /// <inheritdoc/>
        public void Dispose()
        {
            tab.EndSelectedWork();
            _work.Cancel();
            _work.Dispose();
        }
    }
}
