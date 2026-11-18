// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Annotations;
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
public sealed partial class DocumentTabViewModel : ReactiveObject, IDisposable, ILocalisationChildren
{
    /// <summary>The width of tab hover previews, in device independent pixels.</summary>
    private const double PreviewSize = 180;

    /// <summary>The width of sidebar thumbnails in device independent pixels.</summary>
    private const double ThumbnailWidth = 120;

    /// <summary>Percent multiplier.</summary>
    private const double Percent = 100;

    /// <summary>How long the current page must stay put before the thumbnail follows it.</summary>
    private static readonly TimeSpan ThumbnailDelay = TimeSpan.FromMilliseconds(100);

    /// <summary>The services.</summary>
    private readonly AppServices _services;

    /// <summary>Owns everything the tab holds: its subscriptions, its parts and its work, released in the order they were added.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>Whether Back has somewhere to go.</summary>
    private readonly IObservable<bool> _canGoBack;

    /// <summary>Whether Forward has somewhere to go.</summary>
    private readonly IObservable<bool> _canGoForward;

    /// <summary>Whether the sidebar was shown before read mode put it away.</summary>
    private bool _sidebarBeforeReading;

    /// <summary>The shared signals over the tab that stay connected while the tab is in use, by what names each.</summary>
    private Dictionary<object, KeptSignal>? _keptSignals;

    /// <summary>Gives each part made on first use that a window can wait for, those made already and then each as it is made.</summary>
    private ReplaySignal<object>? _partMakes;

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

    /// <summary>Edit mode, made when first used.</summary>
    private PageContentEditViewModel? _contentEdit;

    /// <summary>The tools that add decorations to the pages, made on first use.</summary>
    private PageDecorationViewModel? _decorations;

    /// <summary>The form design, made on first use.</summary>
    private FormDesignViewModel? _formDesign;

    /// <summary>The colour separation bar, once used.</summary>
    private PageSeparationViewModel? _separations;

    /// <summary>The measuring tool, once used.</summary>
    private MeasureViewModel? _measure;

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

        // Closing the tab first closes the windows saving a copy, then stops its own work (text recognition, form
        // actions, selected work) and lets go of the tiles; the parts made later are released after these.
        var lifetime = new CancellationDisposable();
        LifetimeToken = lifetime.Token;
        _subscriptions.Add(Scope.Create(CloseCopyWindows));
        _subscriptions.Add(lifetime);

        // While the tab is selected, its open, search and rendering work runs under one token; deselecting the tab or
        // closing it ends that work.
        _subscriptions.Add(
            this.WhenChanged(static t => t.IsSelected).DistinctUntilChanged().SwitchMap(
                    selected => selected
                        ? Signal.Using(
                    StartSelectedWork,
                    static work => work.Loading)
                        : Signal.Never<RxVoid>()).SubscribeSafeErrors(ErrorTrace.Write));
        _subscriptions.Add(Scope.Create(ReleaseTiles));
        _subscriptions.Add(this.WhenChanged(static x => x.Zoom).Skip(1).SubscribeSafe(ShowZoomText, ErrorTrace.Write));

        // The first value of each property is its current state, which needs no reaction.
        _subscriptions.Add(this.WhenChanged(static x => x.CurrentPageIndex).Skip(1).SubscribeSafe(FollowCurrentPage, ErrorTrace.Write));
        _subscriptions.Add(this.WhenChanged(static x => x.CurrentPageIndex).Skip(1).Throttle(ThumbnailDelay, RxSchedulers.MainThreadScheduler).SubscribeSafe(SelectThumbnailOf, ErrorTrace.Write));

        // The annotation list is read again each time its panel opens.
        _subscriptions.Add(this.WhenChanged(static x => x.SidebarMode).Skip(1).SubscribeSafe(_ => ShowSidebarPane(), ErrorTrace.Write));

        // Picking a thumbnail scrolls to its page, unless the thumbnail is being selected to follow the page shown.
        // The filter reads the tab's own page: a constructor cannot hand 'this' to a state-passing operator.
        _subscriptions.Add(
            this.WhenChanged(static x => x.SelectedThumbnail).Skip(1).KeepNotNull().Where(thumbnail => thumbnail.PageIndex != CurrentPageIndex)
            .SubscribeSafe(thumbnail => GoToPage(thumbnail.PageIndex), ErrorTrace.Write));

        // Picking a bookmark goes to it, unless the bookmark is being selected as part of an edit. The filter reads the
        // tab's own flag: a constructor cannot hand 'this' to a state-passing operator.
        _subscriptions.Add(this.WhenChanged(static x => x.SelectedOutlineItem)
            .Skip(1)
            .KeepNotNull()
            .Where(_ => !_keepingOutlineSelection)
            .SubscribeSafe(item => Navigate(item.Target), ErrorTrace.Write));
        _subscriptions.Add(_isTextFormatShownHelper = this.AnnotatingChanges.CombineLatest(this.FillingChanges, static (a, f) => a || f)
            .ToProperty(this, static vm => vm.IsTextFormatShown));

        // Each part that draws on the pages keeps its latest change. The changed tiles are dropped before the version
        // moves on, so views that redraw for the new version never draw a tile from before the change.
        _contentVersionHelper = Signal.Merge(
            this.WhenChanged(static vm => vm.LastPageChange),
            this.WhenChanged(static vm => vm.AnnotationsInUse).KeepNotNull().SwitchMap(static marks => marks.WhenChanged(static m => m.LastPageChange)),
            this.WhenChanged(static vm => vm.Forms.LastPageChange),
            MadeParts.OfType<LayersViewModel>().Take(1).SwitchMap(static layers => layers.WhenChanged(static l => l.LastPageChange)),
            this.WhenChanged(static vm => vm.TextRecognitionInUse).KeepNotNull().SwitchMap(static recognition => recognition.WhenChanged(static r => r.LastPageChange)))
            .Where(static change => change.IsChange)
            .Do(DropChangedTiles)
            .Scan(0, static (version, _) => version + 1)
            .ToProperty(this, static vm => vm.ContentVersion);
        _subscriptions.Add(_contentVersionHelper);

        // Page management counts its edits to the page order and geometry; each one reads the pages again.
        _subscriptions.Add(
            this.WhenChanged(static vm => vm.PagesInUse).KeepNotNull().SwitchMap(
                static pages => pages.WhenChanged(
                    static p => p
                    .StructureVersion).Skip(1)).SubscribeSafe(_ => RereadPageStructure(), ErrorTrace.Write));

        // The file watcher reports on its own thread; the tab answers on the UI thread.
        _subscriptions.Add(FileWatch.Changes.ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(_ => CheckChangedFile(), ErrorTrace.Write));
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
    public SearchViewModel Search => _search ?? Made(out _search, Owned(new SearchViewModel(this)));

    /// <summary>Gets the search once it has been used, or <see langword="null"/> before. Reading it does not make it.</summary>
    public SearchViewModel? SearchIfMade => _search;

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
    public string PreviewCaption { get => _localizedPreviewCaption.Value ?? string.Empty; private set => SetPreviewCaption(LocalizedMessage.Literal(value)); }

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
    public string? ErrorMessage { get => _localizedErrorMessage.Value; private set => SetErrorMessage(LocalizedMessage.Literal(value)); }

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
    public string? CertificateError { get => _localizedCertificateError.Value; private set => SetCertificateError(LocalizedMessage.Literal(value)); }

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
    public DocumentMediaViewModel Media => _media ?? Loaded(Made(out _media, new(this)), static made => made.Refresh());

    /// <summary>Gets the films and sounds once they have been used, or <see langword="null"/> before. Reading it does not make them.</summary>
    public DocumentMediaViewModel? MediaIfMade => _media;

    /// <summary>Gets the document's parts and article threads.</summary>
    public DocumentPartsViewModel Parts => _parts ?? Loaded(Made(out _parts, Owned(new DocumentPartsViewModel(this))), static made => made.Refresh());

    /// <summary>Gets the document's layers.</summary>
    public LayersViewModel Layers => _layers ?? Loaded(Made(out _layers, Owned(new LayersViewModel(this))), static made => made.Refresh());

    /// <summary>Gets the document's tags and the edits to them.</summary>
    public TagsViewModel Tags => _tags ?? Made(out _tags, Owned(new TagsViewModel(this)));

    /// <summary>Gets the colour separation details of the current page, for a preseparated document.</summary>
    public PageSeparationViewModel Separations => _separations ??= Owned(new PageSeparationViewModel(this));

    /// <summary>Gets the files embedded in the document.</summary>
    public AttachmentsViewModel Attachments => _attachments ??= new(this);

    /// <summary>Gets what happens as each page appears while presenting; made when the page view first shows the tab.</summary>
    public PagePresentationViewModel PagePresentation => _pagePresentation ??= Owned(new PagePresentationViewModel(this, _services));

    /// <summary>Gets the bookmark editing state.</summary>
    public OutlineEditViewModel OutlineEditing => _outlineEditing ?? Made(out _outlineEditing, Owned(new OutlineEditViewModel(this)));

    /// <summary>Gets the annotation state.</summary>
    public AnnotationsViewModel Annotations => AnnotationsInUse ?? Announced(AnnotationsInUse = Owned(new AnnotationsViewModel(this)));

    /// <summary>Gets Edit mode: picking, moving, retyping and removing the objects a page is made of, on the page.</summary>
    public PageContentEditViewModel ContentEdit => _contentEdit ?? Made(out _contentEdit, Owned(new PageContentEditViewModel(this)));

    /// <summary>Gets the page content editing once it has been used, or <see langword="null"/> before. Reading it does not make it.</summary>
    public PageContentEditViewModel? ContentEditIfMade => _contentEdit;

    /// <summary>Gets the tools that add watermarks, headers, footers, page numbers, a background and a crop to the pages.</summary>
    public PageDecorationViewModel Decorations => _decorations ?? Made(out _decorations, Owned(new PageDecorationViewModel(this)));

    /// <summary>Gets the preparation of the form: adding, removing and changing fields.</summary>
    public FormDesignViewModel FormDesign => _formDesign ??= Owned(new FormDesignViewModel(this));

    /// <summary>Gets the selection and undoable actions for the document's pages.</summary>
    public PageManagementViewModel Pages => PagesInUse ?? Announced(PagesInUse = Owned(new PageManagementViewModel(this)));

    /// <summary>Gets the name recorded as the author of new comments: the one chosen in Preferences, or empty for the user name.</summary>
    public string CommentAuthor => _services.Settings.CommentAuthor;

    /// <summary>Gets a value indicating whether fonts that cannot be saved in a PDF are offered for typing.</summary>
    public bool ShowPreviewOnlyFonts => _services.Settings.ShowPreviewOnlyFonts;

    /// <summary>Gets the measuring tool.</summary>
    public MeasureViewModel Measure => _measure ?? Made(out _measure, Owned(new MeasureViewModel(this)));

    /// <summary>Gets the measuring tool once it has been used, or <see langword="null"/> before. Reading it does not make it.</summary>
    public MeasureViewModel? MeasureIfMade => _measure;

    /// <summary>Gets the digital signature state.</summary>
    public SignaturesViewModel Signatures => _signatures ?? Loaded(Made(out _signatures, new(this, _services)), static made => made.Refresh());

    /// <summary>Gets the tab's text recognition, created on first use.</summary>
    public TextRecognitionViewModel TextRecognition => TextRecognitionInUse ?? Announced(TextRecognitionInUse = Owned(new TextRecognitionViewModel(this, _services)));

    /// <summary>Gets focus Mode, created when first used.</summary>
    public FocusModeViewModel FocusMode => _focusMode ?? Made(out _focusMode, Owned(new FocusModeViewModel(this, _services)));

    /// <summary>Gets Focus Mode once it has been used, or <see langword="null"/> before. Reading it does not make it.</summary>
    public FocusModeViewModel? FocusModeIfMade => _focusMode;

    /// <summary>Gets the Read Aloud state, created when first used.</summary>
    public ReadAloudViewModel ReadAloud => _readAloud ?? Made(out _readAloud, Owned(new ReadAloudViewModel(this, _services)));

    /// <summary>Gets Read Aloud once it has been used, or <see langword="null"/> before. Reading it does not make it.</summary>
    public ReadAloudViewModel? ReadAloudIfMade => _readAloud;

    /// <summary>Gets the form filling state.</summary>
    public FormsViewModel Forms => _forms ??= Owned(new FormsViewModel(this));

    /// <summary>Gets the Fill &amp; Sign state.</summary>
    public FillAndSignViewModel FillAndSign => _fillAndSign ?? Made(out _fillAndSign, new(this, _services));

    /// <summary>Gets Fill and Sign once it has been used, or <see langword="null"/> before. Reading it does not make it.</summary>
    public FillAndSignViewModel? FillAndSignIfMade => _fillAndSign;

    /// <summary>Gets the commands that save a changed copy, such as an optimised, PDF/A, flattened or protected one.</summary>
    public TabCopyActionSet Copies => _copies ?? Made(out _copies, Owned(NewCopies()));

    /// <summary>Gets the links to other files: PDFs open in a tab, other files with the desktop's app after asking.</summary>
    public TabFileLinks Links => _links ?? Made(out _links, Owned(new TabFileLinks(this)));

    /// <summary>
    /// Gets the parts of the tab that are made on first use and that a window waits for: those made already, then each as it is
    /// made. A window answers a part's questions from when the part exists, so showing a tab does not make it.
    /// </summary>
    public IObservable<object> MadeParts => _partMakes ??= new();

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
    public partial OutlineItemViewModel[] Outline { get; private set; } = [];

    /// <summary>Gets a value indicating whether the document has an outline.</summary>
    public bool HasOutline => Outline.Length > 0;

    /// <summary>Gets the page thumbnails.</summary>
    [Reactive]
    public partial ThumbnailItemViewModel[] Thumbnails { get; private set; } = [];

    /// <summary>Gets or sets the selected thumbnail; selecting one scrolls to its page.</summary>
    [Reactive]
    public partial ThumbnailItemViewModel? SelectedThumbnail { get; set; }

    /// <summary>Gets or sets the selected outline entry; selecting one navigates to it.</summary>
    [Reactive]
    public partial OutlineItemViewModel? SelectedOutlineItem { get; set; }

    /// <summary>Gets printing: the print preview, the desktop's print dialog and saving the printed pages as a PDF.</summary>
    public TabPrintingViewModel Printing => _printing ?? Made(out _printing, new(this));

    /// <summary>Gets or sets a message about the document that waits until dismissed, for example a failed save.</summary>
    public string? Notice
    {
        get => _saveNotice is { } outcome ? SaveText.Notice(outcome) : _localizedNotice.Value;
        set => SetNotice(LocalizedMessage.Literal(value));
    }

    /// <summary>Gets the interaction asking where to save a copy.</summary>
    public Interaction<string, string?> SaveAsInteraction { get; } = new();

    /// <summary>Gets a value indicating whether the file changed on disk and the user chose to be asked before reloading.</summary>
    [Reactive]
    public partial bool HasPendingReload { get; private set; }

    /// <summary>Gets the application services, for the parts of the tab that need settings or desktop services.</summary>
    public AppServices Services => _services;

    /// <summary>Gets the annotation tools once they have been used, or <see langword="null"/> before.</summary>
    [Reactive]
    public partial AnnotationsViewModel? AnnotationsInUse { get; private set; }

    /// <summary>Gets the page management state once it has been used, or <see langword="null"/> before.</summary>
    [Reactive]
    public partial PageManagementViewModel? PagesInUse { get; private set; }

    /// <summary>Gets the text recognition state once it has been used, or <see langword="null"/> before.</summary>
    [Reactive]
    public partial TextRecognitionViewModel? TextRecognitionInUse { get; private set; }

    /// <summary>Gets the tint and sharpness a background tab keeps until it is selected.</summary>
    internal TabSettingsSlot PendingSettings { get; } = new();

    /// <summary>Gets how media plays on this computer.</summary>
    internal MediaPlaybackFactory MediaPlayback => _services.MediaPlayback;

    /// <summary>Gets the document's text in reading order, which Read Aloud and Focus Mode share.</summary>
    internal TabReading Reading => _reading ??= new(this);

    /// <summary>Gets or sets the tick count when the tab was last left for another, or 0 when it is shown, was never shown or has let its idle memory go.</summary>
    internal long LeftAt { get; set; }

    /// <summary>Gets what follows changes other programs make to the open file.</summary>
    private TabFileWatch FileWatch => field ??= new(_services.FileWatches);

    /// <summary>
    /// Gets a signal over the tab that many parts of the window follow each time the tab is shown. It is made and connected the
    /// first time it is asked for and shared after that, so showing the tab again follows one signal and does not subscribe
    /// to every property behind it again. The connection ends when the tab closes or has gone unshown for a while.
    /// </summary>
    /// <typeparam name="T">The type of the signal's values.</typeparam>
    /// <param name="key">What names the signal; the caller keeps one for each signal it makes.</param>
    /// <param name="make">Makes the signal from the tab, published or replayed as its followers need.</param>
    /// <returns>The shared signal.</returns>
    public IObservable<T> KeepSignal<T>(object key, Func<DocumentTabViewModel, ConnectableSignal<T>> make)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(make);
        _keptSignals ??= [];
        if (_keptSignals.TryGetValue(key, out var kept))
        {
            return (IObservable<T>)kept.Signal;
        }

        var shared = make(this);
        _keptSignals[key] = new(shared, shared.Connect());
        return shared;
    }

    /// <summary>
    /// Follows the changes of a part of the tab that is made on first use, from when the part is made. Nothing is followed, and
    /// the part is not made, while nothing has used it.
    /// </summary>
    /// <typeparam name="TPart">The part's type.</typeparam>
    /// <param name="changes">Gives the changes to follow from the part.</param>
    /// <returns>The changes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> PartChanges<TPart>(Func<TPart, IObservable<RxVoid>> changes)
        where TPart : class =>
        MadeParts.OfType<TPart>().Take(1).SwitchMap(changes);

    /// <summary>Opens the document with cancellable I/O and loads its structure on the UI thread.</summary>
    /// <returns>A cold signal that completes when the document is ready; unsubscribing cancels the open and first-page work.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public IObservable<RxVoid> EnsureLoaded() => IsLoaded && Source.IsOpen ? Signal.Return(RxVoid.Default) : Load();

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
        pageIndex >= 0 && pageIndex < Thumbnails.Length ? Thumbnails[pageIndex].Label : (pageIndex + 1).ToString(CultureInfo.CurrentCulture);

    /// <summary>Shows the bookmarks of an edited document without moving the reader.</summary>
    /// <param name="items">The rebuilt entries.</param>
    /// <param name="selectId">The object number of the entry to select, or 0 for none.</param>
    public void ShowEditedOutline(OutlineItemViewModel[] items, int selectId)
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
    /// <returns>A cold signal giving whether the file was saved; unsubscribing cancels the snapshot and output write.</returns>
    public IObservable<bool> SaveTo(string path) =>
        NeedsPassword || ErrorMessage is not null
            ? Signal.Return(false)

            : Source.Load(RxSchedulers.MainThreadScheduler)
                .SwitchMap(document => DocumentFeature<IAnnotationEditor>.Get(document) is { } editor ? Write(document, editor, path) : Signal.Return(false))
                .Catch<bool, DocumentOpenException>(static _ => Signal.Return(false))
                .Catch<bool, OperationCanceledException>(static _ => Signal.Return(false));

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
    /// <returns>A signal that completes once the selected tab has loaded again; a tab that is not selected loads when it is.</returns>
    [ReactiveCommand]
    public IObservable<RxVoid> Reload()
    {
        HasPendingReload = false;
        var page = CurrentPageIndex;
        if (_copies?.CloseWindows() == true)
        {
            SetNotice(LocalizedMessage.Get(UiText, "The file changed on disk and was reloaded, so the window for saving a copy was closed."));
        }

        TabTiles.DropBoth(RenderHub, Source.Id);
        Source.Reload();
        _reading?.Reset();
        IsLoaded = false;
        LayoutVersion++;
        return IsSelected
            ? EnsureLoaded().Select(_ => ShowReloaded(page)).Catch<RxVoid, OperationCanceledException>(static _ => Signal.Return(RxVoid.Default))
            : Signal.Return(RxVoid.Default);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _subscriptions.Dispose();
        ReleaseKeptSignals();
        _media?.Dispose();
    }

    /// <summary>
    /// Lets go of what the document keeps only to draw its pages faster, once the tab has gone unshown since before a moment.
    /// The window calls it now and then for every tab, so hundreds of open tabs do not each keep the pictures recorded of their
    /// pages. A tab shown since, or one that already let go, does nothing; a page shown later is recorded again.
    /// </summary>
    /// <param name="before">The tick count a tab must have been left before.</param>
    internal void ReleaseIdleMemory(long before)
    {
        if (IsSelected || LeftAt == 0 || LeftAt > before)
        {
            return;
        }

        LeftAt = 0;
        ReleaseIdleMemory();
        ReleaseKeptSignals();
    }

    /// <summary>Lets go of what the document keeps only to draw its pages faster.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ReleaseIdleMemory() => DocumentFeature<IIdleRenderMemory>.Get(TryGetDocument())?.Release();

    /// <summary>Tells the pages that a page's content was changed, so its tiles are drawn again.</summary>
    /// <param name="pageIndex">The page changed.</param>
    internal void ReportPageChange(int pageIndex) => LastPageChange = LastPageChange.Next(pageIndex);

    /// <summary>Reloads a changed file, or offers to, depending on the user's choice. Unsaved edits are never discarded silently.</summary>
    internal void CheckChangedFile()
    {
        var response = FileWatch.Check(FilePath, _services.Settings.FileChangeAction, Source.HasUnsavedChanges);
        if (response == FileChangeResponse.Ask)
        {
            HasPendingReload = true;
        }
        else if (response == FileChangeResponse.Reload)
        {
            _subscriptions.Add(Reload().SubscribeSafe(static _ => { }, ErrorTrace.Write));
        }
    }

    /// <summary>Reads the separation details with async I/O, so the page structure read that follows finds them loaded.</summary>
    /// <param name="document">The acquired document.</param>
    /// <returns>A cold signal giving the document once its separations are read.</returns>
    private static IObservable<IDocument> ReadSeparations(IDocument document) =>
        DocumentFeature<ISeparationSource>.Get(document) is { } separations
            ? separations.ReadSeparations().Select(_ => document)
            : Signal.Return(document);

    /// <summary>Saves between the document's own will-save and did-save actions, which may change fields before the file is written.</summary>
    /// <param name="document">The open document.</param>
    /// <param name="editor">The document writer.</param>
    /// <param name="path">The destination file.</param>
    /// <returns>A cold signal giving whether the file was saved.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<bool> Write(IDocument document, IAnnotationEditor editor, string path) =>
        Forms.RunDocumentEvent(FormDocumentEvent.WillSave)
            .SwitchMap(_ => Pdf20SavePlan.Plan(document, Source.HasUnsavedChanges, Copies.Pdf20SaveInteraction))
            .SwitchMap(upgrade => DocumentSave.Write(editor, Source, path, upgrade))
            .Select(outcome => ApplySaveOutcome(outcome))
            .SwitchMap(saved => saved ? Forms.RunDocumentEvent(FormDocumentEvent.DidSave).Select(static _ => true) : Signal.Return(false));

    /// <summary>Reflects the publication result in the tab and its file watcher.</summary>
    /// <param name="outcome">The write and replacement result.</param>
    /// <returns>Whether the destination was saved.</returns>
    private bool ApplySaveOutcome(in DocumentSaveOutcome outcome)
    {
        if (outcome.Saved)
        {
            FileWatch.MarkSaved();
        }

        if (outcome.Error is { } error)
        {
            SetNotice(LocalizedMessage.Format(UiText, "Could not save: {0}", error));
        }
        else if (SaveText.Notice(outcome) is { } notice)
        {
            SetNotice(LocalizedMessage.Literal(notice));
            _saveNotice = outcome;
        }

        HasUnsavedChanges = Source.HasUnsavedChanges;
        return outcome.Saved;
    }

    /// <summary>Opens a cold document, reporting ordinary open errors in the tab.</summary>
    /// <returns>A cold signal that completes once the document is open or could not be.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> Load() =>
        Source.Load(RxSchedulers.MainThreadScheduler)
            .SwitchMap(ReadSeparations)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .Select(ShowLoaded)
            .Catch<RxVoid, DocumentOpenException>(OpenFailed);

    /// <summary>Publishes a document that opened.</summary>
    /// <param name="document">The ready document.</param>
    /// <returns>The completion value.</returns>
    private RxVoid ShowLoaded(IDocument document)
    {
        CompleteLoad(document);
        return RxVoid.Default;
    }

    /// <summary>Shows why the document did not open.</summary>
    /// <param name="error">The open failure.</param>
    /// <returns>A signal that ends at once.</returns>
    private IObservable<RxVoid> OpenFailed(DocumentOpenException error)
    {
        NeedsPassword = error.Error == DocumentOpenError.Password;
        NeedsCertificate = error.Error == DocumentOpenError.Certificate;
        SetCertificateError(NeedsCertificate ? LocalizedMessage.Literal(error.Message) : LocalizedMessage.Literal(null));
        SetErrorMessage(NeedsPassword || NeedsCertificate ? LocalizedMessage.Literal(null) : LocalizedMessage.Literal(error.Message));
        return Signal.Return(RxVoid.Default);
    }

    /// <summary>Goes back to the page that was shown before a reload, once the document is open again.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The completion value.</returns>
    private RxVoid ShowReloaded(int page)
    {
        if (IsLoaded)
        {
            GoToPage(page);
            _search?.Refresh();
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
        SetCertificateError(LocalizedMessage.Literal(null));
        SetErrorMessage(LocalizedMessage.Literal(null));
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
        _copies?.Refresh(document);
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
        _signatures?.Refresh();
        Attachments.Refresh();
        _layers?.Refresh();
        _parts?.Refresh();
        _media?.Refresh();
        _copies?.Refresh(document);
        PagesInUse?.Refresh();
        if (!IsSecondaryView)
        {
            _services.RecentDocuments.Add(FilePath);
            _subscriptions.Add(Notices.CheckContent(document, Attachments.Items.Count).SubscribeSafe(static _ => { }, ErrorTrace.Write));
            Notices.CheckRepairs(document);
            Notices.CheckIntegrity(Source.Metadata);
            FileWatch.Start(FilePath);
        }

        if (Source.Metadata?.WrappedFileName is { Length: > 0 } wrappedName)
        {
            SetNotice(LocalizedMessage.Format(UiText, "This file contains an encrypted document named {0}. Open it from Attachments.", wrappedName));
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
        AnnotationsInUse?.InvalidateItems();
        ShowSidebarPane();
        _signatures?.Refresh();
        Attachments.Refresh();
        _layers?.Refresh();
        _parts?.Refresh();
        _media?.Refresh();
        _tags?.Refresh();
        _search?.Refresh();
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
    /// <returns>A signal that ends once the copy is saved or the choice was given up.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> SaveAs() => AskAndSave().Select(static _ => RxVoid.Default);

    /// <summary>Asks where to save a copy, suggesting a PDF name for a picture opened as a document, and saves it there.</summary>
    /// <returns>A cold signal giving whether the file was saved.</returns>
    private IObservable<bool> AskAndSave()
    {
        var suggested = PictureFiles.IsPictureFile(FileName) ? PictureFiles.SuggestedPdfName(FileName) : FileName;
        return SaveAsInteraction.Handle(suggested).ToSignal()
            .SwitchMap(path => string.IsNullOrEmpty(path) ? Signal.Return(false) : SaveTo(path));
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
    /// <returns>A signal giving whether the file was saved.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<bool> Save() => PictureFiles.IsPictureFile(FilePath) ? AskAndSave() : SaveTo(FilePath);

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
    /// Follows the current page: updates the page box, shows its separation and checks for repairs. Forms and layers
    /// follow the page themselves, and the thumbnail follows once the page settles.
    /// </summary>
    /// <param name="pageIndex">The current page.</param>
    private void FollowCurrentPage(int pageIndex)
    {
        PageEntry = GetPageDisplay(pageIndex);
        Separations.ShowPage(pageIndex);
        CheckRepairsOfOpenDocument();
    }

    /// <summary>
    /// Selects the thumbnail of the page the reader has settled on. Selecting it scrolls the strip, which rebuilds the strip's
    /// items after a long jump, so a fast scroll or scrollbar drag, which changes the page every few milliseconds, waits.
    /// </summary>
    /// <param name="pageIndex">The page.</param>
    private void SelectThumbnailOf(int pageIndex)
    {
        SelectedThumbnail = pageIndex >= 0 && pageIndex < Thumbnails.Length ? Thumbnails[pageIndex] : null;
        PagesInUse?.Follow(SelectedThumbnail);
    }

    /// <summary>Drops the tiles a page change made stale, so they are drawn again, and notes whether the document now has unsaved changes.</summary>
    /// <param name="change">The change.</param>
    private void DropChangedTiles(PageChange change)
    {
        TabTiles.DropChanged(RenderHub, Source.Id, change.PageIndex);
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
        for (var i = 0; i < Thumbnails.Length; i++)
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
    /// <returns>A signal that ends once the open attempt is over.</returns>
    [ReactiveCommand]
    private IObservable<RxVoid> SubmitPassword()
    {
        Source.Password = PasswordEntry;
        PasswordEntry = string.Empty;
        NeedsPassword = false;
        return EnsureLoaded().Catch<RxVoid, OperationCanceledException>(static _ => Signal.Return(RxVoid.Default));
    }

    /// <summary>Chooses a certificate file through the desktop picker.</summary>
    /// <returns>A signal that ends once the picker is closed.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private IObservable<RxVoid> BrowseCertificate() => DocumentCertificateBrowsing.Browse(this);

    /// <summary>Retries opening with the selected recipient certificate.</summary>
    /// <returns>A signal that ends once the open attempt is over.</returns>
    [ReactiveCommand]
    private IObservable<RxVoid> SubmitCertificate()
    {
        if (string.IsNullOrWhiteSpace(CertificatePathEntry))
        {
            SetCertificateError(LocalizedMessage.Get(UiText, "Choose a certificate file first."));
            return Signal.Return(RxVoid.Default);
        }

        Source.UseCertificate(CertificatePathEntry, CertificatePasswordEntry);
        CertificatePasswordEntry = string.Empty;
        SetCertificateError(LocalizedMessage.Literal(null));
        NeedsCertificate = false;
        return EnsureLoaded().Catch<RxVoid, OperationCanceledException>(static _ => Signal.Return(RxVoid.Default));
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

    /// <summary>
    /// Makes the commands that save a changed copy. Opening a document refreshes them only once they exist, so commands made
    /// after the document opened are told here what its engine supports.
    /// </summary>
    /// <returns>The commands.</returns>
    private TabCopyActionSet NewCopies()
    {
        var copies = new TabCopyActionSet(this);
        if (IsLoaded && TryGetDocument() is { } document)
        {
            copies.Refresh(document);
        }

        return copies;
    }

    /// <summary>
    /// Brings a part made on first use up to date with the open document. Opening a document refreshes a part only once it
    /// exists, so a part made after the document opened is refreshed here.
    /// </summary>
    /// <typeparam name="TPart">The part's type.</typeparam>
    /// <param name="part">The new part, already kept in its field.</param>
    /// <param name="refresh">Reads the document into the part.</param>
    /// <returns>The same part.</returns>
    private TPart Loaded<TPart>(TPart part, Action<TPart> refresh)
    {
        if (IsLoaded)
        {
            refresh(part);
        }

        return part;
    }

    /// <summary>Tells whatever waits for a part made on first use that it has been made.</summary>
    /// <typeparam name="TPart">The part's type.</typeparam>
    /// <param name="part">The new part, already kept in its field.</param>
    /// <returns>The same part.</returns>
    private TPart Announced<TPart>(TPart part)
        where TPart : class
    {
        (_partMakes ??= new()).OnNext(part);
        return part;
    }

    /// <summary>Keeps a part made on first use in its field and tells whatever waits for it.</summary>
    /// <typeparam name="TPart">The part's type.</typeparam>
    /// <param name="slot">The field that holds the part, set before anything is told so a reader that asks again gets this part.</param>
    /// <param name="part">The new part.</param>
    /// <returns>The same part.</returns>
    private TPart Made<TPart>(out TPart? slot, TPart part)
        where TPart : class
    {
        slot = part;
        (_partMakes ??= new()).OnNext(part);
        return part;
    }

    /// <summary>Ends the shared signals kept for the tab. They are made again the next time the tab is shown.</summary>
    private void ReleaseKeptSignals()
    {
        if (_keptSignals is not { } kept)
        {
            return;
        }

        _keptSignals = null;
        foreach (var signal in kept.Values)
        {
            signal.Connection.Dispose();
        }
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
        internal IObservable<RxVoid> Loading => tab.EnsureLoaded().Catch<RxVoid, Exception>(LoadFailed).Concat(Signal.Never<RxVoid>());

        /// <inheritdoc/>
        public void Dispose()
        {
            tab.EndSelectedWork();
            _work.Cancel();
            _work.Dispose();
        }

        /// <summary>Shows an error from loading the selected tab; a load that was cancelled because the tab was deselected is not one.</summary>
        /// <param name="error">The failure.</param>
        /// <returns>A signal that ends at once.</returns>
        private static IObservable<RxVoid> LoadFailed(Exception error)
        {
            if (error is not OperationCanceledException)
            {
                ErrorTrace.Write(error);
            }

            return Signal.Return(RxVoid.Default);
        }
    }

    /// <summary>A shared signal kept for the tab.</summary>
    /// <param name="Signal">The shared signal, as the followers see it.</param>
    /// <param name="Connection">What keeps the shared signal following the tab.</param>
    [DebuggerDisplay("KeptSignal: {Signal}")]
    private sealed record KeptSignal(object Signal, IDisposable Connection);
}
