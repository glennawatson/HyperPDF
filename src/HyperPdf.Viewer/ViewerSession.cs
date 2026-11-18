// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Reading;
using HyperPdf.Core.Theming;
using HyperPdf.Presentation.Canvas;
using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.FormFilling;
using HyperPdf.ViewModels.Markup;
using HyperPdf.ViewModels.Preferences;
using HyperPdf.ViewModels.Services;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Viewer.Accessibility;
using HyperPdf.Viewer.Chrome;
using HyperPdf.Viewer.Chrome.Forms;
using HyperPdf.Windowing.Windows;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Concurrency;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Extensions;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer;

/// <summary>
/// One viewer window at work: the view models, the page canvas and the loop that waits for window events and posted work. The loop
/// turns what the window reports into streams, and everything else in the window follows those streams. A platform head opens its
/// window, makes a session on the same thread and runs it.
/// </summary>
[DebuggerDisplay("ViewerSession: closed {_closed}")]
public sealed class ViewerSession : IDisposable
{
    /// <summary>Half of a length.</summary>
    private const float Half = 0.5F;

    /// <summary>The wait for events that has no limit.</summary>
    private const int NoLimit = -1;

    /// <summary>The smallest saved width or height that is used to open the window, so a bad value cannot open a sliver.</summary>
    private const double MinimumWindowSize = 400;

    /// <summary>What the session owns.</summary>
    private readonly MultipleDisposable _owned = [];

    /// <summary>What follows the tab shown: its dialogs, its password question and its typing. Replaced when another tab is shown.</summary>
    private readonly SwapDisposable _tabStreams = new();

    /// <summary>Signals the end of each turn of the loop, after its events and posted work are handled.</summary>
    private readonly Signal<RxVoid> _turns = new();

    /// <summary>The window.</summary>
    private readonly IPlatformWindow _window;

    /// <summary>The application services the session was made with.</summary>
    private readonly AppServices _services;

    /// <summary>The context that brings posted work to this thread.</summary>
    private readonly WindowThreadContext _context;

    /// <summary>Whether the user asked to close the window.</summary>
    private bool _closed;

    /// <summary>Whether the reader chose to close with unsaved edits, so the close is not asked about again.</summary>
    private bool _closeConfirmed;

    /// <summary>The canvas of the second view of a split document, or <see langword="null"/> when the view is whole.</summary>
    private ViewerCanvas? _split;

    /// <summary>Whether the keys go to the second view: it was the last one pressed on.</summary>
    private bool _keysInSecond;

    /// <summary>Whether the pointer was last over the second view.</summary>
    private bool _pointerInSecond;

    /// <summary>Initializes a new instance of the <see cref="ViewerSession"/> class on the thread that pumps the window.</summary>
    /// <param name="window">The window.</param>
    /// <param name="services">The application services, made with a <see cref="ViewerRenderHead"/> for the same window.</param>
    /// <param name="context">The context from <see cref="UseWindowThread"/>.</param>
    public ViewerSession(IPlatformWindow window, AppServices services, WindowThreadContext context)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(context);
        _window = window;
        _context = context;
        _services = services;
        Main = new(services);

        // The controls sit over the pages, so they take the events first and pass on what they did not use.
        Chrome = new(window, Main, ChromeTheme.From(services.CurrentTheme.Scheme), window.Events.Where(static e => e.Kind != WindowEventKind.CloseRequested));
        Chrome.ShowLabels = services.CurrentTheme.ShowLabels;
        Canvas = new(window) { Colors = ViewerColors.From(services.CurrentTheme.Scheme), Chrome = Chrome };
        Accessibility = new(window);
        window.Accessibility.Attach(Accessibility);
        _owned.Add(Accessibility);
        _owned.Add(Canvas);
        _owned.Add(Chrome);
        _owned.Add(Main);
        _owned.Add(window.Events.Where(static e => e.Kind == WindowEventKind.CloseRequested).SubscribeSafe(_ => RequestClose(), ErrorTrace.Write));
        _owned.Add(Chrome.PageEvents.SubscribeSafe(RoutePageEvent, ErrorTrace.Write));
        _owned.Add(Accessibility.Presses.ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(PressControl, ErrorTrace.Write));
        _owned.Add(Accessibility.FocusRequests.ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(FocusControl, ErrorTrace.Write));

        // Files dropped on the window open as tabs.
        _owned.Add(window.FileDrops.ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(Main.Open, ErrorTrace.Write));

        // A screen reader that moves the caret through the page text moves the one the keyboard uses in caret mode.
        _owned.Add(Accessibility.CaretRequests.ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(place => Canvas.State.MoveCaret(place, false), ErrorTrace.Write));
        _owned.Add(_turns.SubscribeSafe(_ => Accessibility.RefreshIfRead(Main, Canvas), ErrorTrace.Write));
        _owned.Add(Main.OpenFileInteraction.RegisterHandler(context => Chrome.PickFiles(StartFolder()).Do(context.SetOutput)));
        _owned.Add(Main.ConfirmInteraction.RegisterHandler(context => Chrome.Confirm(context.Input).Do(context.SetOutput)));
        _owned.Add(Main.ShowPreferencesInteraction.RegisterHandler(context => Chrome.ShowPreferences(context.Input).AsSignal().Do(context.SetOutput)));
        _owned.Add(Main.ShowLicencesInteraction.RegisterHandler(ShowLicences));
        _owned.Add(Main.ShowPropertiesInteraction.RegisterHandler(context => Chrome.ShowProperties(new(context.Input)).AsSignal().Do(context.SetOutput)));
        _owned.Add(TabRequests.Follow(Chrome, Main));

        // What the status bar says is also spoken, so a screen reader user hears that a save or an open finished.
        _owned.Add(Main.WhenChanged(static main => main.StatusMessage).Skip(1).ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(Announce, ErrorTrace.Write));
        _owned.Add(Main.WhenChanged(static main => main.IsTabFinderOpen)
            .Where(static open => open)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SwitchMap(_ => Chrome.ShowForm(ToolForms.TabFinder(Main)).Finally(() => Main.IsTabFinderOpen = false))
            .SubscribeSafe(static _ => { }, ErrorTrace.Write));
        _owned.Add(services.Theme.ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(ShowTheme, ErrorTrace.Write));
        _owned.Add(Main.WhenChanged(static main => main.SelectedTab).SubscribeSafe(ShowTab, ErrorTrace.Write));
        _owned.Add(Main.WhenChanged(static main => main.SplitTab).SubscribeSafe(ShowSplit, ErrorTrace.Write));
    }

    /// <summary>Gets the main view model.</summary>
    public MainViewModel Main { get; }

    /// <summary>Gets the page canvas.</summary>
    public ViewerCanvas Canvas { get; }

    /// <summary>Gets the window's controls: the tab strip, the toolbar, the status bar, and the menus and dialogs over them.</summary>
    public ViewerChrome Chrome { get; }

    /// <summary>Gets the tree the window's accessibility bridge serves to screen readers and magnifiers.</summary>
    public ViewerAccessibleTree Accessibility { get; }

    /// <summary>Gets the window's events, on the window thread, as they are pumped.</summary>
    public IObservable<WindowEvent> Events => _window.Events;

    /// <summary>Gets a signal for the end of each turn of the loop, after the turn's events and posted work are handled.</summary>
    public IObservable<RxVoid> Turns => _turns;

    /// <summary>Makes the calling thread the window thread: posted work and awaits come back to it, and ReactiveUI's main thread scheduler posts to it.</summary>
    /// <param name="window">The window this thread pumps.</param>
    /// <returns>The context to make the session with.</returns>
    public static WindowThreadContext UseWindowThread(IPlatformWindow window)
    {
        var context = new WindowThreadContext(window);
        SynchronizationContext.SetSynchronizationContext(context);
        RxSchedulers.MainThreadScheduler = new SynchronizationContextSequencer(context);
        return context;
    }

    /// <summary>Asks for the language of the viewer when it has never been chosen, and keeps the choice for the next start. Heads call this once, after the window is open.</summary>
    public void AskForLanguage()
    {
        if (!LanguageChoiceViewModel.IsNeeded(_services))
        {
            return;
        }

        // Closing the window without pressing Continue keeps what it showed, so the question is not asked at every start.
        var choice = new LanguageChoiceViewModel(_services);
        _owned.Add(Chrome.ShowForm(LanguageForms.For(choice)).SubscribeSafe(static _ => { }, ErrorTrace.Write, choice.Keep));
    }

    /// <summary>Opens documents in tabs.</summary>
    /// <param name="paths">The files or addresses.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Open(IEnumerable<string> paths) => Main.Open(paths);

    /// <summary>Opens each file that a second launch hands over, in a tab of this window.</summary>
    /// <param name="paths">The files or addresses, which may arrive on any thread.</param>
    public void Follow(IObservable<string> paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        _owned.Add(paths.ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(OpenAndRaise, ErrorTrace.Write));
    }

    /// <summary>Runs until the user closes the window.</summary>
    public void Run()
    {
        // The window opens at the size it had when it was last closed.
        var settings = _services.Settings;
        if (!Main.IsSecondaryWindow && settings.WindowWidth >= MinimumWindowSize && settings.WindowHeight >= MinimumWindowSize)
        {
            _window.SetSize((int)Math.Round(settings.WindowWidth), (int)Math.Round(settings.WindowHeight));
        }

        _window.Show();
        if (!Main.IsSecondaryWindow && settings.WindowMaximized)
        {
            _window.SetMaximized(true);
        }

        while (PumpOnce(NoLimit))
        {
            // Each turn handles the events and posted work that woke it.
        }
    }

    /// <summary>Waits for window events or posted work and handles what arrived.</summary>
    /// <param name="timeoutMilliseconds">The longest wait, or -1 for no limit.</param>
    /// <returns><see langword="false"/> once the user asked to close the window.</returns>
    public bool PumpOnce(int timeoutMilliseconds)
    {
        // Work that is already waiting must not sit behind a wait for the window. Work posted after this check wakes the wait itself.
        _window.PumpEvents(_context.HasWork ? 0 : timeoutMilliseconds);
        _context.Drain();

        // Posted work changes view models without a window event, so whatever reads them once a turn is told after it too.
        _turns.OnNext(RxVoid.Default);
        return !_closed;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _owned.Dispose();
        _split?.Dispose();
        _tabStreams.Dispose();
        _turns.Dispose();
    }

    /// <summary>Makes the question that asks for the password of a locked document.</summary>
    /// <param name="tab">The tab whose document is locked.</param>
    /// <returns>The question.</returns>
    private static PromptRequest PasswordRequest(DocumentTabViewModel tab) =>
        new(UiText.Get("This document is locked"), UiText.Format("Type the password for {0}.", tab.FileName), string.Empty, UiText.Get("Open"), true);

    /// <summary>
    /// Handles the window being asked to close. With unsaved edits the close is held and the question asked once; closing again after the
    /// answer goes through, remembering the window's size and the session.
    /// </summary>
    private void RequestClose()
    {
        if (_closeConfirmed || !Main.HasUnsavedTabs)
        {
            FinishClose();
            return;
        }

        if (Main.IsConfirmingDiscard)
        {
            return;
        }

        _owned.Add(Main.ConfirmDiscard().ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(AnswerClose, ErrorTrace.Write));
    }

    /// <summary>Closes the window when the reader chose to discard unsaved edits.</summary>
    /// <param name="confirmed">Whether the reader chose to close without saving.</param>
    private void AnswerClose(bool confirmed)
    {
        if (!confirmed)
        {
            return;
        }

        _closeConfirmed = true;
        FinishClose();
    }

    /// <summary>Remembers the window's size and the open documents for the next start, and ends the run.</summary>
    private void FinishClose()
    {
        // A window that fills the screen has a size that is not its own, and a second window leaves the saved size to the first.
        var fullView = Chrome.IsFullScreen || Main.SelectedTab is { IsPresenting: true };
        if (!Main.IsSecondaryWindow && !fullView)
        {
            // A maximised window's size is the screen's, so only its state is kept and the size from before stays.
            var size = _window.ClientSize;
            var scale = _window.Scale;
            var maximized = _window.IsMaximized;
            Main.RememberWindow(size.Width / scale, size.Height / scale, maximized, !maximized);
        }

        Main.SaveSession();
        _closed = true;
    }

    /// <summary>Has a screen reader speak a message.</summary>
    /// <param name="message">The words, or <see langword="null"/> for nothing.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Announce(string? message) => Accessibility.Announce(message ?? string.Empty);

    /// <summary>Gives the keyboard to the control an assistive technology asked for.</summary>
    /// <param name="id">The id of the control's node in the accessibility tree.</param>
    private void FocusControl(int id)
    {
        var inOverlay = id >= ViewerNodes.FirstOverlayId;
        _ = Chrome.FocusControl(inOverlay, id - (inOverlay ? ViewerNodes.FirstOverlayId : ViewerNodes.FirstControlId));
    }

    /// <summary>Presses the control an assistive technology asked for.</summary>
    /// <param name="id">The id of the control's node in the accessibility tree.</param>
    private void PressControl(int id)
    {
        if (id >= ViewerNodes.FirstPageItemId)
        {
            PressPageItem(id);
            return;
        }

        var inOverlay = id >= ViewerNodes.FirstOverlayId;
        _ = Chrome.PressControl(inOverlay, id - (inOverlay ? ViewerNodes.FirstOverlayId : ViewerNodes.FirstControlId));
    }

    /// <summary>Presses a form field, a link or a note on the page for an assistive technology, the way a click in its middle does.</summary>
    /// <param name="id">The id of the item's node in the accessibility tree.</param>
    private void PressPageItem(int id)
    {
        if (Accessibility.Get(id) is not { } node)
        {
            return;
        }

        // A form field is filled in without a click, so it works wherever the field is, even where the window is too small to show it.
        if (ActivateFieldItem(id))
        {
            return;
        }

        var origin = _window.ScreenOrigin;
        var x = node.Bounds.X - origin.X + (node.Bounds.Width >> 1);
        var y = node.Bounds.Y - origin.Y + (node.Bounds.Height >> 1);
        Canvas.Input.OnNext(new(WindowEventKind.PointerMoved, x, y, 0, 0));
        Canvas.Input.OnNext(new(WindowEventKind.PointerDown, x, y, (uint)ViewerButtons.Left, 0));
        Canvas.Input.OnNext(new(WindowEventKind.PointerUp, x, y, (uint)ViewerButtons.Left, 0));
    }

    /// <summary>Starts filling in the form field an item of the page stands for.</summary>
    /// <param name="id">The id of the item's node in the accessibility tree.</param>
    /// <returns>Whether the item was a form field that took the press.</returns>
    private bool ActivateFieldItem(int id)
    {
        var page = ViewerNodes.PageOfItem(id);
        var index = ViewerNodes.IndexOfItem(id);
        if (Main.SelectedTab is not { } tab || DocumentFeature<IPageSemanticSource>.Get(tab.TryGetDocument())?.GetItems(page) is not { } items)
        {
            return false;
        }

        if ((uint)index >= (uint)items.Count || items[index] is not { Kind: PageSemanticKind.FormField } item)
        {
            return false;
        }

        var middle = new PagePoint(item.Bounds.Left + (item.Bounds.Width * Half), item.Bounds.Top + (item.Bounds.Height * Half));
        return tab.Forms.HitTest(page, middle) is { } field && tab.Forms.Activate(field);
    }

    /// <summary>Shows the selected tab on the canvas and its name in the title bar, and follows what the tab asks of the window.</summary>
    /// <param name="tab">The tab, or <see langword="null"/> when none is open.</param>
    private void ShowTab(DocumentTabViewModel? tab)
    {
        Canvas.ShowTab(tab);
        _window.SetTitle(Main.WindowTitle);

        // With no document open, the start view stands where the pages would be.
        if (tab is null)
        {
            Chrome.ShowStart(Main);
        }
        else
        {
            Chrome.CloseStart();
        }

        _tabStreams.Disposable = tab is null ? null : FollowTab(tab);
    }

    /// <summary>Follows what a tab asks of the window: its dialogs, the password of a locked document and the text typed into a field or a text box.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <returns>What ends the following.</returns>
    private MultipleDisposable FollowTab(DocumentTabViewModel tab)
    {
        var forms = tab.Forms;
        var marks = tab.Annotations;
        return
        [
            tab.SaveAsInteraction.RegisterHandler(context => Chrome.PickFile(StartFolder(), Path.GetFileName(context.Input)).Do(context.SetOutput)),
            marks.PromptInteraction.RegisterHandler(AskForText),
            forms.ChooseImportFileInteraction.RegisterHandler(context => Chrome.PickFile(StartFolder(), null).Do(context.SetOutput)),
            forms.ConfirmImportInteraction.RegisterHandler(ConfirmImport),
            tab.Printing.PrintPreviewInteraction.RegisterHandler(context => Chrome.ShowPrint(context.Input).Do(context.SetOutput)),
            TabRequests.Follow(Chrome, tab),

            // How many matches a search found is spoken as it changes.
            tab.Search.WhenChanged(static search => search.Status).Skip(1).ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(Announce, ErrorTrace.Write),

            // A locked document asks for its password when its tab is shown, and again after a wrong one. A question the user turns
            // down is not asked again until the tab is shown again.
            tab.WhenChanged(static t => t.NeedsPassword)
                .Where(static needed => needed)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .SwitchMap(_ => Chrome.Prompt(PasswordRequest(tab)).KeepNotNull())
                .Do(password => tab.PasswordEntry = password)
                .AsSignal()
                .InvokeCommand(tab.SubmitPasswordCommand),

            // Text is typed where it lies on the page: a box opens over the form field or the text box the view model starts
            // typing in, and closes when the view model ends the typing.
            // The value is read when the change is delivered, because the deliveries queue and an old one must not undo a newer one.
            forms.WhenChanged(static f => f.Editing)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Select(_ => forms.Editing)
                .DistinctUntilChanged()
                .SubscribeSafe(field => ShowTyping(field is null ? null : FormFieldTypings.For(forms, field)), ErrorTrace.Write),

            // A document locked with a certificate asks for the certificate when its tab is shown.
            tab.WhenChanged(static t => t.NeedsCertificate)
                .Where(static needed => needed)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .SwitchMap(_ => Chrome.ShowForm(ToolForms.CertificateLock(tab)))
                .SubscribeSafe(static _ => { }, ErrorTrace.Write),

            // A note on the page opens in a window with its replies. Closing the window puts the note away.
            marks.WhenChanged(static m => m.ActiveBubble)
                .KeepNotNull()
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .SwitchMap(_ => Chrome.ShowForm(ToolForms.NoteBubble(marks)).Finally(() => Signal.Return(RxVoid.Default).InvokeCommand(marks.CloseBubbleCommand).Dispose()))
                .SubscribeSafe(static _ => { }, ErrorTrace.Write),

            // Presenting fills the screen with one page at a time, and stopping gives the window back.
            tab.WhenChanged(static t => t.IsPresenting)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Finally(() => _window.SetFullScreen(false))
                .SubscribeSafe(presenting => Present(tab, presenting), ErrorTrace.Write),
            tab.FocusMode.WhenChanged(static focus => focus.IsOn)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .SubscribeSafe(on => ShowFocus(tab, on), ErrorTrace.Write),
            marks.WhenChanged(static m => m.TextEdit)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Select(_ => marks.TextEdit)
                .DistinctUntilChanged()
                .SubscribeSafe(edit => ShowTyping(edit is null ? null : TextBoxTypings.For(forms, marks, edit)), ErrorTrace.Write),
        ];
    }

    /// <summary>Fills the screen with the page while presenting, and starts the timed turning of pages and the page transitions the document asks for.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <param name="presenting">Whether the tab is presenting.</param>
    private void Present(DocumentTabViewModel tab, bool presenting)
    {
        if (presenting)
        {
            _ = tab.PagePresentation;
        }

        _window.SetFullScreen(presenting);
    }

    /// <summary>Shows focus mode in place of the pages while it is on, and the pages again when it is turned off.</summary>
    /// <param name="tab">The tab shown.</param>
    /// <param name="on">Whether focus mode is on.</param>
    private void ShowFocus(DocumentTabViewModel tab, bool on)
    {
        if (on)
        {
            Chrome.ShowFocus(tab.FocusMode);
            return;
        }

        Chrome.CloseFocus();
    }

    /// <summary>Opens the typing box over what the view model started typing in, or closes it when the typing ended.</summary>
    /// <param name="target">What is typed into, or <see langword="null"/> when the typing ended.</param>
    private void ShowTyping(TypingTarget? target)
    {
        if (target is null)
        {
            Chrome.CloseTyping();
            return;
        }

        Chrome.ShowTyping(target, Canvas);
    }

    /// <summary>Shows the licences window. Its copy button puts the chosen licence on the clipboard.</summary>
    /// <param name="context">The interaction to answer.</param>
    /// <returns>One value when the window closes.</returns>
    private IObservable<RxVoid> ShowLicences(IInteractionContext<LicencesViewModel, RxVoid> context)
    {
        var copying = context.Input.CopyInteraction.RegisterHandler(copy =>
        {
            _window.SetClipboardText(copy.Input);
            copy.SetOutput(RxVoid.Default);
        });
        return Chrome.ShowLicences(context.Input).AsSignal().Do(context.SetOutput).Finally(copying.Dispose);
    }

    /// <summary>Opens a file that a second launch handed over and brings the window to the front, so the person who started it sees it.</summary>
    /// <param name="path">The file or address.</param>
    private void OpenAndRaise(string path)
    {
        Main.Open([path]);
        _window.Raise();
    }

    /// <summary>Asks for a line of text, such as the words of a note.</summary>
    /// <param name="context">The interaction to answer.</param>
    /// <returns>One value when the dialog closes.</returns>
    private IObservable<string?> AskForText(IInteractionContext<TextPrompt, string?> context)
    {
        var asked = context.Input;
        return Chrome.Prompt(new(asked.Title, asked.Label, asked.Text, asked.AcceptText, false)).Do(context.SetOutput);
    }

    /// <summary>Asks whether a form may load data from a file.</summary>
    /// <param name="context">The interaction to answer, carrying the file name the form asked for.</param>
    /// <returns>One value when the dialog closes.</returns>
    private IObservable<bool> ConfirmImport(IInteractionContext<string, bool> context)
    {
        var request = new ConfirmRequest(
            UiText.Get("Load data into this form?"),
            UiText.Format("This form asks to fill itself in from the file {0}. You choose the file next.", context.Input),
            UiText.Get("Choose a file"));
        return Chrome.Confirm(request).Do(context.SetOutput);
    }

    /// <summary>
    /// Sends an event of the page area to the view it belongs to. With one view, every event goes to the one canvas. With two, a pointer or
    /// scroll event goes to the view under it, a press also makes that view the one the keys go to, and a new size goes to both.
    /// </summary>
    /// <param name="windowEvent">The event.</param>
    private void RoutePageEvent(WindowEvent windowEvent)
    {
        if (_split is not { } second)
        {
            Canvas.Input.OnNext(windowEvent);
            return;
        }

        switch (windowEvent.Kind)
        {
            case WindowEventKind.Resized:
                {
                    Canvas.Input.OnNext(windowEvent);
                    second.Input.OnNext(windowEvent);
                    break;
                }

            case WindowEventKind.Exposed:
                {
                    Canvas.Input.OnNext(windowEvent);
                    break;
                }

            case WindowEventKind.PointerDown or WindowEventKind.PointerUp or WindowEventKind.PointerMoved:
                {
                    _pointerInSecond = windowEvent.X >= second.Bounds.X;
                    _keysInSecond = windowEvent.Kind == WindowEventKind.PointerDown ? _pointerInSecond : _keysInSecond;
                    (_pointerInSecond ? second : Canvas).Input.OnNext(windowEvent);
                    break;
                }

            case WindowEventKind.Scrolled:
                {
                    // A scroll carries amounts, not a place, so it goes to the view the pointer last was over.
                    (_pointerInSecond ? second : Canvas).Input.OnNext(windowEvent);
                    break;
                }

            default:
                {
                    (_keysInSecond ? second : Canvas).Input.OnNext(windowEvent);
                    break;
                }
        }
    }

    /// <summary>Shows the second view of a split document beside the first, or puts it away.</summary>
    /// <param name="tab">The second view, or <see langword="null"/> when the view is whole.</param>
    private void ShowSplit(DocumentTabViewModel? tab)
    {
        _keysInSecond = false;
        _pointerInSecond = false;
        Canvas.Neighbour = null;
        _split?.Dispose();
        _split = null;
        if (tab is not null)
        {
            _split = new(_window) { Colors = Canvas.Colors, Chrome = Chrome, Partner = Canvas };
            Canvas.Neighbour = _split;
            _split.ShowTab(tab);
        }

        Canvas.Refit();
        _split?.Refit();
    }

    /// <summary>Draws with the colours of a theme.</summary>
    /// <param name="theme">The theme.</param>
    private void ShowTheme(ResolvedTheme theme)
    {
        Canvas.Colors = ViewerColors.From(theme.Scheme);
        if (_split is { } second)
        {
            second.Colors = Canvas.Colors;
        }

        Chrome.Theme = ChromeTheme.From(theme.Scheme);
        Chrome.ShowLabels = theme.ShowLabels;
        _window.RequestFrame();
    }

    /// <summary>Picks the folder a file dialog starts in: the folder of the document shown, or the user's own folder.</summary>
    /// <returns>The folder.</returns>
    private string StartFolder() =>
        Main.SelectedTab is { FilePath: { Length: > 0 } path } && Path.GetDirectoryName(Path.GetFullPath(path)) is { Length: > 0 } folder
            ? folder
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}
