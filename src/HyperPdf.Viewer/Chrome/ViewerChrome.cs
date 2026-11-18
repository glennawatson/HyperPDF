// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Layout;
using HyperPdf.Core.Rendering;
using HyperPdf.Presentation.Input;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Dialogs;
using HyperPdf.ViewModels.Output;
using HyperPdf.ViewModels.Preferences;
using HyperPdf.ViewModels.Reading;
using HyperPdf.ViewModels.Services;
using HyperPdf.ViewModels.Sidebar;
using HyperPdf.ViewModels.Viewing;
using HyperPdf.Viewer.Chrome.Forms;
using HyperPdf.Viewer.Icons;
using HyperPdf.Viewer.Text;
using HyperPdf.Windowing.Rendering;
using HyperPdf.Windowing.Windows;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// The window's own controls around the pages: the tab strip, the toolbar, the status bar, and the menus and dialogs that open over
/// them. It lays them out only when what they show changes, draws them into each frame and takes the input that lands on them.
/// Everything here runs on the window thread.
/// </summary>
[DebuggerDisplay("ViewerChrome: {_bars.Count} controls")]
public sealed class ViewerChrome : IDisposable
{
    /// <summary>The window.</summary>
    private readonly IPlatformWindow _window;

    /// <summary>The main view model.</summary>
    private readonly MainViewModel _main;

    /// <summary>The drawn text.</summary>
    private readonly TextRuns _runs;

    /// <summary>The drawn icons of the controls.</summary>
    private readonly IconRuns _icons;

    /// <summary>The controls of the bars, back to front.</summary>
    private readonly List<ChromeItem> _bars = [];

    /// <summary>The controls of the overlay, back to front.</summary>
    private readonly List<ChromeItem> _overlayItems = [];

    /// <summary>The controls of the preview of the tab the pointer is over, made again each frame it is shown.</summary>
    private readonly List<ChromeItem> _previewItems = [];

    /// <summary>The command of each button of the notice and tool bars, in the order the buttons were laid out.</summary>
    private readonly List<MenuEntry> _toolActions = [];

    /// <summary>Carries the command of each shortcut key pressed to whatever runs it.</summary>
    private readonly Signal<MenuEntry> _shortcuts = new();

    /// <summary>Carries each press of a control of the bars to the view model commands joined to it.</summary>
    private readonly Signal<ChromePress> _presses = new();

    /// <summary>Carries the window events the controls did not use on to the pages.</summary>
    private readonly Signal<WindowEvent> _pageEvents = new();

    /// <summary>Carries every event of the window to the controls, whether it came from the window or was put in by hand.</summary>
    private readonly Signal<WindowEvent> _incoming = new();

    /// <summary>What the controls follow for as long as they live: the window's events, the view models' changes and the presses joined to commands.</summary>
    private readonly MultipleDisposable _owned = [];

    /// <summary>The presses joined to the tab shown, replaced when another tab is shown.</summary>
    private readonly SwapDisposable _tabBindings = new();

    /// <summary>The dialogs that wait under the open one, the last one opened on top. A dialog opened over another one keeps it, and gives the window back to it when it closes.</summary>
    private readonly List<OverlayFrame> _under = [];

    /// <summary>What closed overlays were joined to. An overlay closes while the key or press that closed it is still passing through its input, so it is let go after.</summary>
    private readonly List<IDisposable> _retired = [];

    /// <summary>Which control the keyboard is on.</summary>
    private ChromeFocus _focus = ChromeFocus.None;

    /// <summary>The surface that shows the sheet the print window shows.</summary>
    private SheetFrame _sheetFrame = SheetFrame.Empty;

    /// <summary>The surface that shows the latest picture of the film the media window plays.</summary>
    private MediaFrame _mediaFrame = MediaFrame.Empty;

    /// <summary>What was asked for the pictures of the pages panel.</summary>
    private ThumbnailAsks _thumbnails = ThumbnailAsks.None;

    /// <summary>What the side panel lists, as it was last laid out.</summary>
    private SidebarView _sidebar = SidebarView.Empty;

    /// <summary>What the open overlay is joined to: its changes, its wish to close and everything the overlay itself follows and owns.</summary>
    private IDisposable? _overlayLinks;

    /// <summary>What the controls are drawn with.</summary>
    private ChromeLook _look;

    /// <summary>The menu, dialog or search bar that is open, or <see langword="null"/>.</summary>
    private Overlay? _overlay;

    /// <summary>The control under the pointer, or <see langword="null"/>.</summary>
    private ChromeItem? _hover;

    /// <summary>The part of the window left for the pages.</summary>
    private LayoutRect _canvasBounds;

    /// <summary>Where the pointer last was, in device pixels.</summary>
    private ScreenPoint _pointer;

    /// <summary>A number that goes up each time the controls change, which the accessibility tree compares.</summary>
    private int _version;

    /// <summary>Whether what the bars show changed since they were laid out. They start out needing a layout.</summary>
    private bool _barsChanged = true;

    /// <summary>Whether what the open overlay shows changed since it was laid out.</summary>
    private bool _overlayChanged;

    /// <summary>Whether the pointer has been inside the window yet.</summary>
    private bool _pointerSeen;

    /// <summary>Whether a stroke is being drawn on the dialog's drawing pad, so the pointer moving and being let go belong to it.</summary>
    private bool _padHeld;

    /// <summary>Whether F11 has put the window into full screen.</summary>
    private bool _fullScreen;

    /// <summary>The tab whose preview is shown, or <see langword="null"/> when none is.</summary>
    private DocumentTabViewModel? _previewTab;

    /// <summary>The pictures asked for the preview.</summary>
    private ThumbnailAsks _previewAsks = ThumbnailAsks.None;

    /// <summary>What the controls of the preview were made from, or <see langword="null"/> before the first.</summary>
    private TabPreviewKey? _previewKey;

    /// <summary>The place of the tab the pointer was pressed on, or -1 when it was not pressed on a tab.</summary>
    private int _tabDragFrom = -1;

    /// <summary>Initializes a new instance of the <see cref="ViewerChrome"/> class.</summary>
    /// <param name="window">The window.</param>
    /// <param name="main">The main view model.</param>
    /// <param name="theme">The colours to start with.</param>
    /// <param name="events">The window's events, on the window thread.</param>
    public ViewerChrome(IPlatformWindow window, MainViewModel main, ChromeTheme theme, IObservable<WindowEvent> events)
    {
        ArgumentNullException.ThrowIfNull(window);
        ArgumentNullException.ThrowIfNull(main);
        ArgumentNullException.ThrowIfNull(events);
        _window = window;
        _main = main;
        _runs = new(window.Backend.Surfaces);
        _icons = new(window.Backend.Surfaces);
        _look = new(_runs, theme, ChromeMetrics.For(window.Scale), ThumbnailOf, InkOf) { Icons = _icons };

        // The view models say when something the controls show has changed, on whichever thread changed it. The layout runs on the
        // window's thread, so the signal is brought there first.
        _owned.Add(main.ControlChanges.ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(_ => InvalidateBars(), ErrorTrace.Write));
        _owned.Add(ChromeBindings.Window(_presses, main));
        _owned.Add(_presses.WhereCommand(ChromeCommand.Menu).SubscribeSafe(ShowMainMenu, ErrorTrace.Write));
        _owned.Add(_presses.WhereCommand(ChromeCommand.MarkMore).SubscribeSafe(ShowMoreMarks, ErrorTrace.Write));
        _owned.Add(_presses.WhereCommand(ChromeCommand.Find).SubscribeSafe(_ => ShowFind(), ErrorTrace.Write));
        _owned.Add(main.WhenChanged(static m => m.SelectedTab).SubscribeSafe(JoinTab, ErrorTrace.Write));
        _owned.Add(_shortcuts.SubscribeSafe(RunEntry, ErrorTrace.Write));
        _owned.Add(_presses.WhereCommand(ChromeCommand.ToolAction).SelectWith(_toolActions, static (actions, press) => (uint)press.Index < (uint)actions.Count ? actions[press.Index] : null)
            .KeepNotNull()
            .SubscribeSafe(RunEntry, ErrorTrace.Write));
        _owned.Add(events.SubscribeSafe(_incoming.OnNext, ErrorTrace.Write));
        _owned.Add(RouteEvents());
    }

    /// <summary>Gets the presses of the controls of the bars, by the pointer, a keyboard shortcut or an assistive technology.</summary>
    public IObservable<ChromePress> Presses => _presses;

    /// <summary>Gets what takes the window's events into the controls. An event they use goes no further; any other goes on to <see cref="PageEvents"/>.</summary>
    public IObserver<WindowEvent> Input => _incoming;

    /// <summary>Gets the window events the controls did not use, which belong to the pages.</summary>
    public IObservable<WindowEvent> PageEvents => _pageEvents;

    /// <summary>Gets or sets the colours the controls are drawn with.</summary>
    public ChromeTheme Theme
    {
        get => _look.Theme;
        set
        {
            _look = _look with { Theme = value };
            _window.RequestFrame();
        }
    }

    /// <summary>Gets or sets a value indicating whether tool bar buttons show their names beside their icons.</summary>
    public bool ShowLabels
    {
        get => _look.ShowLabels;
        set
        {
            if (_look.ShowLabels == value)
            {
                return;
            }

            _look = _look with { ShowLabels = value };
            InvalidateBars();
        }
    }

    /// <summary>Gets the part of the window left for the pages, in device pixels.</summary>
    public LayoutRect CanvasBounds
    {
        get
        {
            Layout();
            return _canvasBounds;
        }
    }

    /// <summary>Gets a number that changes whenever the controls change: what the bars show, or which menu or dialog is open and what it shows.</summary>
    public int ControlsVersion => _version;

    /// <summary>Gets the place of the control the keyboard is on in <see cref="Bars"/> or <see cref="OverlayItems"/>, or -1 while the keyboard is on the page.</summary>
    public int FocusedControl => _focus.Index;

    /// <summary>Gets a value indicating whether the control the keyboard is on is in the open menu, dialog or search bar rather than the bars.</summary>
    public bool FocusIsInOverlay => _focus.InOverlay;

    /// <summary>Gets the window's drawn text, which the page overlays share for their labels.</summary>
    public TextRuns Text => _runs;

    /// <summary>Gets a value indicating whether a menu, a dialog or the search bar is open.</summary>
    public bool HasOverlay => _overlay is not null;

    /// <summary>Gets a value indicating whether F11 has put the window into full screen.</summary>
    public bool IsFullScreen => _fullScreen;

    /// <summary>Gets the controls of the bars, back to front.</summary>
    public IReadOnlyList<ChromeItem> Bars
    {
        get
        {
            Layout();
            return _bars;
        }
    }

    /// <summary>Gets the controls of the open menu, dialog or search bar, back to front. Empty when none is open.</summary>
    public IReadOnlyList<ChromeItem> OverlayItems
    {
        get
        {
            Layout();
            return _overlayItems;
        }
    }

    /// <summary>Opens a menu.</summary>
    /// <param name="entries">The entries.</param>
    /// <param name="at">Where the menu's top left corner is wanted, in device pixels.</param>
    public void ShowMenu(IReadOnlyList<MenuEntry> entries, ScreenPoint at)
    {
        ArgumentNullException.ThrowIfNull(entries);

        // A menu entry carries its own command, so the entry chosen says which command to run.
        Show(MenuOverlays.Open(entries, at, RunEntry));
    }

    /// <summary>Asks a yes or no question in a dialog. The dialog opens when the answer is subscribed to.</summary>
    /// <param name="request">What is asked.</param>
    /// <returns>One value when the dialog closes: <see langword="true"/> when the user agreed.</returns>
    public IObservable<bool> Confirm(ConfirmRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Signal.Defer(() =>
        {
            var dialog = ConfirmOverlays.Open(request);
            Show(dialog.Overlay);
            return dialog.Answer;
        });
    }

    /// <summary>Asks for a file in a dialog. The dialog opens when the answer is subscribed to.</summary>
    /// <param name="folder">The folder to start in.</param>
    /// <param name="saveName">The file name to offer when asking where to save, or <see langword="null"/> to ask what to open.</param>
    /// <returns>One value when the dialog closes: the path chosen, or <see langword="null"/> when it was cancelled.</returns>
    public IObservable<string?> PickFile(string folder, string? saveName)
    {
        ArgumentNullException.ThrowIfNull(folder);
        return Signal.Defer(() =>
        {
            var dialog = FileOverlays.Open(folder, saveName, false, false);
            Show(dialog.Overlay);
            return dialog.Answer.Select(static paths => paths.Count > 0 ? paths[0] : null);
        });
    }

    /// <summary>Asks for a folder in a dialog. The dialog opens when the answer is subscribed to.</summary>
    /// <param name="folder">The folder to start in.</param>
    /// <returns>One value when the dialog closes: the folder chosen, or <see langword="null"/> when it was cancelled.</returns>
    public IObservable<string?> PickFolder(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        return Signal.Defer(() =>
        {
            var dialog = FileOverlays.Open(folder, null, false, true);
            Show(dialog.Overlay);
            return dialog.Answer.Select(static paths => paths.Count > 0 ? paths[0] : null);
        });
    }

    /// <summary>Asks for the files to open in a dialog where several can be ticked. The dialog opens when the answer is subscribed to.</summary>
    /// <param name="folder">The folder to start in.</param>
    /// <returns>One value when the dialog closes: the paths chosen, or none when it was cancelled.</returns>
    public IObservable<IReadOnlyList<string>> PickFiles(string folder)
    {
        ArgumentNullException.ThrowIfNull(folder);
        return Signal.Defer(() =>
        {
            var dialog = FileOverlays.Open(folder, null, true, false);
            Show(dialog.Overlay);
            return dialog.Answer;
        });
    }

    /// <summary>Asks for one line of text in a dialog. The dialog opens when the answer is subscribed to.</summary>
    /// <param name="request">What is asked.</param>
    /// <returns>One value when the dialog closes: the text typed, or <see langword="null"/> when it was cancelled.</returns>
    public IObservable<string?> Prompt(PromptRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Signal.Defer(() =>
        {
            var dialog = PromptOverlays.Open(request);
            Show(dialog.Overlay);
            return dialog.Answer;
        });
    }

    /// <summary>Shows the preferences window when subscribed to.</summary>
    /// <param name="preferences">The preferences.</param>
    /// <returns>One value when the window closes.</returns>
    public IObservable<bool> ShowPreferences(PreferencesViewModel preferences)
    {
        ArgumentNullException.ThrowIfNull(preferences);
        return ShowForm(PreferencesForm.For(preferences));
    }

    /// <summary>Shows the properties of a document when subscribed to.</summary>
    /// <param name="properties">The properties.</param>
    /// <returns>One value when the window closes.</returns>
    public IObservable<bool> ShowProperties(PropertiesViewModel properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return ShowForm(DocumentForms.Properties(properties));
    }

    /// <summary>Shows the licences of everything the app is built from when subscribed to.</summary>
    /// <param name="licences">The licences.</param>
    /// <returns>One value when the window closes.</returns>
    public IObservable<bool> ShowLicences(LicencesViewModel licences)
    {
        ArgumentNullException.ThrowIfNull(licences);
        return ShowForm(DocumentForms.Licences(licences));
    }

    /// <summary>Shows the print window when subscribed to.</summary>
    /// <param name="print">The print settings.</param>
    /// <returns>One value when the window closes: <see langword="true"/> when the user chose to print.</returns>
    public IObservable<bool> ShowPrint(PrintPreviewViewModel print)
    {
        ArgumentNullException.ThrowIfNull(print);
        return ShowForm(DocumentForms.Print(print, ShownSheet));
    }

    /// <summary>Shows the window that plays sound or film when subscribed to. The film stops when the window is put away.</summary>
    /// <param name="player">The player.</param>
    /// <returns>One value when the window closes.</returns>
    public IObservable<bool> ShowMedia(MediaPlayerViewModel player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return ShowForm(ToolForms.MediaPlayer(player, _ => ShownFilm(player))).Finally(() => Signal.Return(RxVoid.Default).InvokeCommand(player.StopCommand).Dispose());
    }

    /// <summary>Opens the search bar of the tab shown.</summary>
    public void ShowFind()
    {
        if (_main.SelectedTab is not { } tab)
        {
            return;
        }

        tab.Search.Open();
        ShowFindBar();
    }

    /// <summary>Shows the search bar of the tab shown, unless it is already showing.</summary>
    public void ShowFindBar()
    {
        if (_main.SelectedTab is not { } tab || _overlay is { Kind: OverlayKind.Find })
        {
            return;
        }

        Show(FindOverlays.Open(tab.Search));
    }

    /// <summary>Presses a control, as a screen reader or a switch device does through the accessibility tree.</summary>
    /// <param name="inOverlay">Whether the control is in the open menu, dialog or search bar rather than the bars.</param>
    /// <param name="index">The control's place in <see cref="OverlayItems"/> or <see cref="Bars"/>.</param>
    /// <returns><see langword="true"/> when the control was pressed.</returns>
    public bool PressControl(bool inOverlay, int index)
    {
        Layout();
        var items = inOverlay ? _overlayItems : _bars;
        if ((uint)index >= (uint)items.Count || items[index] is not { Enabled: true } item || item.Command == ChromeCommand.None)
        {
            return false;
        }

        if (!inOverlay)
        {
            Emit(new(item.Command, item.Index, item.Bounds));
            return true;
        }

        if (_overlay is null)
        {
            return false;
        }

        SendToOverlay(OverlayInput.Press(item));
        return true;
    }

    /// <summary>Puts the keyboard on a control, as a screen reader or a magnifier does when it follows or sets focus through the accessibility tree.</summary>
    /// <param name="inOverlay">Whether the control is in the open menu, dialog or search bar rather than the bars.</param>
    /// <param name="index">The control's place in <see cref="OverlayItems"/> or <see cref="Bars"/>.</param>
    /// <returns><see langword="true"/> when the control can take the keyboard and now has it.</returns>
    public bool FocusControl(bool inOverlay, int index)
    {
        Layout();
        var items = inOverlay ? _overlayItems : _bars;
        if ((uint)index >= (uint)items.Count || !ChromeFocus.CanStopOn(items[index]))
        {
            return false;
        }

        _focus = ChromeFocus.At(items, inOverlay, index);
        _version++;
        _window.RequestFrame();
        return true;
    }

    /// <summary>Draws the controls over the pages.</summary>
    /// <param name="pass">The frame.</param>
    public void Draw(IFramePass pass)
    {
        ArgumentNullException.ThrowIfNull(pass);
        ReleaseRetired();
        Layout();
        ChromePainter.Draw(pass, _bars, _look, _hover);
        if (_overlay is not { } overlay)
        {
            DrawFocus(pass, _bars, false);
            DrawTabPreview(pass);
            return;
        }

        if (overlay.IsModal)
        {
            var size = pass.Size;
            pass.FillRect(new(0, 0, size.Width, size.Height), _look.Theme.Scrim);
        }

        ChromePainter.Draw(pass, _overlayItems, _look, _hover);
        DrawFocus(pass, _overlayItems, true);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _owned.Dispose();
        _tabBindings.Dispose();
        var open = _overlay;
        _overlay = null;
        open?.Input.OnNext(OverlayInput.Dismiss());
        _overlayLinks?.Dispose();
        _overlayLinks = null;
        for (var i = _under.Count - 1; i >= 0; i--)
        {
            var waiting = _under[i];
            waiting.Overlay.Input.OnNext(OverlayInput.Dismiss());
            waiting.Links.Dispose();
        }

        _under.Clear();
        ReleaseRetired();
        _presses.Dispose();
        _pageEvents.Dispose();
        _incoming.Dispose();
        _shortcuts.Dispose();
        SheetFrames.Release(_sheetFrame);
        MediaFrames.Release(_mediaFrame);
        _runs.Dispose();
        _icons.Dispose();
    }

    /// <summary>Shows a window made of rows when subscribed to.</summary>
    /// <param name="request">What the window shows.</param>
    /// <returns>One value when the window closes: <see langword="true"/> when the user agreed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal IObservable<bool> ShowForm(FormRequest request) => Signal.Defer(() =>
    {
        var dialog = FormOverlays.Open(request);
        Show(dialog.Overlay);
        return dialog.Answer;
    });

    /// <summary>Opens a typing box over a form field or a text box on the page, so its text is typed where it lies.</summary>
    /// <param name="target">What is typed into.</param>
    /// <param name="canvas">The page canvas, which knows where the page lies in the window.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ShowTyping(TypingTarget target, ViewerCanvas canvas) => Show(TypingOverlays.Open(target, canvas));

    /// <summary>Shows focus mode in place of the pages.</summary>
    /// <param name="focus">The focus mode of the tab shown.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ShowFocus(FocusModeViewModel focus) => Show(FocusOverlays.Open(focus));

    /// <summary>Shows the start view, which stands in for the pages while no document is open.</summary>
    /// <param name="main">The main view model.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void ShowStart(MainViewModel main) => Show(StartOverlays.Open(main));

    /// <summary>Shows the side panel's list from its start again, as when another list is chosen.</summary>
    internal void ResetSidebarScroll() => _sidebar = _sidebar with { First = 0 };

    /// <summary>Finds what a row of the side panel stands for.</summary>
    /// <param name="press">The press of the row.</param>
    /// <returns>The page, bookmark, note, attached file or layer, or <see langword="null"/> for a row that cannot be picked.</returns>
    internal object? SidebarTarget(in ChromePress press) =>
        (uint)press.Index < (uint)_sidebar.Rows.Length && _sidebar.Rows[press.Index] is { Enabled: true } row ? row.Target : null;

    /// <summary>Takes the start view away when a document opened.</summary>
    internal void CloseStart()
    {
        if (_overlay is { Kind: OverlayKind.Start })
        {
            CloseOverlay();
        }
    }

    /// <summary>Goes back to the pages when focus mode was turned off.</summary>
    internal void CloseFocus()
    {
        if (_overlay is { Kind: OverlayKind.Focus })
        {
            CloseOverlay();
        }
    }

    /// <summary>Closes the typing box when the view model ended the typing by itself.</summary>
    internal void CloseTyping()
    {
        if (_overlay is { Kind: OverlayKind.Typing })
        {
            CloseOverlay();
        }
    }

    /// <summary>Marks the bars as changed and asks for a frame, which lays them out again.</summary>
    internal void InvalidateBars()
    {
        _barsChanged = true;
        _version++;
        _window.RequestFrame();
    }

    /// <summary>Tells whether a key goes to the open overlay. One that does not take every key still takes Enter, Escape and Backspace.</summary>
    /// <param name="overlay">The overlay.</param>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when the overlay takes the key.</returns>
    private static bool Reaches(Overlay overlay, ViewerKey key) => overlay.TakesAllKeys || key is ViewerKey.Enter or ViewerKey.Escape or ViewerKey.Back;

    /// <summary>Tells whether a control is a row of a dialog's list, rather than one of its buttons.</summary>
    /// <param name="item">The control.</param>
    /// <returns><see langword="true"/> for a row.</returns>
    private static bool IsListRow(ChromeItem item) => item.Command is ChromeCommand.PickRow or ChromeCommand.MenuEntry;

    /// <summary>Finds the control at a point that can be pressed.</summary>
    /// <param name="items">The controls, back to front.</param>
    /// <param name="x">The x in device pixels.</param>
    /// <param name="y">The y in device pixels.</param>
    /// <returns>The control in front, or <see langword="null"/>.</returns>
    private static ChromeItem? Find(List<ChromeItem> items, int x, int y)
    {
        for (var i = items.Count - 1; i >= 0; i--)
        {
            var item = items[i];
            if (item.Command != ChromeCommand.None && item.Enabled && item.Bounds.Contains(x, y))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>Runs the command of a menu entry the user chose, when it can run.</summary>
    /// <param name="entry">The entry.</param>
    private static void RunEntry(MenuEntry entry)
    {
        if (entry.Command is { } command && command.CanExecute(entry.Parameter))
        {
            command.Execute(entry.Parameter);
        }
    }

    /// <summary>Shows an overlay that needs nothing besides itself, in place of the one that is open.</summary>
    /// <param name="overlay">The overlay.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Show(Overlay overlay) => Show(overlay, EmptyDisposable.Instance);

    /// <summary>Shows an overlay in place of the one that is open, and joins it to its input, its changes and its wish to close.</summary>
    /// <param name="overlay">The overlay.</param>
    /// <param name="extra">What else lives as long as the overlay is open: what made it, and what follows its answer.</param>
    private void Show(Overlay overlay, IDisposable extra)
    {
        if (_overlay is { } open && ((open.IsModal && overlay.IsModal) || open.StaysUnder) && _overlayLinks is { } links)
        {
            // A dialog opened by a dialog, such as the file dialog a tool window asks for, goes over it. So does anything opened over
            // focus mode or the start view. The first one waits, without input, and comes back when the second one closes.
            _under.Add(new(open, links));
        }
        else
        {
            // The overlay that is open answers whoever asked for it before it is let go.
            var old = _overlay;
            _overlay = null;
            old?.Input.OnNext(OverlayInput.Dismiss());
            Retire(_overlayLinks);
        }

        _overlay = overlay;
        _version++;

        // The keyboard starts outside the new overlay's controls, so Enter and Escape answer the overlay as a whole.
        _focus = ChromeFocus.None;

        // An overlay over a view model follows it: the search bar its matches, the print window its printers and sheets. The overlay
        // is let go last, after everything joined to it.
        _overlayLinks = new MultipleDisposable(
            overlay.Changes.ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(_ => InvalidateOverlay(), ErrorTrace.Write),
            overlay.Closed.SubscribeSafe(_ => Close(overlay), ErrorTrace.Write),
            extra,
            overlay.Lifetime);
        BuildOverlay();
        _window.RequestFrame();
    }

    /// <summary>
    /// Closes an overlay that asked to close. One that waits under another, as a dialog does when its answer opened the next
    /// dialog, is let go where it waits, so it does not come back.
    /// </summary>
    /// <param name="overlay">The overlay.</param>
    private void Close(Overlay overlay)
    {
        if (ReferenceEquals(_overlay, overlay))
        {
            CloseOverlay();
            return;
        }

        for (var i = _under.Count - 1; i >= 0; i--)
        {
            if (!ReferenceEquals(_under[i].Overlay, overlay))
            {
                continue;
            }

            var links = _under[i].Links;
            _under.RemoveAt(i);
            Retire(links);
            return;
        }
    }

    /// <summary>Opens the main menu under the button that was pressed.</summary>
    /// <param name="press">The press of the menu button.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ShowMainMenu(ChromePress press) => ShowMenu(MainMenu.Build(_main), new((int)press.Bounds.X, (int)press.Bounds.Bottom));

    /// <summary>Opens the menu of the extra marking tools and text settings under the button that was pressed.</summary>
    /// <param name="press">The press of the button.</param>
    private void ShowMoreMarks(ChromePress press)
    {
        if (_main.SelectedTab is { } tab)
        {
            ShowMenu(ToolMenus.MoreMarks(tab.Annotations), new((int)press.Bounds.X, (int)press.Bounds.Bottom));
        }
    }

    /// <summary>Joins the presses of the controls to the tab shown.</summary>
    /// <param name="tab">The tab, or <see langword="null"/> when none is open.</param>
    private void JoinTab(DocumentTabViewModel? tab) =>
        _tabBindings.Disposable = tab is null ? null : ChromeBindings.Tab(_presses, tab, this);

    /// <summary>
    /// Joins the events of the window to the controls, one stream for each kind of event. An event the controls use goes no further. One
    /// they do not use goes on to the pages, and so does a new size, which moves every control and which the pages need too. What closed an overlay
    /// while an event passed through its input is let go once the event is done with.
    /// </summary>
    /// <returns>What ends the joins.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private MultipleDisposable RouteEvents() => new(
        Signal.Merge(
                _incoming.WhereEventKind(WindowEventKind.PointerMoved).WhereUnused(this, static (chrome, e) => chrome.MovePointer(e.X, e.Y)),
                _incoming.WhereEventKind(WindowEventKind.PointerDown).WhereUnused(this, static (chrome, e) => chrome.Press(e)),
                _incoming.WhereEventKind(WindowEventKind.PointerUp).WhereUnused(this, static (chrome, e) => chrome.Release(e.X, e.Y)),
                _incoming.WhereEventKind(WindowEventKind.Scrolled).WhereUnused(this, static (chrome, e) => chrome.Scroll(e)),
                _incoming.WhereEventKind(WindowEventKind.KeyDown).WhereUnused(this, static (chrome, e) => chrome.PressKey(e.Key, e.Held)),
                _incoming.WhereEventKind(WindowEventKind.KeyUp).WhereUnused(this, static (chrome, _) => chrome._overlay is { TakesAllKeys: true }),
                _incoming.WhereEventKind(WindowEventKind.TextInput).WhereUnused(this, static (chrome, e) => chrome.Type((int)e.Code)),
                _incoming.WhereEventKind(WindowEventKind.Resized).Do(_ => InvalidateBars()),
                _incoming.Where(static e => e.Kind is not (WindowEventKind.PointerMoved or WindowEventKind.PointerDown or WindowEventKind.PointerUp or WindowEventKind.Scrolled
                    or WindowEventKind.KeyDown or WindowEventKind.KeyUp or WindowEventKind.TextInput or WindowEventKind.Resized)))
            .SubscribeSafe(_pageEvents.OnNext, ErrorTrace.Write),
        _incoming.SubscribeSafe(_ => ReleaseRetired(), ErrorTrace.Write));

    /// <summary>Keeps what a closed overlay was joined to until nothing passes through its input.</summary>
    /// <param name="links">What the overlay was joined to, or <see langword="null"/> for none.</param>
    private void Retire(IDisposable? links)
    {
        if (links is not null)
        {
            _retired.Add(links);
        }
    }

    /// <summary>Lets go of everything closed overlays were joined to.</summary>
    private void ReleaseRetired()
    {
        for (var i = 0; i < _retired.Count; i++)
        {
            _retired[i].Dispose();
        }

        _retired.Clear();
    }

    /// <summary>Sends a press of a control of the bars to whatever is joined to it.</summary>
    /// <param name="press">The press.</param>
    private void Emit(in ChromePress press)
    {
        _presses.OnNext(press);
        _window.RequestFrame();
    }

    /// <summary>Sends what the user did to the open overlay, which is then laid out again.</summary>
    /// <param name="input">What the user did.</param>
    private void SendToOverlay(in OverlayInput input)
    {
        _overlay?.Input.OnNext(input);
        InvalidateOverlay();
    }

    /// <summary>Closes the overlay.</summary>
    private void CloseOverlay()
    {
        Retire(_overlayLinks);
        _overlayLinks = null;
        _padHeld = false;
        _version++;
        _overlay = null;
        _overlayItems.Clear();
        if (_focus.InOverlay)
        {
            _focus = ChromeFocus.None;
        }

        if (_under.Count > 0)
        {
            // The dialog that waited under this one takes the window and its input back.
            var waiting = _under[^1];
            _under.RemoveAt(_under.Count - 1);
            _overlay = waiting.Overlay;
            _overlayLinks = waiting.Links;
            InvalidateOverlay();
            return;
        }

        _hover = Find(_bars, _pointer.X, _pointer.Y);
        _window.RequestFrame();
    }

    /// <summary>Draws a ring round the control the keyboard is on, when it is in the list being drawn.</summary>
    /// <param name="pass">The frame.</param>
    /// <param name="items">The controls being drawn.</param>
    /// <param name="inOverlay">Whether they are the controls of the open menu, dialog or search bar.</param>
    private void DrawFocus(IFramePass pass, List<ChromeItem> items, bool inOverlay)
    {
        if (_focus.InOverlay == inOverlay && _focus.Item(items) is { } focused)
        {
            ChromePainter.DrawFocus(pass, focused, _look);
        }
    }

    /// <summary>Marks the open overlay as changed and asks for a frame, which lays it out again.</summary>
    private void InvalidateOverlay()
    {
        _overlayChanged = true;
        _version++;
        _window.RequestFrame();
    }

    /// <summary>Lays the controls out again when what they show changed.</summary>
    private void Layout()
    {
        var barsChanged = _barsChanged;
        if (barsChanged)
        {
            _barsChanged = false;
            _look = _look with { Metrics = ChromeMetrics.For(_window.Scale) };
            var laid = ChromeBars.Build(_bars, _main, _look, _sidebar, _window.ClientSize, _toolActions);
            _canvasBounds = laid.Canvas;
            _sidebar = laid.Sidebar;
            if (!_focus.InOverlay)
            {
                _focus = _focus.Follow(_bars);
            }
        }

        if (_overlay is { } overlay && (barsChanged || _overlayChanged || overlay.HasMoved?.Invoke() == true))
        {
            _overlayChanged = false;
            BuildOverlay();
        }
        else if (barsChanged && _pointerSeen)
        {
            // Nothing is under a pointer that has not been seen in the window yet.
            _hover = Find(_bars, _pointer.X, _pointer.Y);
        }
    }

    /// <summary>Lays the overlay out and finds the control under the pointer again.</summary>
    private void BuildOverlay()
    {
        if (_overlay is not { } overlay)
        {
            return;
        }

        _overlayItems.Clear();
        overlay.Layout(_overlayItems, new(_runs, _look.Metrics, _window.ClientSize, _canvasBounds));
        _hover = Find(_overlayItems, _pointer.X, _pointer.Y);
        if (_focus.InOverlay)
        {
            _focus = _focus.Follow(_overlayItems);
        }
    }

    /// <summary>Tells whether a point is over the controls rather than the pages.</summary>
    /// <param name="x">The x in device pixels.</param>
    /// <param name="y">The y in device pixels.</param>
    /// <returns><see langword="true"/> when input at the point belongs to the controls.</returns>
    private bool IsOverChrome(int x, int y)
    {
        Layout();
        return _overlay is { IsModal: true } || IsOverOverlay(x, y) || !_canvasBounds.Contains(x, y);
    }

    /// <summary>Tells whether a point is inside the open menu, dialog or search bar: its first control, or any panel it lays out.</summary>
    /// <param name="x">The x in device pixels.</param>
    /// <param name="y">The y in device pixels.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private bool IsOverOverlay(int x, int y)
    {
        for (var i = 0; i < _overlayItems.Count; i++)
        {
            var item = _overlayItems[i];
            if ((i == 0 || item.Kind == ChromeItemKind.Popup) && item.Bounds.Contains(x, y))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Follows the pointer, so the control under it is marked.</summary>
    /// <param name="x">The x in device pixels.</param>
    /// <param name="y">The y in device pixels.</param>
    /// <returns><see langword="true"/> when the pointer is over the controls.</returns>
    private bool MovePointer(int x, int y)
    {
        _pointer = new(x, y);
        _pointerSeen = true;
        if (_padHeld)
        {
            SendToOverlay(OverlayInput.Pad(OverlayInputKind.PadMove, x, y));
        }

        var over = IsOverChrome(x, y);
        var hover = IsOverOverlay(x, y) ? Find(_overlayItems, x, y) : null;
        if (hover is null && _overlay is not { IsModal: true })
        {
            hover = Find(_bars, x, y);
        }

        if (!ReferenceEquals(hover, _hover))
        {
            _hover = hover;
            _window.RequestFrame();
        }

        if (over)
        {
            _window.SetCursor(ViewerCursor.Arrow);
        }

        return over;
    }

    /// <summary>Handles a pointer press that is inside the open menu, dialog or search bar: it presses the control under it, or starts a stroke on a drawing pad.</summary>
    /// <param name="overlay">The open overlay.</param>
    /// <param name="x">The x in device pixels.</param>
    /// <param name="y">The y in device pixels.</param>
    /// <param name="buttons">The buttons held.</param>
    private void PressInOverlay(Overlay overlay, int x, int y, ViewerButtons buttons)
    {
        if ((buttons & ViewerButtons.Left) == 0)
        {
            return;
        }

        if (Find(_overlayItems, x, y) is { } pressed)
        {
            SendToOverlay(OverlayInput.Press(pressed));
            return;
        }

        if (!overlay.TakesPointer)
        {
            return;
        }

        _padHeld = true;
        SendToOverlay(OverlayInput.Pad(OverlayInputKind.PadDown, x, y));
    }

    /// <summary>Handles the pointer being let go: it ends a stroke on a drawing pad, and otherwise belongs to whatever it is over.</summary>
    /// <param name="x">The x in device pixels.</param>
    /// <param name="y">The y in device pixels.</param>
    /// <returns><see langword="true"/> when the pointer is over the controls or ended a stroke.</returns>
    private bool Release(int x, int y)
    {
        // A tab let go over another tab takes that tab's place, which is how tabs are put in a new order.
        var dragged = _tabDragFrom;
        _tabDragFrom = -1;
        if (dragged >= 0 && Find(_bars, x, y) is { Command: ChromeCommand.SelectTab } target && target.Index != dragged)
        {
            _main.MoveTab(dragged, target.Index);
            return true;
        }

        if (!_padHeld)
        {
            return IsOverChrome(x, y);
        }

        _padHeld = false;
        SendToOverlay(OverlayInput.Pad(OverlayInputKind.PadUp, x, y));
        return true;
    }

    /// <summary>Draws the preview of the tab the pointer is over: the page it shows, its name and where it is in the document.</summary>
    /// <param name="pass">The frame.</param>
    private void DrawTabPreview(IFramePass pass)
    {
        _previewTab = null;
        if (_hover is not { Command: ChromeCommand.SelectTab } hovered || (uint)hovered.Index >= (uint)_main.Tabs.Count)
        {
            return;
        }

        var tab = _main.Tabs[hovered.Index];
        _previewTab = tab;

        // The controls are made again only when something they show has changed, so a hover that rests costs no allocation.
        var key = new TabPreviewKey(tab, hovered, TabPreviews.PageOf(tab), tab.PageCount, _window.ClientSize.Width, _window.Scale);
        if (!key.IsSameAs(_previewKey))
        {
            _previewKey = key;
            _previewItems.Clear();
            TabPreviews.Add(_previewItems, tab, hovered, _runs, _look.Metrics, key.Scale, key.WindowWidth);
        }

        ChromePainter.Draw(pass, _previewItems, _look, null);
    }

    /// <summary>Handles a press on a tab with the middle or right button: the middle button closes the tab and the right opens its menu.</summary>
    /// <param name="buttons">The buttons held.</param>
    /// <param name="tabItem">The tab.</param>
    /// <returns><see langword="true"/> when the press was one of these.</returns>
    private bool PressTabWithAnotherButton(ViewerButtons buttons, ChromeItem tabItem)
    {
        if ((buttons & ViewerButtons.Middle) != 0)
        {
            Emit(new(ChromeCommand.CloseTab, tabItem.Index, tabItem.Bounds));
            return true;
        }

        if ((buttons & ViewerButtons.Right) == 0 || (uint)tabItem.Index >= (uint)_main.Tabs.Count)
        {
            return false;
        }

        ShowMenu(MainMenu.ForTab(_main, _main.Tabs[tabItem.Index]), new((int)tabItem.Bounds.X, (int)tabItem.Bounds.Bottom));
        return true;
    }

    /// <summary>Handles a pointer press.</summary>
    /// <param name="windowEvent">The press.</param>
    /// <returns><see langword="true"/> when the press was on the controls.</returns>
    private bool Press(in WindowEvent windowEvent)
    {
        var x = windowEvent.X;
        var y = windowEvent.Y;
        _pointer = new(x, y);
        _pointerSeen = true;
        Layout();
        if (_overlay is { } overlay)
        {
            if (IsOverOverlay(x, y))
            {
                PressInOverlay(overlay, x, y, windowEvent.Buttons);
                return true;
            }

            if (overlay.ClosesOnOutsidePress)
            {
                // Being sent away makes the overlay ask to close, which closes it. One that did not ask is closed here.
                overlay.Input.OnNext(OverlayInput.Dismiss());
                Close(overlay);
                return true;
            }

            if (overlay.IsModal)
            {
                return true;
            }
        }

        if (_canvasBounds.Contains(x, y))
        {
            return false;
        }

        PressBars(windowEvent.Buttons, x, y);
        return true;
    }

    /// <summary>Handles a press on the bars: a left press runs the control, and on a tab the other buttons close it or open its menu.</summary>
    /// <param name="buttons">The buttons held.</param>
    /// <param name="x">The x in device pixels.</param>
    /// <param name="y">The y in device pixels.</param>
    private void PressBars(ViewerButtons buttons, int x, int y)
    {
        if (Find(_bars, x, y) is not { } item)
        {
            return;
        }

        if (item.Command == ChromeCommand.SelectTab && PressTabWithAnotherButton(buttons, item))
        {
            return;
        }

        if ((buttons & ViewerButtons.Left) == 0)
        {
            return;
        }

        _tabDragFrom = item.Command == ChromeCommand.SelectTab ? item.Index : -1;
        Emit(new(item.Command, item.Index, item.Bounds));
    }

    /// <summary>Handles a scroll.</summary>
    /// <param name="windowEvent">The scroll.</param>
    /// <returns><see langword="true"/> when the scroll belongs to the controls.</returns>
    private bool Scroll(in WindowEvent windowEvent)
    {
        if (_overlay is not { IsModal: true })
        {
            // A scroll carries no place, so it goes where the pointer last was. Before the pointer has moved, that is the pages.
            var overChrome = _pointerSeen && IsOverChrome(_pointer.X, _pointer.Y);
            if (overChrome && _sidebar.Bounds.Contains(_pointer.X, _pointer.Y) && !IsOverOverlay(_pointer.X, _pointer.Y))
            {
                ScrollSidebar(-Math.Sign(windowEvent.Y));
            }
            else if (overChrome && _pointer.Y < _look.Metrics.TabHeight && !IsOverOverlay(_pointer.X, _pointer.Y))
            {
                ScrollTabs(-Math.Sign(windowEvent.Y));
            }

            return overChrome;
        }

        // A notch up scrolls a list towards its start.
        SendToOverlay(OverlayInput.Scroll(-Math.Sign(windowEvent.Y)));
        return true;
    }

    /// <summary>Moves to a neighbouring tab, which is how the tab strip scrolls: it always keeps the chosen tab in view.</summary>
    /// <param name="step">The tabs to move on, negative to move back.</param>
    private void ScrollTabs(int step)
    {
        if (_main.SelectedTab is not { } current)
        {
            return;
        }

        var next = Math.Clamp(_main.Tabs.IndexOf(current) + step, 0, _main.Tabs.Count - 1);
        _main.SelectedTab = _main.Tabs[next];
    }

    /// <summary>Scrolls the list of bookmarks in the side panel.</summary>
    /// <param name="rows">The rows to move down, negative to move up.</param>
    private void ScrollSidebar(int rows)
    {
        var first = Math.Clamp(_sidebar.First + rows, 0, Math.Max(0, _sidebar.Rows.Length - _sidebar.VisibleRows));
        if (first == _sidebar.First)
        {
            return;
        }

        _sidebar = _sidebar with { First = first };
        InvalidateBars();
    }

    /// <summary>Gets the picture of a sheet of the print window, drawn again only when another sheet or size is wanted.</summary>
    /// <param name="sheet">The sheet.</param>
    /// <param name="room">The room for it.</param>
    /// <returns>The picture, or <see langword="null"/> when it cannot be drawn.</returns>
    private IRenderSurface? ShownSheet(PrintPreviewPage sheet, SurfaceSize room)
    {
        _sheetFrame = SheetFrames.Get(_window.Backend.Surfaces, _sheetFrame, sheet, room);
        return _sheetFrame.Surface;
    }

    /// <summary>Gets the latest picture of the film the media window plays.</summary>
    /// <param name="player">The player.</param>
    /// <returns>The picture, or <see langword="null"/> before the first one.</returns>
    private IRenderSurface? ShownFilm(MediaPlayerViewModel player)
    {
        _mediaFrame = MediaFrames.Get(_window.Backend.Surfaces, _mediaFrame, player);
        return _mediaFrame.Surface;
    }

    /// <summary>Gets what is drawn on the drawing pad of the open dialog.</summary>
    /// <returns>The ink, or <see langword="null"/> when the open overlay has no pad.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PadInk? InkOf() => _overlay?.Ink?.Invoke();

    /// <summary>Gets the picture of the page a row of the pages panel shows.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The picture, or <see langword="null"/> while it is being made.</returns>
    private IRenderSurface? ThumbnailOf(int row)
    {
        // A dialog's own picture, such as the sheet a print makes, has a number no row of the pages panel has.
        if (row == Overlay.PictureIndex)
        {
            return _overlay?.Picture?.Invoke();
        }

        // So has the picture of the page a tab's preview shows.
        if (row == TabPreviews.PictureIndex)
        {
            return _previewTab is { } previewed ? PageThumbnails.Get(ref _previewAsks, previewed, TabPreviews.PageOf(previewed)) : null;
        }

        return _main.SelectedTab is { } tab && (uint)row < (uint)_sidebar.Rows.Length ? PageThumbnails.Get(ref _thumbnails, tab, _sidebar.Rows[row].Page) : null;
    }

    /// <summary>Handles a key.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <returns><see langword="true"/> when the key was used.</returns>
    private bool PressKey(ViewerKey key, ViewerModifiers modifiers)
    {
        Layout();
        if (_overlay is { IsModal: false } && (RunShortcut(key, modifiers) || OpenMenuByKey(key)))
        {
            // A shortcut and the menu key still work over the search bar, focus mode or the start view. A dialog keeps every key to itself.
            return true;
        }

        return _overlay is { } overlay ? PressOverlayKey(overlay, key, modifiers) : PressWindowKey(key, modifiers);
    }

    /// <summary>Handles a key while no menu or dialog is open: a shortcut, the keys that move the keyboard to the controls, or a key for the control it is on.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <returns><see langword="true"/> when the key was used.</returns>
    private bool PressWindowKey(ViewerKey key, ViewerModifiers modifiers)
    {
        if (RunShortcut(key, modifiers))
        {
            return true;
        }

        if (key == ViewerKey.F6)
        {
            // F6 goes from the page to the first control, and from the controls back to the page.
            if (_focus.Index >= 0)
            {
                LeaveFocus();
            }
            else
            {
                MoveFocus(_bars, false, 1);
            }

            return true;
        }

        return OpenMenuByKey(key) || (_focus.Index >= 0 && PressFocusKey(key, modifiers));
    }

    /// <summary>Opens the main menu when the key is F10, under its button.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when the menu was opened.</returns>
    private bool OpenMenuByKey(ViewerKey key)
    {
        if (key != ViewerKey.F10 || _bars.Find(static item => item.Command == ChromeCommand.Menu) is not { } menu)
        {
            return false;
        }

        Emit(new(menu.Command, menu.Index, menu.Bounds));
        return true;
    }

    /// <summary>Runs the command of a shortcut key.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <returns><see langword="true"/> when the key is a shortcut that has something to act on now.</returns>
    private bool RunShortcut(ViewerKey key, ViewerModifiers modifiers)
    {
        // Delete and F2 edit text where a field, a note or the search bar is being typed in, so they belong to that while it is open.
        if (_overlay is not null && key is ViewerKey.Delete or ViewerKey.F2)
        {
            return false;
        }

        // Full screen belongs to the window, not to a view model, so F11 acts on the window itself.
        if (key == ViewerKey.F11 && modifiers == ViewerModifiers.None)
        {
            _fullScreen = !_fullScreen;
            _window.SetFullScreen(_fullScreen);
            return true;
        }

        if (Shortcuts.Find(_main, key, modifiers) is not { } shortcut)
        {
            return false;
        }

        _shortcuts.OnNext(shortcut);
        _window.RequestFrame();
        return true;
    }

    /// <summary>Handles a key while the keyboard is on a control of the bars. The page gets no keys until the keyboard goes back to it.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <returns><see langword="true"/>, as the key belongs to the controls.</returns>
    private bool PressFocusKey(ViewerKey key, ViewerModifiers modifiers)
    {
        switch (key)
        {
            case ViewerKey.Tab:
                {
                    MoveFocus(_bars, false, (modifiers & ViewerModifiers.Shift) != 0 ? -1 : 1);
                    break;
                }

            case ViewerKey.Right or ViewerKey.Down:
                {
                    MoveFocus(_bars, false, 1);
                    break;
                }

            case ViewerKey.Left or ViewerKey.Up:
                {
                    MoveFocus(_bars, false, -1);
                    break;
                }

            case ViewerKey.Enter or ViewerKey.Space when _focus.Item(_bars) is { } item:
                {
                    Emit(new(item.Command, item.Index, item.Bounds));
                    break;
                }

            case ViewerKey.Escape:
                {
                    LeaveFocus();
                    break;
                }

            default:
                {
                    break;
                }
        }

        return true;
    }

    /// <summary>Handles a key while a menu, a dialog or the search bar is open.</summary>
    /// <param name="overlay">The overlay.</param>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <returns><see langword="true"/> when the key was used.</returns>
    private bool PressOverlayKey(Overlay overlay, ViewerKey key, ViewerModifiers modifiers)
    {
        if (key == ViewerKey.V && modifiers == ViewerModifiers.Control)
        {
            Paste();
            return true;
        }

        if (!Reaches(overlay, key))
        {
            return false;
        }

        if (key == ViewerKey.Tab && !overlay.UsesTab)
        {
            TabThroughOverlay((modifiers & ViewerModifiers.Shift) != 0 ? -1 : 1);
            return true;
        }

        if (!PressFocusedInOverlay(key))
        {
            SendToOverlay(OverlayInput.KeyDown(key, modifiers));
        }

        return true;
    }

    /// <summary>Moves the keyboard from one control of the dialog to the next. Landing on a typing box also starts typing in it.</summary>
    /// <param name="step">1 for the next control, -1 for the one before.</param>
    private void TabThroughOverlay(int step)
    {
        if (!TryRevealRow(step))
        {
            MoveFocus(_overlayItems, true, step);
        }

        if (_focus.Item(_overlayItems) is { Kind: ChromeItemKind.Field } box)
        {
            SendToOverlay(OverlayInput.Press(box));
        }
    }

    /// <summary>
    /// Scrolls a long dialog by one row when the keyboard is on its last row in view and moving down, or on its first and moving
    /// up. The keyboard stays in the same place on screen, which is now the next row.
    /// </summary>
    /// <param name="step">1 when the keyboard is moving down, -1 when it is moving up.</param>
    /// <returns><see langword="true"/> when another row came into view and has the keyboard.</returns>
    private bool TryRevealRow(int step)
    {
        var slot = _focus.Index;
        if (!_focus.InOverlay || _focus.Item(_overlayItems) is not { } row || !IsListRow(row) || NextIsListRow(slot, step))
        {
            return false;
        }

        var before = OverlaySignature();
        _overlay?.Input.OnNext(OverlayInput.Scroll(step));
        _overlayChanged = true;
        Layout();
        if (OverlaySignature() == before || (uint)slot >= (uint)_overlayItems.Count || !ChromeFocus.CanStopOn(_overlayItems[slot]))
        {
            // The dialog was already at its end, so the keyboard moves on to the buttons as usual.
            return false;
        }

        _focus = ChromeFocus.At(_overlayItems, true, slot);
        _version++;
        _window.RequestFrame();
        return true;
    }

    /// <summary>Tells whether the next control the keyboard can stop on, in one direction, is another row of the dialog's list.</summary>
    /// <param name="from">The place of the control the keyboard is on.</param>
    /// <param name="step">1 to look down, -1 to look up.</param>
    /// <returns><see langword="true"/> when there is another row in view that way.</returns>
    private bool NextIsListRow(int from, int step)
    {
        for (var i = from + step; (uint)i < (uint)_overlayItems.Count; i += step)
        {
            if (ChromeFocus.CanStopOn(_overlayItems[i]))
            {
                return IsListRow(_overlayItems[i]);
            }
        }

        return false;
    }

    /// <summary>Makes a number out of what the open dialog shows, which changes when the dialog scrolls.</summary>
    /// <returns>The number.</returns>
    private int OverlaySignature()
    {
        var signature = default(HashCode);
        foreach (var item in _overlayItems)
        {
            signature.Add(item.Index);
            signature.Add(item.Text);
        }

        return signature.ToHashCode();
    }

    /// <summary>Presses the control of the dialog the keyboard is on, when Enter or Space is the key and the control is not a typing box.</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when the control was pressed.</returns>
    private bool PressFocusedInOverlay(ViewerKey key)
    {
        if (key is not (ViewerKey.Enter or ViewerKey.Space) || !_focus.InOverlay || _focus.Item(_overlayItems) is not { Kind: not ChromeItemKind.Field } focused)
        {
            return false;
        }

        SendToOverlay(OverlayInput.Press(focused));
        return true;
    }

    /// <summary>Moves the keyboard to the next or the previous control.</summary>
    /// <param name="items">The controls.</param>
    /// <param name="inOverlay">Whether the controls are those of the open menu, dialog or search bar.</param>
    /// <param name="step">1 for the next control, -1 for the one before.</param>
    private void MoveFocus(List<ChromeItem> items, bool inOverlay, int step)
    {
        if (_focus.Move(items, inOverlay, step) is not { } moved)
        {
            return;
        }

        _focus = moved;
        _version++;
        _window.RequestFrame();
    }

    /// <summary>Puts the keyboard back on the page.</summary>
    private void LeaveFocus()
    {
        _focus = ChromeFocus.None;
        _version++;
        _window.RequestFrame();
    }

    /// <summary>Types the clipboard's text into the open menu, dialog or search bar. A text box takes one line, so the paste stops at the first line break.</summary>
    private void Paste()
    {
        if (_window.GetClipboardText() is not { Length: > 0 } text)
        {
            return;
        }

        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value is '\n' or '\r')
            {
                break;
            }

            _overlay?.Input.OnNext(OverlayInput.Text(rune.Value));
        }

        InvalidateOverlay();
    }

    /// <summary>Handles a typed character.</summary>
    /// <param name="scalar">The Unicode scalar value.</param>
    /// <returns><see langword="true"/> when the character was used.</returns>
    private bool Type(int scalar)
    {
        if (_overlay is null)
        {
            return false;
        }

        if (scalar == ' ' && _focus.InOverlay && _focus.Item(_overlayItems) is { Kind: not ChromeItemKind.Field })
        {
            // Space pressed the control the keyboard is on. It is not also typed into a box elsewhere in the dialog.
            return true;
        }

        SendToOverlay(OverlayInput.Text(scalar));
        return true;
    }
}
