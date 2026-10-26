// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Platform;
using HyperPdf.Core.Rendering;
using HyperPdf.Core.Settings;
using HyperPdf.Core.Tabs;
using HyperPdf.Http.Remote;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels;

/// <summary>The main window: the open tabs, the start page and window level commands.</summary>
[DebuggerDisplay("MainViewModel: {Tabs.Count} tabs")]
public sealed partial class MainViewModel : ReactiveObject, IDisposable, ILocalisationChildren
{
    /// <summary>The number of recent documents shown on the start page.</summary>
    private const int RecentCount = 24;

    /// <summary>Closing this many tabs at once asks first.</summary>
    private const int ConfirmCloseThreshold = 2;

    /// <summary>The number of closed tabs that can be reopened.</summary>
    private const int ClosedTabMemory = 32;

    /// <summary>The services.</summary>
    private readonly AppServices _services;

    /// <summary>Recently closed tabs, most recent last; tabs closed together form one group and reopen together.</summary>
    private readonly List<SessionTab[]> _closedTabs = [];

    /// <summary>
    /// Owns everything the view model holds: the download token, the subscriptions that follow the services, and
    /// finally the tabs. It releases them in the order they were added.
    /// </summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>Cancelled when the window's view model is disposed, which stops downloads still running; its source is owned by <see cref="_subscriptions"/>.</summary>
    private readonly CancellationToken _lifetime;

    /// <summary>The tab whose cancellable work currently owns selection.</summary>
    private DocumentTabViewModel? _activeTab;

    /// <summary>The folder search, created only when the reader opens it.</summary>
    private FolderSearchViewModel? _folderSearch;

    /// <summary>The retained interface message, separate from document and exception text.</summary>
    private LocalizedMessage _statusMessage;

    /// <summary>The tint and sharpness last given to the tabs, or <see langword="null"/> before the first change.</summary>
    private TabSettings? _appliedTabSettings;

    /// <summary>Initializes a new instance of the <see cref="MainViewModel"/> class.</summary>
    /// <param name="services">The application services.</param>
    public MainViewModel(AppServices services)
    {
        ArgumentNullException.ThrowIfNull(services);
        _services = services;
        PageToneEnabled = services.Settings.PageToneEnabled;
        RefreshRecentDocuments();
        var lifetime = new CancellationDisposable();
        _lifetime = lifetime.Token;
        _subscriptions.Add(lifetime);
        _subscriptions.Add(services.Theme.Select(static theme => theme.PageTone).SubscribeSafe(ShareTone, ErrorTrace.Write));

        // The window made last answers a request to open a document, so it opens once. The handler needs this view model.
        _subscriptions.Add(
            services.OpenDocumentInteraction.RegisterHandler(
                    context =>
{
    Open([context.Input]);
    context.SetOutput(RxVoid.Default);
        }));
        _subscriptions.Add(services.WhenChanged(static x => x.SettingsVersion).Skip(1).SubscribeSafe(_ => TakeSettings(), ErrorTrace.Write));
        _subscriptions.Add(this.WhenChanged(static x => x.TabQuery).Skip(1).SubscribeSafe(_ => RefreshFoundTabs(), ErrorTrace.Write));

        // Leaving a tab stops its work and puts away a second view of another document; the tab shown starts its work.
        _subscriptions.Add(
            this.WhenChanged(static x => x.SelectedTab).Skip(1).Do(LeaveActiveTab).KeepNotNull().SubscribeSafe(static tab => tab.IsSelected = true, ErrorTrace.Write));

        // The window title follows the shown tab's name, which changes when its document loads.
        _subscriptions.Add(
            this.WhenChanged(static x => x.SelectedTab).SwitchMap(
                static tab => tab is null
                    ? Signal.Never<string>()
                    : tab
                .WhenChanged(static t => t.WindowName)).SubscribeSafe(_ => RaiseWindowTitle(), ErrorTrace.Write));
        _subscriptions.Add(this.WhenChanged(static x => x.PageToneEnabled).Skip(1).SubscribeSafe(ApplyPageTone, ErrorTrace.Write));
        _subscriptions.Add(Scope.Create(ReleaseTabs));
    }

    /// <summary>Gets the interaction asking the view for files to open.</summary>
    public Interaction<RxVoid, IReadOnlyList<string>> OpenFileInteraction { get; } = new();

    /// <summary>Gets the interaction asking the user to confirm a destructive action.</summary>
    public Interaction<ConfirmRequest, bool> ConfirmInteraction { get; } = new();

    /// <summary>Gets the interaction asking the view to show the preferences.</summary>
    public Interaction<PreferencesViewModel, RxVoid> ShowPreferencesInteraction { get; } = new();

    /// <summary>Gets the interaction asking the view to show the About and Licences window.</summary>
    public Interaction<LicencesViewModel, RxVoid> ShowLicencesInteraction { get; } = new();

    /// <summary>Gets the interaction asking the view to show document properties.</summary>
    public Interaction<DocumentTabViewModel, RxVoid> ShowPropertiesInteraction { get; } = new();

    /// <summary>Gets the open tabs.</summary>
    public ObservableCollection<DocumentTabViewModel> Tabs { get; } = [];

    /// <summary>Gets the tabs matching <see cref="TabQuery"/>, shown in the tab finder.</summary>
    public ObservableCollection<DocumentTabViewModel> FoundTabs { get; } = [];

    /// <summary>Gets or sets what the user typed in the tab finder.</summary>
    [Reactive]
    public partial string TabQuery { get; set; } = string.Empty;

    /// <summary>Gets the recently opened documents shown on the start page.</summary>
    public ObservableCollection<RecentDocument> RecentDocuments { get; } = [];

    /// <summary>Gets or sets the selected tab.</summary>
    [Reactive(nameof(WindowTitle))]
    public partial DocumentTabViewModel? SelectedTab { get; set; }

    /// <summary>Gets a value indicating whether any tab is open.</summary>
    [Reactive]
    public partial bool HasTabs { get; private set; }

    /// <summary>Gets a value indicating whether the user is being asked whether to discard unsaved edits.</summary>
    [Reactive]
    public partial bool IsConfirmingDiscard { get; private set; }

    /// <summary>Gets a value indicating whether any tab has unsaved annotations or form entries.</summary>
    public bool HasUnsavedTabs
    {
        get
        {
            foreach (var tab in Tabs)
            {
                if (tab.HasUnsavedChanges)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Gets the window title.</summary>
    public string WindowTitle => SelectedTab is { } tab ? UiText.Format("{0} — Hyper PDF Viewer", tab.WindowName) : "Hyper PDF Viewer";

    /// <summary>Gets or sets a value indicating whether pages are drawn in the comfort page colour rather than plain white.</summary>
    [Reactive]
    public partial bool PageToneEnabled { get; set; }

    /// <summary>Gets the page tone tabs draw with.</summary>
    [Reactive]
    public partial PageTone PageTone { get; private set; } = PageTone.None;

    /// <summary>Gets a status message, for example a failed download; it stays until dismissed.</summary>
    public string? StatusMessage { get => _statusMessage.Value; private set => SetStatusMessage(LocalizedMessage.Literal(value)); }

    /// <summary>Gets or sets a value indicating whether the tab finder is open; a command sets it to open the finder, and closing the finder clears it.</summary>
    [Reactive]
    public partial bool IsTabFinderOpen { get; set; }

    /// <summary>Gets the second view of the selected document while the view is split, or <see langword="null"/>.</summary>
    [Reactive]
    public partial DocumentTabViewModel? SplitTab { get; private set; }

    /// <summary>Gets the interaction that shows another window, given its view model.</summary>
    public Interaction<MainViewModel, RxVoid> NewWindowInteraction { get; } = new();

    /// <summary>Gets a value indicating whether this is a window opened from another, which leaves the session to the first window.</summary>
    public bool IsSecondaryWindow { get; init; }

    /// <summary>Gets the interaction that shows the Search in Folder window.</summary>
    public Interaction<FolderSearchViewModel, RxVoid> ShowFolderSearchInteraction { get; } = new();

    /// <summary>Gets the folder search, kept so its results stay while the window is closed and opened again.</summary>
    public FolderSearchViewModel FolderSearch => _folderSearch ??= new(_services, OpenAt);

    /// <summary>Opens files, file URIs or web addresses, selecting the last one. Already open files are selected instead.</summary>
    /// <param name="items">Paths or URIs.</param>
    public void Open(IEnumerable<string> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        DocumentTabViewModel? last = null;
        foreach (var item in items)
        {
            if (RemoteDocumentDownloader.IsRemote(item, out var remote))
            {
                _ = OpenRemoteAsync(remote);
                continue;
            }

            var path = ToLocalPath(item);
            if (path is not null)
            {
                last = OpenOrFind(path, StartPage(path));
            }
        }

        if (last is not null)
        {
            SelectedTab = last;
        }
    }

    /// <summary>Opens a file, or selects it when already open, at a page, with words found by a folder search shown.</summary>
    /// <param name="path">The file.</param>
    /// <param name="pageIndex">The page, zero-based.</param>
    /// <param name="query">The words to show, or empty.</param>
    public void OpenAt(string path, int pageIndex, string query)
    {
        var tab = OpenOrFind(path, pageIndex);
        SelectedTab = tab;
        tab.GoToPage(pageIndex);
        if (query.Length == 0)
        {
            return;
        }

        tab.Search.Query = query;
        tab.Search.IsOpen = true;
    }

    /// <summary>Restores the tabs open at the end of the previous run.</summary>
    public void RestoreSession()
    {
        if (!_services.Settings.RestoreSession)
        {
            return;
        }

        DocumentTabViewModel? selected = null;
        foreach (var entry in _services.Settings.Session)
        {
            if (!File.Exists(entry.FilePath))
            {
                continue;
            }

            var tab = OpenOrFind(entry.FilePath, entry.PageIndex);
            if (entry.IsSelected)
            {
                selected = tab;
            }
        }

        SelectedTab = selected ?? (Tabs.Count > 0 ? Tabs[0] : null);
    }

    /// <summary>Records the open tabs and saves settings. Another window only records where its documents were left.</summary>
    public void SaveSession()
    {
        if (IsSecondaryWindow)
        {
            foreach (var tab in Tabs)
            {
                RememberPage(tab);
            }

            _services.SaveSettings();
            return;
        }

        var session = _services.Settings.Session;
        session.Clear();
        foreach (var tab in Tabs)
        {
            RememberPage(tab);
            session.Add(new() { FilePath = tab.FilePath, PageIndex = Math.Max(0, tab.CurrentPageIndex), IsSelected = tab == SelectedTab });
        }

        if (SelectedTab is { } current)
        {
            _services.Settings.ShowSidebar = current.SidebarVisible;
        }

        _services.SaveSettings();
    }

    /// <summary>Records the window size and state for the next start.</summary>
    /// <param name="width">The window width.</param>
    /// <param name="height">The window height.</param>
    /// <param name="maximized">Whether the window is maximised.</param>
    /// <param name="restored">Whether the window is in its normal state, so its size is meaningful.</param>
    public void RememberWindow(double width, double height, bool maximized, bool restored)
    {
        var settings = _services.Settings;
        settings.WindowMaximized = maximized;
        if (!restored)
        {
            return;
        }

        settings.WindowWidth = width;
        settings.WindowHeight = height;
    }

    /// <summary>Moves a tab, used by drag and drop reordering.</summary>
    /// <param name="oldIndex">The current index.</param>
    /// <param name="newIndex">The new index.</param>
    public void MoveTab(int oldIndex, int newIndex)
    {
        if (oldIndex == newIndex || (uint)oldIndex >= (uint)Tabs.Count || (uint)newIndex >= (uint)Tabs.Count)
        {
            return;
        }

        var selected = SelectedTab;
        Tabs.Move(oldIndex, newIndex);
        SelectedTab = selected;
    }

    /// <summary>Closes a tab straight away, even with unsaved edits; the tab can be reopened.</summary>
    /// <param name="tab">The tab.</param>
    public void CloseTabWithoutAsking(DocumentTabViewModel? tab)
    {
        if (tab is null)
        {
            return;
        }

        var index = Tabs.IndexOf(tab);
        if (index < 0)
        {
            return;
        }

        Remember([tab]);
        Close(tab, index);
    }

    /// <summary>Refreshes the tab finder's list from <see cref="TabQuery"/>.</summary>
    public void RefreshFoundTabs()
    {
        var summaries = new TabSummary[Tabs.Count];
        for (var i = 0; i < summaries.Length; i++)
        {
            var tab = Tabs[i];
            summaries[i] = new(tab.FileName, tab.Title, tab.Folder);
        }

        var matches = new int[summaries.Length];
        var count = TabFilter.Filter(TabQuery, summaries, matches);
        FoundTabs.Clear();
        for (var i = 0; i < count; i++)
        {
            FoundTabs.Add(Tabs[matches[i]]);
        }
    }

    /// <summary>Asks whether to discard unsaved edits in every tab; true when there are none.</summary>
    /// <returns><see langword="true"/> to go ahead.</returns>
    public async Task<bool> ConfirmDiscardAsync()
    {
        var unsaved = 0;
        foreach (var tab in Tabs)
        {
            unsaved += tab.HasUnsavedChanges ? 1 : 0;
        }

        if (unsaved == 0)
        {
            return true;
        }

        var request = new ConfirmRequest(
            unsaved == 1 ? LocalizedMessage.Get(UiText, "Close without saving?") : LocalizedMessage.Format(UiText, "Close {0} documents without saving?", unsaved),
            LocalizedMessage.Get(UiText, "Annotations and form entries you have not saved will be lost. Choose Cancel, then Save (Ctrl+S) to keep them."),
            LocalizedMessage.Get(UiText, "Close Without Saving"));
        IsConfirmingDiscard = true;
        try
        {
            return await ConfirmInteraction.Handle(request).ToTask().ConfigureAwait(true);
        }
        finally
        {
            IsConfirmingDiscard = false;
        }
    }

    /// <summary>Closes a tab, asking first when it has unsaved edits.</summary>
    /// <param name="tab">The tab; null closes the selected tab.</param>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    public async Task CloseTabAsync(DocumentTabViewModel? tab)
    {
        tab ??= SelectedTab;
        if (tab is null)
        {
            return;
        }

        if (tab.HasUnsavedChanges)
        {
            var request = new ConfirmRequest(
                LocalizedMessage.Format(UiText, "Close {0} without saving?", tab.FileName),
                LocalizedMessage.Get(UiText, "Annotations and form entries you have not saved will be lost. Choose Cancel, then Save (Ctrl+S) to keep them."),
                LocalizedMessage.Get(UiText, "Close Without Saving"));
            if (!await ConfirmInteraction.Handle(request).ToTask().ConfigureAwait(true))
            {
                return;
            }
        }

        _ = await tab.Forms.RunDocumentEventAsync(FormDocumentEvent.WillClose).ConfigureAwait(true);
        CloseTabWithoutAsking(tab);
    }

    /// <summary>Shows a failure no command handled as a notice on the selected tab, so the app goes on running.</summary>
    /// <param name="text">What went wrong, in plain words.</param>
    public void ReportFailure(string text)
    {
        if (SelectedTab is { } tab)
        {
            tab.SetNotice(LocalizedMessage.Literal(text));
        }
    }

    /// <summary>Reloads the recent documents list.</summary>
    public void RefreshRecentDocuments()
    {
        var latest = _services.RecentDocuments.GetRecent(RecentCount);
        if (SameDocuments(latest))
        {
            // Nothing changed since the list was last shown, so the menu and start page keep their items.
            return;
        }

        RecentDocuments.Clear();
        foreach (var recent in latest)
        {
            RecentDocuments.Add(recent);
        }
    }

    /// <summary>Removes one recent entry from the start page and menu.</summary>
    /// <param name="recent">The document to forget.</param>
    public void RemoveRecent(RecentDocument recent)
    {
        _services.RecentDocuments.Remove(recent.FilePath);
        RefreshRecentDocuments();
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        this.RaisePropertyChanged(nameof(WindowTitle));
        this.RaisePropertyChanged(nameof(StatusMessage));
        LocalisationRefresh.RefreshCore(_folderSearch, visited);
        LocalisationRefresh.RefreshCore(_services, visited);
        LocalisationRefresh.RefreshCore(_activeTab, visited);
        LocalisationRefresh.RefreshCore(Tabs, visited);
        LocalisationRefresh.RefreshCore(FoundTabs, visited);
    }

    /// <summary>Converts a path or <c>file://</c> URI to a full local path.</summary>
    /// <param name="item">The item.</param>
    /// <returns>The path, or <see langword="null"/> when it is not local.</returns>
    private static string? ToLocalPath(string item)
    {
        if (string.IsNullOrWhiteSpace(item))
        {
            return null;
        }

        if (Uri.TryCreate(item, UriKind.Absolute, out var uri) && uri.IsFile)
        {
            return uri.LocalPath;
        }

        return Path.IsPathRooted(item) || !item.Contains("://", StringComparison.Ordinal) ? Path.GetFullPath(item) : null;
    }

    /// <summary>Runs the document actions that happen before a document closes, for each tab about to close.</summary>
    /// <param name="closing">The tabs.</param>
    /// <returns>A task.</returns>
    private static async Task RunWillCloseAsync(List<DocumentTabViewModel> closing)
    {
        foreach (var tab in closing)
        {
            _ = await tab.Forms.RunDocumentEventAsync(FormDocumentEvent.WillClose).ConfigureAwait(true);
        }
    }

    /// <summary>Clears the recent PDF list shown by this app.</summary>
    [ReactiveCommand]
    private void ClearRecent()
    {
        _services.RecentDocuments.Clear();
        RefreshRecentDocuments();
    }

    /// <summary>Splits the view of a document in two, beside each other, or puts the second view away.</summary>
    /// <param name="tab">The document's first view, or its second view.</param>
    private void ToggleSplit(DocumentTabViewModel tab)
    {
        if (SplitTab is not null)
        {
            CloseSplit();
            return;
        }

        // The second view shares the open document, so edits made in either view are in the one file.
        var split = new DocumentTabViewModel(tab.Source, _services) { PageTone = PageTone, OpenDocument = OpenLinked, ToggleSplitView = ToggleSplit, IsSecondaryView = true };
        split.ReportPosition(tab.Position, tab.CurrentPageIndex);
        split.IsSelected = true;
        tab.IsSplitView = true;
        split.IsSplitView = true;
        SplitTab = split;
    }

    /// <summary>Stops the work of the tab that was shown, and puts away a second view of a document other than the newly selected one.</summary>
    /// <param name="next">The tab selected now.</param>
    private void LeaveActiveTab(DocumentTabViewModel? next)
    {
        if (_activeTab is { } left)
        {
            left.IsSelected = false;
        }

        if (SplitTab is { } split && !ReferenceEquals(split.Source, next?.Source))
        {
            CloseSplit();
        }

        _activeTab = next;
    }

    /// <summary>Tells the window that its title changed.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RaiseWindowTitle() => this.RaisePropertyChanged(nameof(WindowTitle));

    /// <summary>Releases the second view and every tab, like closing them: the pool drops each source, which closes its document and forgets its passwords.</summary>
    private void ReleaseTabs()
    {
        SplitTab?.Dispose();
        foreach (var tab in Tabs)
        {
            tab.Dispose();
            _services.Pool.Remove(tab.Source);
        }

        Tabs.Clear();
    }

    /// <summary>Puts the second view away.</summary>
    private void CloseSplit()
    {
        if (SplitTab is not { } split)
        {
            return;
        }

        SplitTab = null;
        foreach (var tab in Tabs)
        {
            tab.IsSplitView = false;
        }

        split.Dispose();
    }

    /// <summary>Determines whether the recent documents shown already match the latest list.</summary>
    /// <param name="latest">The latest list.</param>
    /// <returns><see langword="true"/> when nothing changed.</returns>
    private bool SameDocuments(IReadOnlyList<RecentDocument> latest)
    {
        if (latest.Count != RecentDocuments.Count)
        {
            return false;
        }

        for (var i = 0; i < latest.Count; i++)
        {
            if (latest[i] != RecentDocuments[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Opens a PDF a link or attachment points to, at a page, in a tab beside the current one.</summary>
    /// <param name="path">The full path.</param>
    /// <param name="pageIndex">The zero-based page.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OpenLinked(string path, int pageIndex) => OpenAt(path, pageIndex, string.Empty);

    /// <summary>Gets the page a document opens at: where it was last closed, when that is wanted.</summary>
    /// <param name="path">The full path.</param>
    /// <returns>The zero-based page.</returns>
    private int StartPage(string path) =>
        _services.Settings.ReopenAtLastPage ? LastPages.Find(_services.Settings.LastPages, path) : 0;

    /// <summary>Records the page a tab shows, so the document reopens there.</summary>
    /// <param name="tab">The tab.</param>
    private void RememberPage(DocumentTabViewModel tab)
    {
        if (tab.FilePath.Length > 0)
        {
            LastPages.Remember(_services.Settings.LastPages, tab.FilePath, tab.CurrentPageIndex);
        }
    }

    /// <summary>Finds an open tab for a file or opens a new one.</summary>
    /// <param name="path">The full path.</param>
    /// <param name="pageIndex">The page to show in a new tab.</param>
    /// <returns>The tab.</returns>
    private DocumentTabViewModel OpenOrFind(string path, int pageIndex)
    {
        foreach (var existing in Tabs)
        {
            if (string.Equals(existing.FilePath, path, StringComparison.Ordinal))
            {
                return existing;
            }
        }

        var tab = new DocumentTabViewModel(_services.Pool.Create(path, null), _services) { PageTone = PageTone, OpenDocument = OpenLinked, ToggleSplitView = ToggleSplit };
        if (pageIndex > 0)
        {
            tab.ReportPosition(new(pageIndex, 0), pageIndex);
        }

        var insertAt = SelectedTab is { } selected ? Tabs.IndexOf(selected) + 1 : Tabs.Count;
        Tabs.Insert(Math.Clamp(insertAt, 0, Tabs.Count), tab);
        UpdateHasTabs();
        return tab;
    }

    /// <summary>Shows the Search in Folder window, starting in the selected document's folder.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task SearchFolderAsync()
    {
        var search = FolderSearch;
        if (search.Folder.Length == 0 && SelectedTab is { } tab && Path.GetDirectoryName(tab.FilePath) is { Length: > 0 } folder)
        {
            search.Folder = folder;
        }

        _ = await ShowFolderSearchInteraction.Handle(search).ToTask().ConfigureAwait(true);
    }

    /// <summary>Downloads a remote document and opens it.</summary>
    /// <param name="uri">The URI.</param>
    /// <returns>A task.</returns>
    private async Task OpenRemoteAsync(Uri uri)
    {
        SetStatusMessage(LocalizedMessage.Format(UiText, "Downloading {0}…", uri.Host));
        var token = _lifetime;
        try
        {
            var path = await _services.Downloader.DownloadAsync(uri, token).ConfigureAwait(true);

            // A download that finished just as the window closed must not open a tab in a disposed view model.
            token.ThrowIfCancellationRequested();
            StatusMessage = null;
            Open([path]);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // The window closed while the file was coming, so there is no one left to show it to.
        }
        catch (HttpRequestException ex)
        {
            SetStatusMessage(LocalizedMessage.Format(UiText, "Download failed: {0}", ex.Message));
        }
        catch (InvalidDataException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    /// <summary>Asks the view for files and opens them.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task OpenAsync()
    {
        var files = await OpenFileInteraction.Handle(RxVoid.Default);
        Open(files);
    }

    /// <summary>Asks the view to show properties of the selected document.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task PropertiesAsync()
    {
        if (SelectedTab is { IsLoaded: true } tab)
        {
            _ = await ShowPropertiesInteraction.Handle(tab);
        }
    }

    /// <summary>Asks the view to show the preferences; they stop following the settings when the window closes.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task PreferencesAsync()
    {
        using PreferencesViewModel preferences = new(_services);
        _ = await ShowPreferencesInteraction.Handle(preferences).ToTask().ConfigureAwait(true);
    }

    /// <summary>Asks the view to show this application's licence and the licences of what it includes.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task LicencesAsync()
    {
        using LicencesViewModel licences = new(_services.LicenceNotices.Load());
        _ = await ShowLicencesInteraction.Handle(licences).ToTask().ConfigureAwait(true);
    }

    /// <summary>Shows the selected document in the file manager.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task ShowInFolderAsync()
    {
        if (SelectedTab is { } tab && !await _services.FileManager.ShowItemAsync(tab.FilePath, CancellationToken.None).ConfigureAwait(true))
        {
            SetStatusMessage(LocalizedMessage.Get(UiText, "No file manager is available."));
        }
    }

    /// <summary>Closes every tab except one, asking first when more than one would close.</summary>
    /// <param name="keep">The tab to keep; null keeps the selected tab.</param>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task CloseOtherTabsAsync(DocumentTabViewModel? keep)
    {
        keep ??= SelectedTab;
        var closing = new List<DocumentTabViewModel>(Tabs.Count);
        foreach (var tab in Tabs)
        {
            if (tab != keep)
            {
                closing.Add(tab);
            }
        }

        if (!await ConfirmCloseAsync(closing.Count).ConfigureAwait(true))
        {
            return;
        }

        await RunWillCloseAsync(closing).ConfigureAwait(true);
        CloseGroup(closing);
    }

    /// <summary>Closes every tab, asking first when there is more than one.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task CloseAllTabsAsync()
    {
        if (!await ConfirmCloseAsync(Tabs.Count).ConfigureAwait(true))
        {
            return;
        }

        List<DocumentTabViewModel> closing = [.. Tabs];
        await RunWillCloseAsync(closing).ConfigureAwait(true);
        CloseGroup(closing);
        RefreshRecentDocuments();
    }

    /// <summary>Asks before closing several tabs at once.</summary>
    /// <param name="count">The number of tabs that would close.</param>
    /// <returns><see langword="true"/> to go ahead.</returns>
    private async Task<bool> ConfirmCloseAsync(int count)
    {
        if (count < ConfirmCloseThreshold)
        {
            return count == 0 || await ConfirmDiscardAsync().ConfigureAwait(true);
        }

        var request = new ConfirmRequest(
            LocalizedMessage.Format(UiText, "Close {0} tabs?", count),
            LocalizedMessage.Get(UiText, "You can reopen them together with Reopen Closed Tabs (Ctrl+Shift+T)."),
            LocalizedMessage.Get(UiText, "Close Tabs"));
        return await ConfirmInteraction.Handle(request).ToTask().ConfigureAwait(true);
    }

    /// <summary>Closes several tabs as one group that reopens together.</summary>
    /// <param name="closing">The tabs.</param>
    private void CloseGroup(List<DocumentTabViewModel> closing)
    {
        if (closing.Count == 0)
        {
            return;
        }

        Remember(closing);
        foreach (var tab in closing)
        {
            Close(tab, Tabs.IndexOf(tab));
        }
    }

    /// <summary>Removes and disposes a tab, selecting a neighbour when it was selected.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="index">Its index.</param>
    private void Close(DocumentTabViewModel tab, int index)
    {
        if (index < 0)
        {
            return;
        }

        var wasSelected = tab == SelectedTab;
        if (tab.IsSplitView)
        {
            CloseSplit();
        }

        RememberPage(tab);
        Tabs.RemoveAt(index);
        tab.Dispose();
        _services.Pool.Remove(tab.Source);
        if (wasSelected)
        {
            SelectedTab = Tabs.Count == 0 ? null : Tabs[Math.Min(index, Tabs.Count - 1)];
        }

        UpdateHasTabs();
    }

    /// <summary>Selects the next tab, wrapping around.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void NextTab() => CycleTab(1);

    /// <summary>Selects the previous tab, wrapping around.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void PreviousTab() => CycleTab(-1);

    /// <summary>Selects a tab from the tab finder; a null tab keeps the current one.</summary>
    /// <param name="tab">The tab.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void GoToTab(DocumentTabViewModel? tab) => SelectedTab = tab ?? SelectedTab;

    /// <summary>Opens the tab finder.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ShowTabFinder() => IsTabFinderOpen = true;

    /// <summary>Clears the status message.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void DismissStatus() => StatusMessage = null;

    /// <summary>Publishes a status while retaining its resource identity for culture changes.</summary>
    /// <param name="message">The resource message or literal exception text.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetStatusMessage(in LocalizedMessage message) => this.RaiseAndSetIfChanged(ref _statusMessage, message, nameof(StatusMessage));

    /// <summary>Switches between the comfort page colour and plain white pages.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void TogglePageTone() => PageToneEnabled = !PageToneEnabled;

    /// <summary>Opens another window, showing the selected document at the same page.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task NewWindowAsync()
    {
        var window = new MainViewModel(_services) { IsSecondaryWindow = true, PageToneEnabled = PageToneEnabled };
        if (SelectedTab is { } current && current.FilePath.Length > 0)
        {
            window.OpenAt(current.FilePath, Math.Max(0, current.CurrentPageIndex), string.Empty);
        }

        _ = await NewWindowInteraction.Handle(window).ToTask().ConfigureAwait(true);
    }

    /// <summary>Opens a recent document.</summary>
    /// <param name="recent">The document.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void OpenRecent(RecentDocument recent) => Open([recent.FilePath]);

    /// <summary>Selects the next or previous tab, wrapping around.</summary>
    /// <param name="direction">1 or -1.</param>
    private void CycleTab(int direction)
    {
        if (Tabs.Count == 0)
        {
            return;
        }

        var index = SelectedTab is null ? 0 : Tabs.IndexOf(SelectedTab);
        SelectedTab = Tabs[(index + direction + Tabs.Count) % Tabs.Count];
    }

    /// <summary>Reopens the most recently closed tab, or every tab of a group closed together.</summary>
    [ReactiveCommand]
    private void ReopenClosedTab()
    {
        if (_closedTabs.Count == 0)
        {
            return;
        }

        var group = _closedTabs[^1];
        _closedTabs.RemoveAt(_closedTabs.Count - 1);
        DocumentTabViewModel? last = null;
        foreach (var entry in group)
        {
            if (File.Exists(entry.FilePath))
            {
                last = OpenOrFind(entry.FilePath, entry.PageIndex);
            }
        }

        if (last is not null)
        {
            SelectedTab = last;
        }
    }

    /// <summary>Remembers closed tabs so they can be reopened together.</summary>
    /// <param name="tabs">The tabs.</param>
    private void Remember(List<DocumentTabViewModel> tabs)
    {
        if (_closedTabs.Count >= ClosedTabMemory)
        {
            _closedTabs.RemoveAt(0);
        }

        var group = new SessionTab[tabs.Count];
        for (var i = 0; i < group.Length; i++)
        {
            group[i] = new() { FilePath = tabs[i].FilePath, PageIndex = Math.Max(0, tabs[i].CurrentPageIndex) };
        }

        _closedTabs.Add(group);
    }

    /// <summary>Updates <see cref="HasTabs"/>.</summary>
    private void UpdateHasTabs() => HasTabs = Tabs.Count > 0;

    /// <summary>Saves the page colour toggle and applies it.</summary>
    /// <param name="enabled">Whether the comfort page colour is on.</param>
    private void ApplyPageTone(bool enabled)
    {
        if (enabled == _services.Settings.PageToneEnabled)
        {
            return;
        }

        _services.Settings.PageToneEnabled = enabled;
        _services.ApplySettings();
    }

    /// <summary>Takes the settings that change what is open: the page colour switch and the tint over fillable form fields.</summary>
    private void TakeSettings()
    {
        PageToneEnabled = _services.Settings.PageToneEnabled;
        var settings = new TabSettings(_services.FormHighlight, _services.Sharpness);

        // An unrelated setting change leaves the tint and sharpness as they were, so no tab is asked about them.
        if (_appliedTabSettings == settings)
        {
            return;
        }

        _appliedTabSettings = settings;
        foreach (var tab in Tabs)
        {
            // Only the selected tab draws; the others keep the settings and take them when selected, before they draw.
            if (ReferenceEquals(tab, SelectedTab))
            {
                tab.ApplyFormHighlight(settings.Highlight);
                tab.ApplyRenderSharpness(settings.Sharpness);
            }
            else
            {
                tab.PendingSettings.Defer(settings);
            }
        }
    }

    /// <summary>Gives a new theme's page tone to the window and every tab.</summary>
    /// <param name="tone">The page tone.</param>
    private void ShareTone(PageTone tone)
    {
        PageTone = tone;
        foreach (var tab in Tabs)
        {
            tab.PageTone = tone;
        }
    }
}
